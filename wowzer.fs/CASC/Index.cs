using System;
using System.Buffers;
using System.Collections;
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

        /// <summary>
        /// Performs a binary search with the given predicate.
        /// </summary>
        /// <typeparam name="T">An argument to carry around to the predicate.</typeparam>
        /// <param name="cmp">A predicate to use to determine ordering.</param>
        /// <param name="arg">An extra argument to pass to the predicate.</param>
        /// <returns>The index of a corresponding entry or -1 if none was found.</returns>
        public int BinarySearchBy<T>(BinarySearchPredicate<T> cmp, T arg) where T : allows ref struct {
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
            return -1;
        }

        public Entry this[int index]
        {
            get
            {
                var range = new Range(index * Spec.Length, (index + 1) * Spec.Length);

                if (range.End.Value < _rawData.Length)
                    return new Entry(_rawData[range], Spec);
                else
                    return default;
            }
        }

        public IEnumerable<Entry> this[Range range] => new EntryEnumerable(this, range.Start.Value, range.End.Value);

        public int Bucket { get; init; }
        public int Length { get; init; }

        private readonly byte[] _rawData;

        public EntrySpec Spec { get; init; }

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

        class EntryEnumerable(Index index, int lowerBound, int upperBound) : IEnumerable<Entry>, IEnumerator<Entry>
        {
            private readonly Index _index = index;
            private readonly int _lowerBound = lowerBound;
            private readonly int _upperBound = upperBound;
            private int _current = lowerBound;

            public Entry Current {
                get {
                    var projectedSpan = new Range(_current * _index.Spec.Length, (_current + 1) * _index.Spec.Length);
                    return new Entry(_index._rawData[projectedSpan], _index.Spec);
                }
            }

            object IEnumerator.Current => throw new InvalidOperationException();

            public void Dispose() { }

            public IEnumerator<Entry> GetEnumerator() => this;

            public bool MoveNext()
            {
                ++_current;
                return _current < _upperBound;
            }

            public void Reset() => _current = _lowerBound;

            IEnumerator IEnumerable.GetEnumerator() => this;
        }
    }
}
