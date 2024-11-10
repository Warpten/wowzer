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
            for (var i = 0; i < chunkCount; ++i)
            {
                var compressedSize = _underlyingStream.ReadInt32BE();
                var decompressedSize = _underlyingStream.ReadInt32BE();

                var checksum = _underlyingStream.ReadUInt128BE();
                _chunks[i] = new(compressedSize - 1, decompressedSize, checksum, 0, decompressedSize);
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
            var totalReadCount = 0;
            while (count > 0) {
                ref var currentChunk = ref _chunks.UnsafeIndex(_chunkIndex);

                if (currentChunk.CompressionMode == 0)
                    Initialize(ref currentChunk);

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

                if (currentChunk.Remainder == 0)
                {
                    if (_chunkIndex + 1 >= _chunks.Length)
                        return totalReadCount;

                    _currentChunk!.Dispose();
                    _currentChunk = null;
                    ++_chunkIndex;
                }
            }

            return totalReadCount;
        }

        // Dear future me: see the comments on SeekCore. This function is broken somewhere.
        private void SeekCore2(long decompressedOffset)
        {
            _position = (int) decompressedOffset;

            var seekOffset = _header.headerSize;
            var seekOrigin = SeekOrigin.Begin;

            for (var i = 0; i < _chunks.Length && decompressedOffset > 0; ++i)
            {
                ref var currentChunk = ref _chunks.UnsafeIndex(i);

                if (currentChunk.CompressionMode == 0)
                {
                    _underlyingStream.Seek(seekOffset, seekOrigin);
                    Initialize(ref currentChunk, true);

                    seekOffset = 0;
                    seekOrigin = SeekOrigin.Current;
                }
                else
                {
                    seekOffset += 1;
                }

                switch (currentChunk.CompressionMode)
                {
                    case (byte)'N':
                        {
                            var consumableInChunk = (int)Math.Min(decompressedOffset, currentChunk.DecompressedSize);
                            seekOffset += consumableInChunk;

                            currentChunk.Remainder = currentChunk.DecompressedSize - consumableInChunk;
                            decompressedOffset -= consumableInChunk;
                            break;
                        }
                    case (byte)'Z':
                        {
                            // Encountered Z chunk.
                            if (decompressedOffset >= currentChunk.DecompressedSize)
                            {
                                // Skip right past it
                                seekOffset += currentChunk.CompressedSize;
                                decompressedOffset -= currentChunk.DecompressedSize;
                            }
                            else
                            {
                                // We're in the middle of it.
                                _underlyingStream.Seek(seekOffset, seekOrigin);

                                var consumableInChunk = (int)Math.Min(decompressedOffset, currentChunk.DecompressedSize);
                                _currentChunk = new ZLibStream(_underlyingStream.ReadSlice(currentChunk.CompressedSize), CompressionMode.Decompress);
                                _currentChunk.Skip(consumableInChunk);

                                currentChunk.Remainder = currentChunk.DecompressedSize - consumableInChunk;
                                decompressedOffset -= consumableInChunk;

                                _chunkIndex = i;
                                return;
                            }

                            break;
                        }
                    default:
                        throw new NotImplementedException();
                }

                if (currentChunk.Remainder == 0)
                {
                    _currentChunk?.Dispose();
                    _chunkIndex = i + 1;
                }
            }

            _underlyingStream.Seek(seekOffset, seekOrigin);
        }

        // Try to optimize the number of Win32 API calls if you can be bothered - past you can't. -Warpten.
        // Ideally every seek operation (calls to Skip) should be buffered until 'Z' is hit, at which point
        // either:
        // 1. All the chunk is skipped, so just increment by CompressedSize + 1
        // 2. Part of the chunk is skipped, so skip to the start of the chunk, decompress whatever amount is needed, and exit.
        // 3. Very optional - in case of nested BLTEs, buffer further?
        private long SeekCore(long decompressedOffset)
        {
            if (!CanSeek)
                throw new NotSupportedException();

            _position = 0;

            // Broken if this is not commented but prevents seeking backwards if commented
            // _underlyingStream.Seek(_header.headerSize, SeekOrigin.Begin);

            for (var i = 0; i < _chunks.Length && decompressedOffset > 0; ++i)
            {
                ref var currentChunk = ref _chunks.UnsafeIndex(i);
                if (currentChunk.Remainder == 0)
                {
                    decompressedOffset -= currentChunk.DecompressedSize;
                    _position += currentChunk.DecompressedSize;

                    // Broken if this is not commented but prevents seeking backwards if commented
                    // _underlyingStream.Skip(currentChunk.CompressedSize + 1);
                }
                else
                {
                    if (currentChunk.CompressionMode == 0)
                        Initialize(ref currentChunk);

                    switch (currentChunk.CompressionMode)
                    {
                        case (byte)'N':
                        case (byte)'Z':
                            {
                                Debug.Assert(_currentChunk != null);

                                var consumableInChunk = (int) Math.Min(decompressedOffset, currentChunk.DecompressedSize);

                                var previousRemainder = currentChunk.Remainder;
                                currentChunk.Remainder = currentChunk.DecompressedSize - consumableInChunk;

                                _currentChunk!.Skip(previousRemainder - currentChunk.Remainder);
                                if (currentChunk.Remainder == 0) {
                                    _currentChunk!.Dispose();
                                    _currentChunk = null;
                                }

                                decompressedOffset -= consumableInChunk;
                                _position += consumableInChunk;
                                break;
                            }
                        default:
                            throw new NotImplementedException();
                    }

                    if (currentChunk.Remainder == 0)
                    {
                        _chunkIndex = i + 1;

                        if (i + 1 >= _chunks.Length)
                            return Position;

                    }
                }
            }

            return Position;
        }

        private void Initialize(ref ChunkInfo chunkInfo, bool lazy = false)
        {
            var compressionMode = _underlyingStream.ReadUInt8();
            Debug.Assert(compressionMode != 0);

            chunkInfo.CompressionMode = compressionMode;
            chunkInfo.Remainder = chunkInfo.DecompressedSize;

            if (lazy)
                return;

            _currentChunk = compressionMode switch
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

        private record struct ChunkInfo(int CompressedSize, int DecompressedSize, UInt128 Checksum, byte CompressionMode, int Remainder);
        private record struct Header(int headerSize, int flags);
    }
}
