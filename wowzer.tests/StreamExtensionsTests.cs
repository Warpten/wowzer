using System.Text;

using wowzer.fs.Extensions;

namespace wowzer.fs.tests
{
    [TestClass]
    public class StreamExtensionsTests
    {
        [TestMethod] public void TestEndianness()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(0x11223344u);
            writer.Flush();

            stream.Position = 0;

            var value = stream.ReadUInt32BE();
            Assert.AreEqual(0x44332211u, value);
        }

        [TestMethod] public void TestVectorizedEndianness()
        {
            Span<uint> span =
            [
                0x11223344u, 0x11223344u, 0x11223344u, 0x11223344u,
                0x11223344u, 0x11223344u, 0x11223344u, 0x11223344u,
                0x11223344u, 0x11223344u, 0x11223344u, 0x11223344u,
                0x11223344u, 0x11223344u, 0x11223344u, 0x11223344u,
                0x11223344u, 0x11223344u, 0x11223344u, 0x11223344u,
                0x11223344u
            ];

            SpanExtensions.ReverseEndianness(span);
            for (var i = 0; i < span.Length; ++i)
                Assert.AreEqual(0x44332211u, span[i]);
        }
    }
}