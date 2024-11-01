using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wowzer.fs.CASC
{
    public class FileSystem
    {
        public FileSystem(string path, string buildCfgPath, string cdnCfgPath)
        {
            using var bldCfg = File.OpenRead($"{path}/Data/config/{buildCfgPath}");
            var buildConfig = new Configuration(bldCfg);

            using var cdnCfg = File.OpenRead($"{path}/Data/config/{cdnCfgPath}");
            var cdnConfig = new Configuration(cdnCfg);

            var indices = Directory.EnumerateFiles($"{path}/Data/data/")
                .Where(file => file.EndsWith(".idx"))
                .Select(Index.FromFile)
                .ToList();

            var encodingSpec = buildConfig["encoding"].As(ContentKey.From, EncodingKey.From);
        }
    }
}
