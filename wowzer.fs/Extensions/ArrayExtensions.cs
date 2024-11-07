using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wowzer.fs.Extensions
{
    public static class ArrayExtensions
    {
        public delegate int BinarySearchPredicate<T, U>(T entry, U arg) where U : allows ref struct;

        /// <summary>
        /// Performs a binary search with the given predicate.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U">An argument to carry around to the predicate.</typeparam>
        /// <param name="cmp">A predicate to use to determine ordering.</param>
        /// <param name="arg">An extra argument to pass to the predicate.</param>
        /// <returns>The index of a corresponding entry or -1 if none was found.</returns>
        public static int BinarySearchBy<T, U>(this T[] array, BinarySearchPredicate<T, U> cmp, U arg) where U : allows ref struct
        {
            var size = array.Length;
            var left = 0;
            var right = size;

            while (left < right)
            {
                var mid = left + size / 2;
                var ordering = cmp(array[mid], arg);

                left = ordering switch
                {
                    -1 => mid + 1,
                    _ => left
                };

                right = ordering switch
                {
                    1 => mid,
                    _ => right
                };

                if (ordering == 0)
                {
                    Debug.Assert(mid < array.Length);
                    return mid;
                }

                size = right - left;
            }

            Debug.Assert(left < array.Length);
            return -1;
        }

        public delegate int BinarySearchPredicate<T>(T entry);

        /// <summary>
        /// Performs a binary search with the given predicate.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U">An argument to carry around to the predicate.</typeparam>
        /// <param name="cmp">A predicate to use to determine ordering.</param>
        /// <param name="arg">An extra argument to pass to the predicate.</param>
        /// <returns>The index of a corresponding entry or -1 if none was found.</returns>
        public static int BinarySearchBy<T>(this T[] array, BinarySearchPredicate<T> cmp)
        {
            var size = array.Length;
            var left = 0;
            var right = size;

            while (left < right)
            {
                var mid = left + size / 2;
                var ordering = cmp(array[mid]);

                left = ordering switch
                {
                    -1 => mid + 1,
                    _ => left
                };

                right = ordering switch
                {
                    1 => mid,
                    _ => right
                };

                if (ordering == 0)
                {
                    Debug.Assert(mid < array.Length);
                    return mid;
                }

                size = right - left;
            }

            Debug.Assert(left < array.Length);
            return -1;
        }
    }
}
