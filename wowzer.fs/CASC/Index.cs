using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using wowzer.fs.Extensions;

namespace wowzer.fs.CASC
{
    public class Index
    {
        [SkipLocalsInit] public Index(Stream dataStream)
        {
            var fileStart = dataStream.Position;

            dataStream.Skip(8); // hashSize (LE), hash (LE)

            var version = dataStream.ReadUInt16LE();
            Debug.Assert(version == 7);

            Bucket = dataStream.ReadUInt8();
            var extraBytes = dataStream.ReadUInt8();
            Debug.Assert(extraBytes == 0);

            Spec = new EntrySpec(dataStream);

            dataStream.Skip(8); // archiveSize (LE)

            var position = dataStream.Position;
            var padding = (position - fileStart + 8) & ~7;
            dataStream.Seek(padding - position, SeekOrigin.Current);

            var entriesSize = dataStream.ReadInt32LE();
            dataStream.Skip(8); // entriesHash (LE)

            _rawData = new byte[entriesSize];
            dataStream.ReadExactly(_rawData);

            Length = entriesSize / Spec.Length;
        }

        public delegate int BinarySearchPredicate<T>(Entry entry, T arg) where T : allows ref struct;

        public int? BinarySearchBy<T>(BinarySearchPredicate<T> cmp, T arg) where T : allows ref struct {
            var size = Length;
            var left = 0;
            var right = size;

            while (left < right)
            {
                var mid = left + size / 2;
                var ordering = cmp(this[mid], arg);

                left = ordering switch {
                    -1 => mid + 1,
                    _ => left
                };

                right = ordering switch {
                    1 => mid,
                    _ => right
                };

                if (ordering == 0)
                {
                    Debug.Assert(mid < Length);
                    return mid;
                }

                size = right - left;
            }

            Debug.Assert(left < Length);
            return null;
        }

        public Entry this[int index]
        {
            get
            {
                var range = new Range(index * Spec.Length, (index + 1) * Spec.Length);

                if (range.End.Value < _rawData.Length)
                    return new Entry(_rawData.AsSpan()[range], Spec);
                else
                    return default;
            }
        }

        public int Bucket { get; init; }
        public int Length { get; init; }

        private readonly byte[] _rawData;

        public EntrySpec Spec { get; init; }

        public readonly ref struct Entry(ReadOnlySpan<byte> rawData, EntrySpec spec)
        {
            private readonly ReadOnlySpan<byte> _rawData = rawData;
            private readonly EntrySpec _spec = spec;

            public ReadOnlySpan<byte> Key => _rawData[_spec.Key];
            public long Size
            {
                get
                {
                    var data = _rawData[_spec.Size];

                    var size = 0L; // Little endian
                    for (var i = 0; i < data.Length; ++i)
                        size |= (long) data[i] << (8 * i);

                    return size;
                }
            }

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
        }

        public readonly record struct EntrySpec
        {
            public readonly Range Key;
            public readonly Range Offset;
            public readonly Range Size;
            public readonly byte OffsetBits;

            public EntrySpec(Stream dataStream)
            {
                var size = dataStream.ReadUInt8();
                var offset = dataStream.ReadUInt8();
                var key = dataStream.ReadUInt8();
                OffsetBits = dataStream.ReadUInt8();

                Key = new(0, key);
                Offset = new(key, key + offset);
                Size = new(key + offset, key + offset + size);
            }

            public readonly int Length => Size.End.Value;
        }

    }
}
