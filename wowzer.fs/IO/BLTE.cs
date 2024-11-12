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
                _chunks[i] = new(compressedSize - 1, decompressedSize, checksum, chunkOffset, 0, decompressedSize);

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

        public override int Read(Span<byte> buffer)
        {
            var totalReadCount = 0;
            var offset = 0;
            var count = buffer.Length;

            while (count > 0)
            {
                ref var currentChunk = ref _chunks.UnsafeIndex(_chunkIndex);
                if (currentChunk.CompressionMode == 0)
                    Initialize(ref currentChunk);

                switch (currentChunk.CompressionMode)
                {
                    case (byte)'N':
                    case (byte)'Z':
                        {
                            Debug.Assert(_currentChunk != null);

                            while (currentChunk.Remainder > 0 && count > 0)
                            {
                                var expectedRead = Math.Min(currentChunk.Remainder, count);
                                var actualRead = _currentChunk!.Read(buffer.Slice(offset, expectedRead));

                                currentChunk.Remainder -= actualRead; // Remove from remainder.
                                totalReadCount += actualRead;

                                count -= actualRead; // Remove the written bytes
                                offset += actualRead; // Advance the write offset

                                _position += actualRead;
                            }
                            break;
                        }
                    default:
                        throw new NotImplementedException();
                }

                if (currentChunk.Remainder == 0)
                {
                    if (_chunkIndex + 1 >= _chunks.Length)
                        return totalReadCount;

                    _currentChunk!.Dispose();
                    _currentChunk = null;
                    ++_chunkIndex;
                }
            }

            Debug.Assert(count == 0);

            return totalReadCount;
        }

        public override int Read(byte[] buffer, int offset, int count)
            => Read(buffer.AsSpan().Slice(offset, count));

        private long SeekCore(long decompressedOffset)
        {
            if (!CanSeek)
                throw new NotSupportedException();

            long compressedPosition = _header.headerSize;
            long decompressedPosition = 0;

            _currentChunk?.Dispose();
            _currentChunk = null;

            for (var i = 0; i < _chunks.Length && decompressedOffset > 0; ++i)
            {
                ref var currentChunk = ref _chunks.UnsafeIndex(i);

                if (decompressedOffset >= currentChunk.DecompressedSize)
                {
                    // Skipping straight over this chunk
                    currentChunk.Remainder = 0;

                    decompressedOffset -= currentChunk.DecompressedSize;
                    decompressedPosition += currentChunk.DecompressedSize;

                    compressedPosition += currentChunk.CompressedSize + 1;

                    // Prepare for next chunk.
                    _chunkIndex = i + 1;
                }
                else
                {
                    // Current chunk compression is unknown, seek to it now and read it because
                    // we need special processing for Z chunks.
                    if (currentChunk.CompressionMode == 0)
                    {
                        // Lazily initialize the chunk - don't prepare a chunk stream.
                        _underlyingStream.Seek(currentChunk.Offset, SeekOrigin.Begin);
                        Initialize(ref currentChunk, true);
                    }

                    // Mark this chunk as current.
                    _chunkIndex = i;

                    if (currentChunk.CompressionMode == (byte) 'Z')
                    {
                        // Special case for zlibbed chunks; we are stopping in the middle
                        // but we still need to parse (and discard) the N bytes leading up
                        // to the stop point.

                        // Seek to the start of the chunk's compressed data (note: past the compression mode byte!)
                        _underlyingStream.Seek(compressedPosition + 1, SeekOrigin.Begin);

                        // Manually initialize the chunk stream.
                        _currentChunk = new ZLibStream(_underlyingStream.ReadSlice(currentChunk.CompressedSize), CompressionMode.Decompress, true);
                        _currentChunk.Skip((int) decompressedOffset);

                        // Set remainder properly
                        currentChunk.Remainder = (int) (currentChunk.DecompressedSize - decompressedOffset);

                        // Update decompressed cursor.
                        _position = (int) (decompressedPosition + decompressedOffset);

                        // Done.
                        return _position;
                    }
                    else if (currentChunk.CompressionMode == (byte)'N')
                    {
                        // Compression and decompression have identical meaning here.

                        // Seek to an offset within the chunk, so add 1 for the compression mode byte, and the remainder.
                        compressedPosition += decompressedOffset + 1;

                        _underlyingStream.Seek(compressedPosition, SeekOrigin.Begin);

                        // Set remainder properly
                        currentChunk.Remainder = (int)(currentChunk.DecompressedSize - decompressedOffset);

                        // Manually initialize the chunk stream.
                        _currentChunk = _underlyingStream.ReadSlice(currentChunk.Remainder);

                        // Update decompressed cursor.
                        _position = (int) (decompressedPosition + decompressedOffset);

                        // Done.
                        return _position;
                    }

                    // Unreachable
                    throw new InvalidOperationException("unreachable");
                }
            }

            // If we got here we basically skipped to the start of a new chunk.
            // Initialize it (I think?)
            _underlyingStream.Seek(compressedPosition, SeekOrigin.Begin);
            _position = (int) decompressedOffset;

            if (_chunkIndex < _chunks.Length)
                Initialize(ref _chunks.UnsafeIndex(_chunkIndex), false);

            return _position;
        }

        private void Initialize(ref ChunkInfo chunkInfo, bool lazy = false)
        {
            if (_underlyingStream.Position != chunkInfo.Offset)
            {
                Console.Error.WriteLine($"Incorrect offset in BLTE: expected {chunkInfo.Offset}, found {_underlyingStream.Position}");
                _underlyingStream.Seek(chunkInfo.Offset, SeekOrigin.Begin);
            }

            if (chunkInfo.CompressionMode == 0)
            {
                var compressionMode = _underlyingStream.ReadUInt8();
                Debug.Assert(compressionMode != 0);

                chunkInfo.CompressionMode = compressionMode;
                chunkInfo.Remainder = chunkInfo.DecompressedSize;
            }

            if (lazy)
                return;

            _currentChunk = chunkInfo.CompressionMode switch
            {
                (byte)'Z' => new ZLibStream(_underlyingStream.ReadSlice(chunkInfo.CompressedSize), CompressionMode.Decompress),
                _ => _underlyingStream.ReadSlice(chunkInfo.CompressedSize)
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

        protected override void Dispose(bool disposing)
            => _underlyingStream.Dispose();

        private record struct ChunkInfo(int CompressedSize, int DecompressedSize, UInt128 Checksum, long Offset, byte CompressionMode, int Remainder);
        private record struct Header(int headerSize, int flags);
    }
}
