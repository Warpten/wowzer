using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace wowzer.fs.Extensions
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> source) where T : allows ref struct
            => new FlatteningRefEnumerator<T>(source);

        private class FlatteningRefEnumerator<T> : IEnumerable<T>, IEnumerator<T> where T : allows ref struct
        {
            private readonly IEnumerator<IEnumerable<T>> _source;
            private IEnumerator<T> _current = null;

            public FlatteningRefEnumerator(IEnumerable<IEnumerable<T>> source)
            {
                _source = source.GetEnumerator();
                if (_source.MoveNext())
                    _current = _source.Current.GetEnumerator();
            }

            public T Current => _current.Current;

            // object IEnumerator.Current => RuntimeHelpers.IsReferenceOrContainsReferences<T>()
            //     ? throw new InvalidOperationException()
            //    : _current.Current;
            object IEnumerator.Current => throw new InvalidOperationException();


            public void Dispose() { }

            public bool MoveNext()
            {
                if (_current == null)
                    return false;

                var movedNext = _current.MoveNext();
                if (!movedNext) {
                    if (!_source.MoveNext())
                        return false;

                    _current = _source.Current.GetEnumerator();
                }

                return _current.MoveNext();
            }

            public void Reset() => throw new InvalidOperationException();

            public IEnumerator<T> GetEnumerator() => this;
            IEnumerator IEnumerable.GetEnumerator() => this;
        }
    }
}