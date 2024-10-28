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
            writer.WriteLine("patch-file-index = a246f192dbe5b39cf170f156cf883d11");
            writer.WriteLine("patch-file-index-size = 1145388");

            ms.Position = 0;

            using var reader = new StreamReader(ms);
            var config = new Configuration(ms);
        }
    }
}
