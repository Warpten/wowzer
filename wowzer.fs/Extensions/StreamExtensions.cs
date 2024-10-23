using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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
                var bufferSize = Math.Min(2048, (count + 1) & -2);
                Span<byte> buffer = GC.AllocateUninitializedArray<byte>(bufferSize);
                for (var i = 0; i < count / bufferSize; ++i)
                    stream.ReadExactly(buffer);
                stream.ReadExactly(buffer[..(count % bufferSize)]);
            }
        }
    }
}
