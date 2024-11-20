using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using wowzer.fs.Extensions;
using wowzer.fs.Utils;

namespace wowzer.fs.IO
{
    public class BLTE<T> : Stream where T : Stream
    {
        private readonly LimitedStream<T> _underlyingStream;
        private Stream? _currentChunk = null;
        private int _position = 0;

        private int _chunkIndex = 0;
        private readonly ChunkInfo[] _chunks;

        public BLTE(T sourceStream, long archiveSize)
        {
            _underlyingStream = sourceStream.ReadSlice(archiveSize);

            var magic = _underlyingStream.ReadUInt32LE();
            Debug.Assert(magic == 0x45544C42);

            var headerSize = _underlyingStream.ReadInt32BE();
            var chunkCount = _underlyingStream.ReadInt32BE();
            var flags = chunkCount >> 24;
            chunkCount &= 0xFFFFFF;

            Debug.Assert(flags == 0xF, "Unknown flags");

            _chunks = GC.AllocateUninitializedArray<ChunkInfo>(chunkCount);

            var compressedOffset = headerSize;
            var decompressedOffset = 0;
            for (var i = 0; i < chunkCount; ++i)
            {
                var compressedSize = _underlyingStream.ReadInt32BE();
                var decompressedSize = _underlyingStream.ReadInt32BE();

                var compressedRange = new Range(compressedOffset, compressedOffset + compressedSize);
                var decompressedRange = new Range(decompressedOffset, decompressedOffset + decompressedSize);

                var checksum = _underlyingStream.ReadUInt128BE();
                _chunks[i] = new(compressedRange, decompressedRange, checksum, 0, decompressedSize);

                compressedOffset += compressedSize;
                decompressedOffset += decompressedSize;
            }

            // Read-ahead the compression byte and initialize chunk state
            TryInitialize(ref _chunks.UnsafeIndex(_chunkIndex));
        }

        public override bool CanRead { get; } = true;
        public override bool CanWrite { get; } = false;
        public override bool CanSeek => _underlyingStream.CanSeek;

        public override long Length => _chunks.Sum(chunk => chunk.Compressed.Count());

        public override long Position
        {
            get => _position;
            set => SeekCore(value);
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_chunkIndex >= _chunks.Length)
                return 0;

            var totalReadCount = 0;
            while (count > 0)
            {
                ref var currentChunk = ref _chunks.UnsafeIndex(_chunkIndex);
                Debug.Assert(currentChunk.CompressionMode != 0 && _currentChunk != null, $"Invalid state for chunk {_chunkIndex}");

                while (currentChunk.Remainder > 0 && count > 0)
                {
                    var expectedRead = Math.Min(currentChunk.Remainder, count);
                    var actualRead = _currentChunk!.Read(buffer, offset, expectedRead);

                    currentChunk.Remainder -= actualRead; // Remove from remainder.

                    count -= actualRead; // Remove the written bytes
                    offset += actualRead; // Advance the write offset

                    _position += actualRead;
					totalReadCount += actualRead;
				}

                Debug.Assert(currentChunk.Remainder >= 0);
                if (currentChunk.Remainder == 0)
				{
					_currentChunk!.Dispose();
					_currentChunk = null;

					++_chunkIndex;
					if (_chunkIndex >= _chunks.Length)
                        return totalReadCount;

                    TryInitialize(ref _chunks.UnsafeIndex(_chunkIndex));
                }
            }

            return totalReadCount;
        }

