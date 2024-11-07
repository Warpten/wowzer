using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using wowzer.fs.Extensions;

namespace wowzer.fs.CASC
{
    public class FileSystem
    {
        private readonly string _dataPath;
        private readonly List<Index> _indices = []; // Get rid of the list and use an array later
        private readonly Encoding _encoding = default;
        private Root _root = default;

        public FileSystem(string path, string buildCfgPath, string cdnCfgPath)
        {
            _dataPath = path;

            // 1. Load configuration files
            using var bldCfg = FromDisk(path, "config", buildCfgPath);
            var buildConfig = new Configuration(bldCfg);

            using var cdnCfg = FromDisk(path, "config", cdnCfgPath);
            var cdnConfig = new Configuration(cdnCfg);

            // 2. Load indices
            foreach (var dataFile in Directory.EnumerateFiles($"{path}/Data/data/")) {
                if (!dataFile.EndsWith(".idx"))
                    continue;

                using var fileStream = File.OpenRead(dataFile);
                _indices.Add(new Index(fileStream));
            }
            _indices.Sort((left, right) => left.Bucket.CompareTo(right.Bucket));

            // 3. Load encoding
            var (_, encodingKey) = buildConfig["encoding"].As(ContentKey.From, EncodingKey.From);

            foreach (var encodingIndex in FindEncodingKey(encodingKey)) {
                var encodingStream = FromArchive(encodingIndex);
                try {
                    _encoding = new Encoding(encodingStream, Encoding.LoadFlags.Content);
                    break;
                } catch (EndOfStreamException) {
                    // Possibly log this.
                }
            }

            // 4. Load root
            var rootKey = buildConfig["root"].As(ContentKey.From);
            foreach (var rootIndex in FindContentKey(rootKey)) {
                var rootStream = FromArchive(rootIndex);
                try {
                    _root = new Root(rootStream);
                    break;
                } catch (EndOfStreamException) {
                    // Possibly log this.
                }
            }
        }

        private static FileStream FromDisk(string rootDirectory, string subdirectory, string hash)
            => File.OpenRead($"{rootDirectory}/Data/{subdirectory}/{hash.AsSpan()[0..2]}/{hash.AsSpan()[2..4]}/{hash}");

        private Stream FromArchive(Index.Entry indexEntry) {
            var (archiveIndex, archiveOffset) = indexEntry.Offset;
            var size = indexEntry.Size;

            var diskStream = FromDisk(_dataPath, "data", $"data.{archiveIndex:03}");
            // Skip over the header preceding the BLTE data.
            // TODO: Probably fix this to validate said header instead?
            diskStream.Seek(archiveOffset + 0x10 + 4 + 2 + 4 + 4, SeekOrigin.Current);

            return diskStream.ReadBLTE();
        }

        private IEnumerable<Index.Entry> FindContentKey(ContentKey contentKey) {
            foreach (var encodingKey in _encoding.Find(contentKey))
                foreach (var indexEntry in FindEncodingKey(encodingKey))
                    yield return indexEntry;
        }

        /// <summary>
        /// Finds all index entries that match a specific encoding key.
        /// </summary>
        /// <typeparam name="T">An implementation of an encoding key.</typeparam>
        /// <param name="encodingKey">The encoding key to look for.</param>
        /// <returns>An enumeration of all known index entries.</returns>
        /// <remarks><typeparamref name="T"/> is needed to because this function needs access to <see cref="IKey{T}.this[int]"/></remarks>
        private IEnumerable<Index.Entry> FindEncodingKey<T>(T encodingKey)
            where T : IKey<T>, IEncodingKey
        {
            Debug.Assert(encodingKey.Length >= 9);

            // Find the appropriate index to look into.
            var bucketIndex = encodingKey[0];
            for (var i = 1; i < 9; ++i)
                bucketIndex ^= encodingKey[i];
            bucketIndex = (byte) ((bucketIndex & 0xF) ^ (bucketIndex >> 4));

            var index = _indices[bucketIndex];

            // Adjust the provided encoding key to the amount of bytes known to the index.
            var lookup = encodingKey[index.Spec.Key];

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

            if (lowerBound < 0 || upperBound < 0)
                return [];

            return index[lowerBound .. upperBound];
        }
    }
}
