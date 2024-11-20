using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using wowzer.fs.Extensions;

using static wowzer.fs.CASC.Index;

namespace wowzer.fs.CASC
{
    /// <summary>
    /// Represents an entry in the CASC file system.
    /// </summary>
    public readonly ref struct Entry(ReadOnlySpan<byte> rawData, EntrySpec spec)
    {
        private readonly ReadOnlySpan<byte> _rawData = rawData;
        private readonly EntrySpec _spec = spec;

        public ReadOnlySpan<byte> Key => _rawData[_spec.Key];

        /// <summary>
        /// Returns the size of the file associated with this entry.
        /// </summary>
        public long Size
        {
            get
            {
                var data = _rawData[_spec.Size];

                var size = 0L; // Little endian
                for (var i = 0; i < data.Length; ++i)
                    size |= (long)data[i] << (8 * i);

                return size;
            }
        }


        /// <summary>
        /// Returns information about the archive containing the file associated with this entry.
        /// </summary>
        public (long ArchiveIndex, long ArchiveOffset) Offset
        {
            get
            {
                var data = _rawData[_spec.Offset];

                var rawData = 0L; // Big endian
                for (var i = 0; i < data.Length; ++i)
                    rawData = (rawData << 8) | data[i];

                var archiveBits = _spec.Offset.Count() * 8 - _spec.OffsetBits;
                var offsetBits = _spec.OffsetBits;

                return (
                    (rawData >> offsetBits) & ((1 << archiveBits) - 1),
                    rawData & ((1 << offsetBits) - 1)
                );
            }
        }

        /// <summary>
        /// Reads the file associated with this entry from the given filesystem.
        /// </summary>
        /// <param name="fileSystem">The filesystem to read from.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Handle Read(FileSystem fileSystem) => fileSystem.Open(this);
    }
}
