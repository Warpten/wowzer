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
            Span<byte> rawData = stackalloc byte[4];
            stream.ReadExactly(rawData);

            return MemoryMarshal.Read<uint>(rawData);
        }

        public static byte ReadUInt8(this Stream stream) => (byte) stream.ReadByte();

        public static byte[] ReadUInt8(this Stream stream, int length)
        {
            var buffer = GC.AllocateUninitializedArray<byte>(length);
            stream.ReadExactly(buffer);
            return buffer;
        }

        [SkipLocalsInit]
        public static void Skip(this Stream stream, int count, int bufferSize = 128)
        {
            // TODO: Optimize this for non-seekable strems when skipping small sizes.

            if (stream.CanSeek)
                stream.Seek(count, SeekOrigin.Current);
            else
            {
                Span<byte> buffer = new byte[bufferSize];
                for (var i = 0; i < count / bufferSize; ++i)
                    stream.ReadExactly(buffer);
                stream.ReadExactly(buffer[..(count % bufferSize)]);
            }
        }
    }
}