        private long SeekCore(long offset)
        {
            if (!CanSeek)
                throw new NotSupportedException();

		    // Reset decompressed position
		    _position = 0;

            // If the seek position is in the current chunk, just skip in it.
            ref var currentChunk = ref _chunks.UnsafeIndex(_chunkIndex);
            if (currentChunk.Decompressed.Contains((int) offset))
            {
                Debug.Assert(_currentChunk != null);

                // Current chunk contains the new offset, seek to it..
                // Compute offset from current position in the chunk.
                var currentOffset = currentChunk.Decompressed.Count() - currentChunk.Remainder;
                if (offset > currentOffset) // Skip ahead
                    _currentChunk.Skip((int) (offset - currentOffset));
                else
                {
                    // Skipping behind.
                    if (!currentChunk.Flat)
                    {
                        _currentChunk?.Dispose();

                        TryInitialize(ref currentChunk);
                        _currentChunk!.Skip((int) (offset - currentChunk.Decompressed.Start.Value));
                    }
                    else
                    {
						_currentChunk.Seek((int) (offset - currentOffset), SeekOrigin.Current);
					}
                }

                currentChunk.Remainder = (int) (currentChunk.Decompressed.Count() - offset);
                _position = (int) (currentChunk.Decompressed.End.Value - offset); 

			}
            else
            {
                var chunkIndex = 0;
                for (; chunkIndex < _chunks.Length && offset > 0; ++chunkIndex)
                {
                    ref var chunkInfo = ref _chunks.UnsafeIndex(chunkIndex);

                    if (offset < chunkInfo.Decompressed.Count())
                    {
						if (chunkInfo.CompressionMode == 0)
						{
							_underlyingStream.Seek(chunkInfo.Compressed.Start.Value, SeekOrigin.Begin);
							TryInitialize(ref chunkInfo);
						}

                        _currentChunk!.Seek(offset, SeekOrigin.Begin);

						chunkInfo.Remainder = (int)(chunkInfo.Decompressed.End.Value - offset);
                        _position = (int) (chunkInfo.Decompressed.Start.Value + offset);
                        offset = 0;
					}
					else
                    {
                        // Skipping over this chunk.
                        chunkInfo.Remainder = 0;
                        offset -= chunkInfo.Decompressed.Count();
                    }
                }

                _chunkIndex = chunkIndex;
                currentChunk = ref _chunks.UnsafeIndex(chunkIndex);

                Debug.Assert(_underlyingStream.Position == currentChunk.Compressed.Start.Value);
				TryInitialize(ref currentChunk);
			}

            return _position;
		}

        private void TryInitialize(ref ChunkInfo chunkInfo)
		{
            if (chunkInfo.CompressionMode == 0)
            {
                // For some reason, this can happen ?!
                if (_underlyingStream.Position != chunkInfo.Compressed.Start.Value)
                    _underlyingStream.Seek(chunkInfo.Compressed.Start.Value, SeekOrigin.Begin);

                chunkInfo.CompressionMode = _underlyingStream.ReadUInt8();
                chunkInfo.Remainder = chunkInfo.Decompressed.Count();
            }
            else
            {
                // And so can this ???
                if (_underlyingStream.Position != chunkInfo.Compressed.Start.Value + 1)
					_underlyingStream.Seek(chunkInfo.Compressed.Start.Value + 1, SeekOrigin.Begin);
			}

			_currentChunk?.Dispose();

			var slice = _underlyingStream.ReadSlice(chunkInfo.Compressed.Count() - 1);
			_currentChunk = chunkInfo.CompressionMode switch
			{
				(byte) 'Z' => new ZLibStream(slice, CompressionMode.Decompress),
                (byte) '4' => throw new NotImplementedException("LZ4 compression is not implemented"),
                (byte) 'F' => slice.ReadBLTE(), // Untested
                (byte) 'E' => throw new NotImplementedException("Encrypted files are not implemented"),
				(byte) 'N' => slice,
                _ => throw new ArgumentOutOfRangeException(paramName: nameof(chunkInfo.CompressionMode), message: $"Invalid compression mode {chunkInfo.CompressionMode}")
			};
		}

        public override long Seek(long offset, SeekOrigin origin)
        {
            return origin switch
            {
                SeekOrigin.Begin => SeekCore(offset),
                SeekOrigin.Current => SeekCore(offset + Position),
                SeekOrigin.End => SeekCore(offset + Position),
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
        }

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        protected override void Dispose(bool disposing) {
            _currentChunk?.Dispose();
            _underlyingStream.Dispose();
        }

        private record struct ChunkInfo(Range Compressed, Range Decompressed, UInt128 Checksum, byte CompressionMode, int Remainder)
        {
            public readonly bool Flat => CompressionMode == (byte)'N';
        }
        private record struct Header(int headerSize, int flags);
    }
}
