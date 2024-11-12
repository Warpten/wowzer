using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using wowzer.fs.Extensions;
using wowzer.fs.Utils;

namespace wowzer.fs.CASC
{
    public class FileSystem
    {
        private readonly string _dataPath;
        private readonly Configuration _cdnConfiguration;
        private readonly Configuration _buildConfiguration;
        private readonly Index[] _indices = [];
        private readonly Encoding _encoding;
        private readonly Root? _root;

        public FileSystem(string path, string buildCfgPath, string cdnCfgPath)
        {
            _dataPath = path;

            // 1. Load configuration files
            using (var bldCfg = OpenConfigurationFile(path, buildCfgPath))
                _buildConfiguration = new Configuration(bldCfg);

            using (var cdnCfg = OpenConfigurationFile(path, cdnCfgPath))
                _cdnConfiguration = new Configuration(cdnCfg);

            // 2. Load indices
            var indices = new List<Index>();
            foreach (var dataFile in Directory.EnumerateFiles($"{path}/Data/data/")) {
                if (!dataFile.EndsWith(".idx"))
                    continue;

                using var fileStream = new FileStream(dataFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                indices.Add(new Index(fileStream));
            }
            indices.Sort((left, right) => left.Bucket.CompareTo(right.Bucket));
            _indices = [.. indices];

            // 3. Load encoding
            var (_, encodingKey) = _buildConfiguration["encoding"].As(
                data => data.AsKeyString<ContentKey>(),
                data => data.AsKeyString<EncodingKey>()
            );

            foreach (var encodingIndex in FindEncodingKey(encodingKey)) {
                using var encodingStream = Open(encodingIndex);
                try {
                    _encoding = new Encoding(encodingStream, Encoding.LoadFlags.Content);
                    break;
                } catch (EndOfStreamException ex) {
                    Console.WriteLine(ex);
                }
            }

            if (_encoding == null)
                throw new InvalidOperationException("Could not load encoding");

            // 4. Load root
            var rootKey = _buildConfiguration["root"].As(data => data.AsKeyString<ContentKey>());
            foreach (var rootIndex in FindContentKey(rootKey)) {
                using var rootStream = Open(rootIndex);
                try {
                    _root = new Root(rootStream);
                    break;
                } catch (EndOfStreamException) {
                    // Possibly log this.
                }
            }
        }

        private static FileStream OpenConfigurationFile(string rootDirectory, string hash)
            => File.OpenRead($"{rootDirectory}/Data/config/{hash.AsSpan()[0..2]}/{hash.AsSpan()[2..4]}/{hash}");

        // Yes, this is stupid, but bypasses exceptions from File.OpenRead.
        // Consider switching to native APIs on Windows?
        private static FileStream? OpenData(string rootDirectory, string fileName)
        {
            var filePath = $"{rootDirectory}/Data/data/{fileName}";
            if (File.Exists(filePath))
                return File.OpenRead(filePath);

            return null;
        }

        /// <summary>
        /// Opens a stream over a file's content. This operation is greedy and loads the entire file in memory.
        /// </summary>
        /// <param name="fileEntry"> And entry identifying the file in this filesystem.</param>
        /// <returns></returns>
        public Stream Open(Entry fileEntry) {
            var (archiveIndex, archiveOffset) = fileEntry.Offset;
            var size = fileEntry.Size; // TODO: Validate that we read the correct amount of bytes

            var diskStream = OpenData(_dataPath, $"data.{archiveIndex:000}");
            if (diskStream != null)
            {
                // Skip over the header preceding the BLTE data.
                // TODO: Probably fix this to validate said header instead?
                diskStream.Seek(archiveOffset + 0x10 + 4 + 2 + 4 + 4, SeekOrigin.Current);
                return diskStream.ReadBLTE(size);
            }

            return new MemoryStream();
        }

        /// <summary>
        /// Finds all known file entries given a <see cref="ContentKey">.
        /// </summary>
        /// <param name="contentKey"></param>
        /// <returns></returns>
        public IEnumerable<Entry> FindContentKey(ContentKey contentKey) {
            return _encoding.Find(contentKey)
                .Select(FindEncodingKey)
                .Flatten();
        }

        /// <summary>
        /// Finds all known file entries given the file's path's Jenkins96 hash.
        /// </summary>
        /// <param name="nameHash"></param>
        /// <returns></returns>
        public IEnumerable<Entry> FindNameHash(ulong nameHash)
        {
            if (_root == null)
                return [];

            var rootRecord = _root.FindHash(nameHash);
            if (rootRecord == null)
                return [];

            return FindContentKey(rootRecord.ContentKey);
        }

        /// <summary>
        /// Finds all known file entries given its ID.
        /// </summary>
        /// <param name="fileDataID">The file's ID</param>
        /// <returns></returns>
        public IEnumerable<Entry> FindFileDataID(int fileDataID)
        {
            if (_root == null)
                return [];

            var rootRecord = _root.FindFileDataID(fileDataID);
            if (rootRecord == null)
                return [];

            return FindContentKey(rootRecord.ContentKey);
        }

        /// <summary>
        /// Finds all known file entries given an <see cref="EncodingKey"/>.
        /// </summary>
        /// <param name="encodingKey">The encoding key to look for.</param>
        /// <returns>An enumeration of all known index entries.</returns>
        private IEnumerable<Entry> FindEncodingKey(EncodingKey encodingKey)
            => FindEncodingKeyImpl(encodingKey);

        /// <remarks>
        ///  <typeparamref name="T"/> is needed to because this function needs access to <see cref="IKey{T}.this[int]"/>.
        ///  This avoids boxing and the language can devirtualize.
        /// </remarks>
        private IEnumerable<Entry> FindEncodingKeyImpl<T>(T encodingKey)
            where T : IKey<T>, IEncodingKey
        {
            Debug.Assert(encodingKey.Length >= 9);

            // Find the appropriate index to look into.
            var bucketIndex = encodingKey[0];
            for (var i = 1; i < 9; ++i)
                bucketIndex ^= encodingKey[i];
            bucketIndex = (byte) ((bucketIndex & 0xF) ^ (bucketIndex >> 4));

            Debug.Assert(bucketIndex < _indices.Length);
            var index = _indices.UnsafeIndex(bucketIndex);

            // Adjust the provided encoding key to the amount of bytes known to the index.
            var lookup = encodingKey[index.Spec.Key];

            var lowerBound = index.BinarySearchBy(static (entry, lookup) => {
                var ordering = entry.Key.SequenceCompareTo(lookup).ToOrdering();
                return ordering switch {
                    Ordering.Equal => Ordering.Greater,
                    _ => ordering
                };
            }, lookup);

            var upperBound = index.BinarySearchBy(static (entry, lookup) => {
                var ordering = entry.Key.SequenceCompareTo(lookup).ToOrdering();
                return ordering switch {
                    Ordering.Equal => Ordering.Less,
                    _ => ordering
                };
            }, lookup);

            return index[lowerBound .. upperBound];
        }
    }
}
