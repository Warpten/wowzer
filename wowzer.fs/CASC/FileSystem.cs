using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            using var bldCfg = OpenFile(path, "config", buildCfgPath);
            var buildConfig = new Configuration(bldCfg);

            using var cdnCfg = OpenFile(path, "config", cdnCfgPath);
            var cdnConfig = new Configuration(cdnCfg);

            var indices = new List<Index>();
            foreach (var dataFile in Directory.EnumerateFiles($"{path}/Data/data/")) {
                if (!dataFile.EndsWith(".idx"))
                    continue;

                using var fileStream = File.OpenRead(dataFile);
                indices.Add(new Index(fileStream));
            }

            (ContentKey ContentKey, EncodingKey EncodingKey) encodingSpec = buildConfig["encoding"].As(ContentKey.From, EncodingKey.From);

            FindEncodingKey(indices, encodingSpec.EncodingKey);
        }

        private static FileStream OpenFile(string rootDirectory, string subdirectory, string hash)
            => File.OpenRead($"{rootDirectory}/Data/{subdirectory}/{hash.AsSpan()[0..2]}/{hash.AsSpan()[2..4]}/{hash}");

        private static void FindEncodingKey<T>(List<Index> indices, T encodingKey) where T : IKey<T>, IEncodingKey
        {
            Debug.Assert(encodingKey.Length >= 9);

            // Find the appropriate index to look into.
            var bucketIndex = encodingKey[1];
            for (var i = 1; i < 9; ++i)
                bucketIndex ^= encodingKey[i];
            bucketIndex = (byte)((bucketIndex & 0xF) ^ (bucketIndex >> 4));

            var index = indices[bucketIndex];

            // Adjust the provided encoding key to the amount of bytes known to the index.
            var lookup = encodingKey.AsSpan()[index.Spec.Key];

            var lowerBound = index.BinarySearchBy((entry, lookup) => {
                var comparison = lookup.SequenceCompareTo(entry.Key);
                if (comparison == 0)
                    return 1;

                return comparison;
            }, lookup);

            var upperBound = index.BinarySearchBy((entry, lookup) => {
                var comparison = lookup.SequenceCompareTo(entry.Key);
                if (comparison == 0)
                    return -1;

                return comparison;
            }, lookup);
        }
    }
}
