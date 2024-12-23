﻿using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using wowzer.fs.IO;
using wowzer.fs.Utils;

namespace wowzer.fs.Extensions
{
    public static class StreamExtensions
    {
        [SkipLocalsInit]
        public static unsafe uint ReadFourCC(this Stream stream)
        {
            var value = 0u;
            stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1)));
            return value;
        }

        public static byte ReadUInt8(this Stream stream) => (byte) stream.ReadByte();
        public static sbyte ReadInt8(this Stream stream) => (sbyte) stream.ReadByte();

        public static byte[] ReadUInt8(this Stream stream, int length)
        {
            var buffer = GC.AllocateUninitializedArray<byte>(length);
            stream.ReadExactly(buffer);
            return buffer;
        }

        public static sbyte[] ReadInt8(this Stream stream, int length)
        {
            var buffer = GC.AllocateUninitializedArray<sbyte>(length);
            stream.ReadExactly(MemoryMarshal.AsBytes(buffer.AsSpan()));
            return buffer;
        }

        [SkipLocalsInit]
        public static void Skip(this Stream stream, int count)
        {
            // TODO: Optimize this for non-seekable strems when skipping small sizes.

            if (stream.CanSeek)
                stream.Seek(count, SeekOrigin.Current);
            else
            {
                // TODO: Avoid this for paths where count < nuint
                var bufferSize = Math.Min(2048, (count + 1) & -2);
                Span<byte> buffer = GC.AllocateUninitializedArray<byte>(bufferSize);
                for (var i = 0; i < count / bufferSize; ++i)
                    stream.ReadExactly(buffer);
                stream.ReadExactly(buffer[..(count % bufferSize)]);
            }
        }

        public static ReadOnlySpan<byte> ReadExactly(this Stream stream, int count)
        {
            var buffer = GC.AllocateUninitializedArray<byte>(count);
            stream.ReadExactly(buffer);
            return buffer;
        }

        public static BLTE<T> ReadBLTE<T>(this T dataStream) where T : Stream => new BLTE<T>(dataStream, dataStream.Length);
        public static BLTE<T> ReadBLTE<T>(this T dataStream, long length) where T : Stream => new BLTE<T>(dataStream, length);

        [SkipLocalsInit]
        public static MemoryStream ReadMemoryBLTE(this Stream dataStream)
        {
            var magic = dataStream.ReadUInt32LE();
            Debug.Assert(magic == 0x45544C42);

            var headerSize = dataStream.ReadUInt32BE();
            var chunkCount = dataStream.ReadUInt32BE();
            var flags = chunkCount >> 24;
            chunkCount &= 0xFFFFFF;

            var chunkInfo = GC.AllocateUninitializedArray<ChunkInfo>((int) chunkCount);
            for (var i = 0; i < chunkCount; ++i)
            {
                var compressedSize = dataStream.ReadInt32BE();
                var decompressedSize = dataStream.ReadInt32BE();

                var checksum = dataStream.ReadUInt128BE();
                chunkInfo[i] = new(compressedSize - 1, decompressedSize, checksum);
            }

            var allocationSize = chunkInfo.Sum(ci => ci.DecompressedSize);

            var dst = GC.AllocateUninitializedArray<byte>(allocationSize);
            var writePos = 0;

            foreach (var chunk in chunkInfo) {
                var encodingMode = dataStream.ReadUInt8();
                switch (encodingMode)
                {
                    case (byte) 'N':
                        dataStream.ReadExactly(dst.AsSpan().Slice(writePos, chunk.CompressedSize));
                        writePos += chunk.CompressedSize;
                        break;
                    case (byte) 'Z':
                        using (var compression = new ZLibStream(dataStream.ReadSlice(chunk.CompressedSize), CompressionMode.Decompress, true))
                        using (var target = new UnsafeSpanStream(dst.AsSpan().Slice(writePos, chunk.DecompressedSize)))
                            compression.CopyTo(target);

                        writePos += chunk.DecompressedSize;
                        break;
                    default:
                        throw new NotImplementedException();
                }

                Debug.Assert(writePos <= allocationSize);
            }

            return new MemoryStream(dst);
        }

        private record struct ChunkInfo(int CompressedSize, int DecompressedSize, UInt128 Checksum);

        public static LimitedStream<T> ReadSlice<T>(this T stream, long length) where T : Stream
            => new LimitedStream<T>(stream, length);
    }
}
