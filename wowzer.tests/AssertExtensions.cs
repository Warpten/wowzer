using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static wowzer.tests.AssertExtensions;

namespace wowzer.tests
{
    public static class AssertExtensions
    {
        public static void AreEqual<T>(this Assert _, T expected, T actual, IComparer comparer)
        {
            CollectionAssert.AreEqual(
                new[] { expected },
                new[] { actual }, comparer,
                $"\nExpected: <{expected}>.\nActual: <{actual}>.");
        }

        public static void AreEqual<T>(this Assert _, T expected, T actual, BinaryPredicate<T> compareFunc)
        {
            var comparer = new LambdaComparer<T>(compareFunc);

            CollectionAssert.AreEqual(
                new[] { expected },
                new[] { actual }, comparer,
                $"\nExpected: <{expected}>.\nActual: <{actual}>.");
        }

        public delegate bool BinaryPredicate<in T>(T left, T right);

        class LambdaComparer<T>(BinaryPredicate<T> compareFunc) : IComparer
        {
            private readonly BinaryPredicate<T> _compareFunc = compareFunc;

            public int Compare(object? x, object? y)
            {
                if (x == null && y == null)
                    return 0;

                if (x is not T t1 || y is not T t2)
                    return -1;

                return _compareFunc(t1, t2) ? 0 : 1;
            }
        }
    }
}
