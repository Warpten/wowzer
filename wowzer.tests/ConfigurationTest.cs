using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using wowzer.fs.CASC;

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

            var rootContentKey = default(IContentKey);
            _ = RootSpec.TryParse(config, ref rootContentKey);
            Assert.AreEqual(IContentKey.From([0xa2, 0x46, 0xf1, 0x92, 0xdb, 0xe5, 0xb3, 0x9c, 0xf1, 0x70, 0xf1, 0x56, 0xcf, 0x88, 0x3d, 0x11]), rootContentKey);
        }
    }
}
