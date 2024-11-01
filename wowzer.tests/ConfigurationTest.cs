using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using wowzer.fs.CASC;
using wowzer.tests;

namespace wowzer.fs.tests
{
    [TestClass]
    public class ConfigurationTest
    {
        [TestMethod] public void TestConfigurationParser()
        {
            using var ms = new MemoryStream();
            using var writer = new StreamWriter(ms);

            writer.WriteLine("# CDN Configuration");
            writer.WriteLine();
            writer.WriteLine("archives = 0017a402f556fbece46c38dc431a2c9b 0025060801813b796c787a777bdbfcf9");
            writer.WriteLine("file-index = ad4b6d5659c1c016f43c1cad4675b8ce");
            writer.WriteLine("file-index-size = 918788");
            writer.WriteLine("root = a246f192dbe5b39cf170f156cf883d11");
            writer.Flush();

            ms.Position = 0;

            var config = new Configuration(ms);

            // Test root (ckey)
            Assert.That.AreEqual(
                ContentKey.From([0xa2, 0x46, 0xf1, 0x92, 0xdb, 0xe5, 0xb3, 0x9c, 0xf1, 0x70, 0xf1, 0x56, 0xcf, 0x88, 0x3d, 0x11]),
                config["root"].AsContentKey(),
                (left, right) => left.Equals(right)
            );

            // Test archives (ekeys)
            Assert.That.AreEqual(
                [
                    EncodingKey.From([0x00, 0x17, 0xa4, 0x02, 0xf5, 0x56, 0xfb, 0xec, 0xe4, 0x6c, 0x38, 0xdc, 0x43, 0x1a, 0x2c, 0x9b]),
                    EncodingKey.From([0x00, 0x25, 0x06, 0x08, 0x01, 0x81, 0x3b, 0x79, 0x6c, 0x78, 0x7a, 0x77, 0x7b, 0xdb, 0xfc, 0xf9])
                ],
                config["archives"].AsEncodingKeys(),
                (left, right) => left.SequenceEqual(right)
            );
        }
    }
}
