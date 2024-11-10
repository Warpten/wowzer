using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using wowzer.fs.Utils;

namespace wowzer.fs.Extensions
{
    public static class ArrayExtensions
    {
        /// <summary>
        /// Returns a given element in an array, bypassing bounds check automatically inserted by the JITter.
        /// 
        /// This is semantically equivalen to <pre>arr[index]</pre> but prevents the JIT from emitting bounds checks.
        /// 
        /// Note that in return no guarantees are made and you should always make sure the <paramref name="index"/> is within bounds
        /// yourself.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arr">The array to index.</param>
        /// <param name="index">The index of the element to return.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T UnsafeIndex<T>(this T[] arr, int index)
            => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(arr), index);

        public delegate Ordering BinarySearchPredicate<T>(T entry);
        public delegate Ordering BinarySearchPredicate<T, U>(T entry, U arg) where U : allows ref struct;

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
                    Ordering.Less => mid + 1,
                    _ => left
                };

                right = ordering switch
                {
                    Ordering.Greater => mid,
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
                    Ordering.Less => mid + 1,
                    _ => left
                };

                right = ordering switch
                {
                    Ordering.Greater => mid,
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
