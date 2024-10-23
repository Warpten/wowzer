using System.Text;

using wowzer.fs.Extensions;

namespace wowzer.fs.tests
{
    [TestFixture]
    public class StreamExtensionsTests
    {
        [Test]
        public void TestEndianness()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(0x11223344u);
            writer.Flush();

            stream.Position = 0;

            var value = stream.ReadUInt32BE();
            Assert.That(0x44332211u == value);
        }

        [Test]
        public void TestVectorizedEndianness()
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
                Assert.That(0x44332211u == span[i]);
        }
    }
}