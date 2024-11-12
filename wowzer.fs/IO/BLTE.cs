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
        private readonly Header _header;

        public BLTE(T sourceStream, long archiveSize)
        {
            _underlyingStream = sourceStream.ReadSlice(archiveSize);

            var magic = _underlyingStream.ReadUInt32LE();
            Debug.Assert(magic == 0x45544C42);

            var headerSize = _underlyingStream.ReadInt32BE();
            var chunkCount = _underlyingStream.ReadInt32BE();
            var flags = chunkCount >> 24;
            chunkCount &= 0xFFFFFF;

            _header = new Header(headerSize, flags);
            _chunks = GC.AllocateUninitializedArray<ChunkInfo>(chunkCount);

            var chunkOffset = headerSize;
            for (var i = 0; i < chunkCount; ++i)
            {
                var compressedSize = _underlyingStream.ReadInt32BE();
                var decompressedSize = _underlyingStream.ReadInt32BE();

                var checksum = _underlyingStream.ReadUInt128BE();
                _chunks[i] = new(compressedSize, decompressedSize, checksum, chunkOffset, 0, decompressedSize);

                chunkOffset += compressedSize;
            }
        }

        public override bool CanRead { get; } = true;
        public override bool CanWrite { get; } = false;
        public override bool CanSeek => _underlyingStream.CanSeek;

        public override long Length => _chunks.Sum(chunk => chunk.DecompressedSize);

        public override long Position {
            get => _position;
            set => SeekCore(value);
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
#if _DEBUG
            Debug.Assert(_chunkIndex < _chunks.Length);
#else
            if (_chunkIndex >= _chunks.Length)
                return 0;
#endif

            var totalReadCount = 0;
            while (count > 0) {
                ref var currentChunk = ref _chunks.UnsafeIndex(_chunkIndex);

                TryInitialize(ref currentChunk, true);

                switch (currentChunk.CompressionMode) {
                    case (byte) 'N':
                    case (byte) 'Z':
                        {
                            Debug.Assert(_currentChunk != null, "How did we get here?");

                            var expectedRead = Math.Min(currentChunk.Remainder, count);
                            var actualRead = _currentChunk!.Read(buffer, offset, expectedRead);

                            currentChunk.Remainder -= actualRead; // Remove from remainder.

                            totalReadCount += actualRead;

                            count -= actualRead; // Remove the written bytes
                            offset += actualRead; // Advance the write offset

                            _position += actualRead;
                            break;
                        }
                    default:
                        throw new NotImplementedException();
                }

                // Chunk is done, seek to the next chunk.
                if (currentChunk.Remainder == 0)
                {
                    if (_chunkIndex + 1 >= _chunks.Length)
                        return totalReadCount;

                    _currentChunk?.Dispose();
                    _currentChunk = null;

                    ++_chunkIndex;
                }
            }

            return totalReadCount;
        }

        private long SeekCore(long decompressedOffset)
        {
            if (!CanSeek)
                throw new NotSupportedException();

            _position = 0;

            var seekOffset = 0L;

            for (var i = 0; i < _chunks.Length && decompressedOffset > 0; ++i)
            {
                ref var currentChunk = ref _chunks.UnsafeIndex(i);
                TryInitialize(ref currentChunk, false);

                if (decompressedOffset >= currentChunk.DecompressedSize)
                {
                    // Skipping straight over this chunk
                    currentChunk.Remainder = 0;

                    decompressedOffset -= currentChunk.DecompressedSize;
                    _position += currentChunk.DecompressedSize;

                    _currentChunk?.Dispose();
                    _currentChunk = null;

                    // Prepare for next chunk.
                    _chunkIndex = i + 1;

                    seekOffset = currentChunk.Offset + currentChunk.CompressedSize;
                }
                else
                {
                    // Current chunk compression is unknown, seek to it now and read it because
                    // we need special processing for Z chunks.
                    if (currentChunk.CompressionMode == 0)
                    {
                        _underlyingStream.Seek(currentChunk.Offset, SeekOrigin.Begin);

                        TryInitialize(ref currentChunk, true);
                    }

                    currentChunk.Remainder = (int) (currentChunk.DecompressedSize - decompressedOffset);
                    _position += (int) decompressedOffset;

                    _chunkIndex = i;

                    if (currentChunk.CompressionMode == (byte)'Z')
                    {
                        // Special case for zlibbed chunks; we are stopping in the middle
                        // but we still need to parse (and discard) the N bytes leading up
                        // to the stop point.

                        // Seek to the start of the chunk's compressed data.
                        _underlyingStream.Seek(currentChunk.Offset + 1, SeekOrigin.Begin);
                        TryInitialize(ref currentChunk, true);

                        // Skip the amount of consumable bytes
                        _currentChunk!.Skip((int) decompressedOffset);

                        return _position;
                    }
                    else if (currentChunk.CompressionMode == (byte)'N')
                    {
                        // Skip to the actual offset within the chunk.
                        seekOffset = currentChunk.Offset + 1 + decompressedOffset;
                        currentChunk.Remainder = (int)(currentChunk.DecompressedSize - decompressedOffset);
                        decompressedOffset = 0;
                    }
                    else 
                        throw new UnreachableException();
                }
            }

            _underlyingStream.Seek(seekOffset, SeekOrigin.Begin);
            TryInitialize(ref _chunks.UnsafeIndex(_chunkIndex), true);
            return _position;
        }

        private void TryInitialize(ref ChunkInfo chunkInfo, bool initializeChunkStream)
        {
            // If compression is unknown, read it.
            if (chunkInfo.CompressionMode == 0) {
                Debug.Assert(_underlyingStream.Position == chunkInfo.Offset);

                var compressionMode = _underlyingStream.ReadUInt8();
                Debug.Assert(compressionMode != 0);

                chunkInfo.Remainder = chunkInfo.DecompressedSize;
                chunkInfo.CompressionMode = compressionMode;
            }

            if (initializeChunkStream && chunkInfo.Remainder == chunkInfo.DecompressedSize) {
                Debug.Assert(_underlyingStream.Position == chunkInfo.Offset + 1);

                _currentChunk = chunkInfo.CompressionMode switch {
                    (byte)'Z' => new ZLibStream(_underlyingStream.ReadSlice(chunkInfo.CompressedSize - 1), CompressionMode.Decompress),
                    _ => _underlyingStream.ReadSlice(chunkInfo.Remainder)
                };
            }
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

        protected override void Dispose(bool disposing)
            => _underlyingStream.Dispose();

        private record struct ChunkInfo(int CompressedSize, int DecompressedSize, UInt128 Checksum, long Offset, byte CompressionMode, int Remainder);
        private record struct Header(int headerSize, int flags);
    }
}
