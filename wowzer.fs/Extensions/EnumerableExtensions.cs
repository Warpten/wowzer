using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace wowzer.fs.Extensions
{
    public static class EnumerableExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<TValue> Flatten<TValue>(this IEnumerable<IEnumerable<TValue>> source)
            where TValue : allows ref struct
            => new FlatteningRefEnumerable<IEnumerable<TValue>, TValue>(source);

        private class FlatteningRefEnumerable<TEnumerator, TValue> : IEnumerable<TValue>, IEnumerator<TValue>
            where TEnumerator : IEnumerable<TValue>
            where TValue : allows ref struct
        {
            private readonly IEnumerator<TEnumerator> _source;
            private IEnumerator<TValue> _current;

            public FlatteningRefEnumerable(IEnumerable<TEnumerator> source) {
                _source = source.GetEnumerator();
                if (_source.MoveNext())
                    _current = _source.Current.GetEnumerator();
            }

            public TValue Current => _current.Current;
            object IEnumerator.Current => throw new NotImplementedException();

            public void Dispose() { _current = null; }

            public bool MoveNext()
            {
                if (_current == null)
                    return false;

                var moved = _current.MoveNext();
                if (!moved) {
                    if (!_source.MoveNext())
                        return false;

                    _current = _source.Current.GetEnumerator();
                }

                return _current.MoveNext();
            }

            public void Reset() => throw new InvalidOperationException();

            public IEnumerator<TValue> GetEnumerator() => this;
            IEnumerator IEnumerable.GetEnumerator() => this;
        }
    }
}