using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using wowzer.fs.Extensions;

namespace wowzer.tests
{
    [TestClass]
    public class BLTETest
    {
        private static byte[] COMPRESSED_DATA =
        [
            0x78, 0x9c, 0x4d, 0x8f, 0x41, 0x6e, 0xc4, 0x30, 0x08, 0x45, 0xaf, 0xf2, 0x0f, 0x30, 0xca, 0x1d,
            0xaa, 0xae, 0x2a, 0xb5, 0xdb, 0xee, 0x89, 0x83, 0x22, 0x2a, 0x8c, 0x33, 0x36, 0x44, 0x3d, 0xfe,
            0xe0, 0xc9, 0x2c, 0x66, 0x67, 0xf1, 0xf0, 0xfb, 0x9f, 0xef, 0xd6, 0xb9, 0x42, 0x8e, 0x11, 0x15,
            0x5b, 0xd3, 0xd6, 0x31, 0xc4, 0x41, 0x95, 0xfd, 0x86, 0xd2, 0x6c, 0x70, 0x71, 0xf6, 0xe8, 0xa0,
            0x4d, 0x0e, 0x19, 0x45, 0x6c, 0x07, 0xab, 0xf8, 0x82, 0x2f, 0x7b, 0xfd, 0xaa, 0x72, 0xc3, 0x11,
            0x7a, 0x8a, 0x51, 0x87, 0x35, 0xc3, 0x2a, 0x2b, 0xdb, 0x96, 0x64, 0x3a, 0x9c, 0xeb, 0x11, 0x03,
            0xc6, 0x05, 0x83, 0x0e, 0x61, 0x5b, 0xf0, 0xd9, 0x69, 0x20, 0xd4, 0xbb, 0x14, 0xe1, 0x01, 0x17,
            0x2b, 0xb2, 0x85, 0xf9, 0x6b, 0x01, 0x94, 0xaf, 0xc2, 0xca, 0x5d, 0xc6, 0x3d, 0x78, 0xc1, 0x87,
            0xca, 0x3d, 0x28, 0x4b, 0x9a, 0x73, 0x4f, 0xef, 0x8c, 0xeb, 0xd3, 0x99, 0x59, 0x7b, 0xa7, 0x53,
            0x36, 0xc2, 0xc9, 0xc3, 0x65, 0x0d, 0x9d, 0xf4, 0xaa, 0x65, 0xa1, 0x4a, 0xe0, 0x3d, 0xc1, 0x8c,
            0xeb, 0x46, 0x49, 0xec, 0x2d, 0xed, 0x92, 0xfc, 0xc5, 0xf0, 0x96, 0x4d, 0xd3, 0xf0, 0x3c, 0xeb,
            0x57, 0x4e, 0xaa, 0x39, 0xaf, 0x4d, 0x55, 0x32, 0x23, 0xac, 0x80, 0xca, 0x45, 0xf3, 0x72, 0xae,
            0x6c, 0x3e, 0x8f, 0xa6, 0x7f, 0xc9, 0xb5, 0x05, 0x3f, 0x14, 0xd9, 0xf3, 0x4d, 0x5b, 0x9f, 0x83,
            0x05, 0x0f, 0x29, 0xbf, 0x7f, 0xd7
        ];
        private static byte[] DECOMPRESSED_DATA = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. In ipsum mi, pulvinar non bibendum et, tempus nec sapien. Cras ultricies tincidunt sapien at scelerisque. Aliquam interdum, purus non gravida vestibulum, ipsum nulla egestas urna, in tincidunt purus justo et velit. Vivamus mollis nunc ac velit elementum maximus. Mauris tincidunt mauris. "u8.ToArray();

        private static MemoryStream GenerateBLTE()
        {
            var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.ASCII, true);
            writer.Write(0x45544C42);

            writer.Write(0); // Header Size - BE4
            writer.Write(0x020000ff); // Chunk Count - BE3 & Flags

            { // First chunk
                writer.Write(0x02000000); // Compressed Size - BE4
                writer.Write(0x01000000); // Decompressed Size - BE4
                 // Checksum
                writer.Write(0x00000000);
                writer.Write(0x00000000);
                writer.Write(0x00000000);
                writer.Write(0x00000000);
            }

            { // Second chunk
                writer.Write(BinaryPrimitives.ReverseEndianness(COMPRESSED_DATA.Length + 1));
                writer.Write(BinaryPrimitives.ReverseEndianness(DECOMPRESSED_DATA.Length));
                // Checksum
                writer.Write(0x00000000);
                writer.Write(0x00000000);
                writer.Write(0x00000000);
                writer.Write(0x00000000);
            }

            { // First chunk
                writer.Write((byte) 'N');
                writer.Write((byte) 0xCC);
            }

            { // Second chunk
                writer.Write((byte) 'Z');
                writer.Write(COMPRESSED_DATA);
            }

            writer.Flush();

            ms.Position = 0;
            return ms;
        }

        [TestMethod]
        public void BufferedBLTE()
        {
            using var source = GenerateBLTE();

            using var decompressed = new MemoryStream();
            using (var blte = source.ReadBLTE())
                blte.CopyTo(decompressed);

            decompressed.Position = 0;

            Assert.AreEqual(0xCC, decompressed.ReadUInt8());

            var str = decompressed.ReadUInt8(DECOMPRESSED_DATA.Length);
            Assert.That.AreEqual(DECOMPRESSED_DATA, str, (left, right) => left.SequenceEqual(right));

            decompressed.Position = 0;
            Console.WriteLine(string.Join(", ", decompressed.GetBuffer()));
        }

        [TestMethod]
        public void MemoryBLTE()
        {
            using var source = GenerateBLTE();

            using var decompressed = new MemoryStream();
            using (var blte = source.ReadMemoryBLTE())
                blte.CopyTo(decompressed);

            decompressed.Position = 0;

            Assert.AreEqual(0xCC, decompressed.ReadUInt8());

            var str = decompressed.ReadUInt8(DECOMPRESSED_DATA.Length);
            Assert.That.AreEqual(DECOMPRESSED_DATA, str, (left, right) => left.SequenceEqual(right));

            decompressed.Position = 0;
            Console.WriteLine(string.Join(", ", decompressed.GetBuffer()));
        }
    }
}
