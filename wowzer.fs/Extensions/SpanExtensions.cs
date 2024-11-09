using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace wowzer.fs.Extensions
{
    public static class SpanExtensions
    {
        public static Range[] Split<T>(this ReadOnlySpan<T> source, T delimiter, bool removeEmptyEntries = true)
            where T : IEquatable<T>
        {
            if (source.Length == 0)
                return [];

            var list = new List<Range>();
            for (var i = 0; i < source.Length;)
            {
                var delimiterIndex = source[i..].IndexOf(delimiter);
                if (delimiterIndex == -1)
                    delimiterIndex = source.Length;
                
                if (!removeEmptyEntries || delimiterIndex != i + 1)
                    list.Add(new Range(i, delimiterIndex));

                i = delimiterIndex + 1;
            }

            return [.. list];
        }

        /// <summary>
        /// Reads an integer type in big-endian from the given span.
        /// </summary>
        /// <typeparam name="T">The type of value to read.</typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static T ReadBE<T>(this Span<byte> source) where T : unmanaged, IBinaryInteger<T>
            => ReadEndianAware<T>(source, BitConverter.IsLittleEndian);

        /// <summary>
        /// Reads an integer type in little-endian from the given span.
        /// </summary>
        /// <typeparam name="T">The type of value to read.</typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static T ReadLE<T>(this Span<byte> source) where T : unmanaged, IBinaryInteger<T>
            => ReadEndianAware<T>(source, !BitConverter.IsLittleEndian);

        /// <summary>
        /// Reads an integer type in whatever the system endianness is.
        /// </summary>
        /// <typeparam name="T">The type of value to read.</typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static T ReadNative<T>(this Span<byte> source) where T : unmanaged, IBinaryInteger<T>
            => ReadEndianAware<T>(source, false);

        /// <summary>
        /// Reads an array of integer type in big-endian from the given span.
        /// </summary>
        /// <typeparam name="T">The type of value to read.</typeparam>
        /// <param name="source"></param>
        /// <param name="count">The amount of values to read.</param>
        /// <returns></returns>
        public static T[] ReadBE<T>(this Span<byte> source, int count) where T : unmanaged, IBinaryInteger<T>
            => ReadEndianAware<T>(source, count, BitConverter.IsLittleEndian);

        /// <summary>
        /// Reads an array of integer type in little-endian from the given span.
        /// </summary>
        /// <typeparam name="T">The type of value to read.</typeparam>
        /// <param name="source"></param>
        /// <param name="count">The amount of values to read.</param>
        /// <returns></returns>
        public static T[] ReadLE<T>(this Span<byte> source, int count) where T : unmanaged, IBinaryInteger<T>
            => ReadEndianAware<T>(source, count, !BitConverter.IsLittleEndian);

        /// <summary>
        /// Reads an array of integer type in whatever the system endianness is.
        /// </summary>
        /// <typeparam name="T">The type of value to read.</typeparam>
        /// <param name="source"></param>
        /// <param name="count">The amount of values to read.</param>
        /// <returns></returns>
        public static T[] ReadNative<T>(this Span<byte> source, int count) where T : unmanaged, IBinaryInteger<T>
            => ReadEndianAware<T>(source, count, false);

        [MethodImpl(MethodImplOptions.AggressiveInlining), SkipLocalsInit]
        private static T ReadEndianAware<T>(this Span<byte> source, bool reverse) where T : unmanaged, IBinaryInteger<T>
        {
            Debug.Assert(source.Length >= Unsafe.SizeOf<T>());

            var value = MemoryMarshal.Read<T>(source);

            var valueSpan = MemoryMarshal.CreateSpan(ref value, 1);
            if (reverse)
                valueSpan.ReverseEndianness();

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining), SkipLocalsInit]
        private static T[] ReadEndianAware<T>(this Span<byte> source, int count, bool reverse) where T : unmanaged, IBinaryInteger<T>
        {
            Debug.Assert(source.Length >= Unsafe.SizeOf<T>() * count);

            var value = GC.AllocateUninitializedArray<T>(count);
            MemoryMarshal.Cast<byte, T>(source)[..count].CopyTo(value);

            if (reverse)
                value.AsSpan().ReverseEndianness();

            return value;
        }

        /// <summary>
        /// Performs endianness reversal on the current span. This operation happens in-place.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining), SkipLocalsInit]
        public static unsafe void ReverseEndianness<T>(this Span<T> value) where T : unmanaged, IBinaryInteger<T>
        {
            if (typeof(T) == typeof(UInt128) || typeof(T) == typeof(Int128))
            {
                // Treat as ulongs and reverse those.
                var inversable = MemoryMarshal.Cast<T, ulong>(value);
                inversable.ReverseEndianness();

                // Exchange lower and upper
                // TODO: Is there any way to vectorize this?
                for (var i = 0; i < inversable.Length; i += 2)
                    (inversable[i], inversable[i + 1]) = (inversable[i], inversable[i + 1]);
            }
            else
            {
                var valueSpan = MemoryMarshal.AsBytes(value);
                ref byte byteRef = ref MemoryMarshal.GetReference(valueSpan);
                ref T sourceRef = ref MemoryMarshal.GetReference(value);

                int i = 0;

                if (Vector256.IsHardwareAccelerated)
                {
                    var iterationCount = value.Length - Vector256<T>.Count;
                    if (i <= iterationCount)
                    {
                        var swizzle = MakeSwizzle256Fast<T>();

                        while (i <= iterationCount)
                        {
                            var source = Vector256.LoadUnsafe(ref sourceRef, (uint)i).AsByte();

                            Vector256.StoreUnsafe(
                                Vector256.Shuffle(source, swizzle).As<byte, T>(),
                                ref sourceRef,
                                (uint)i);
                            i += Vector256<T>.Count;
                        }
                    }
                }

                if (Vector128.IsHardwareAccelerated)
                {
                    var iterationCount = value.Length - Vector128<T>.Count;
                    if (i <= iterationCount)
                    {
                        var swizzle = MakeSwizzle128Fast<T>();

                        while (i <= iterationCount)
                        {
                            var source = Vector128.LoadUnsafe(ref sourceRef, (uint)i).AsByte();

                            Vector128.StoreUnsafe(
                                Vector128.Shuffle(source, swizzle).As<byte, T>(),
                                ref sourceRef,
                                (uint)i);
                            i += Vector128<T>.Count;
                        }
                    }
                }

                // Is this worth the effort?
                if (Vector64.IsHardwareAccelerated)
                {
                    var iterationCount = value.Length - Vector64<T>.Count;
                    if (i <= iterationCount)
                    {
                        var swizzle = MakeSwizzle64Fast<T>();

                        while (i <= iterationCount)
                        {
                            var source = Vector64.LoadUnsafe(ref sourceRef, (uint)i).AsByte();

                            Vector64.StoreUnsafe(
                                Vector64.Shuffle(source, swizzle).As<byte, T>(),
                                ref sourceRef,
                                (uint)i);
                            i += Vector64<T>.Count;
                        }
                    }
                }

                i *= Unsafe.SizeOf<T>();
                while (i < valueSpan.Length)
                {
                    for (int j = 0, k = Unsafe.SizeOf<T>() - 1; j < k; ++j, --k)
                    {
                        var leftIndex = i + j;
                        var rightIndex = i + k;

                        ref byte leftByte = ref Unsafe.Add(ref byteRef, leftIndex);
                        ref byte rightByte = ref Unsafe.Add(ref byteRef, rightIndex);

                        (rightByte, leftByte) = (leftByte, rightByte);
                    }

                    i += Unsafe.SizeOf<T>();
                }
            }
        }

        //! TODO: Revisit in .NET 9, the compiler might be able to see the output of this function is a constant.
        [MethodImpl(MethodImplOptions.AggressiveInlining), SkipLocalsInit]
        private unsafe static void MakeSwizzle<M>(ref M zero, int wordSize) where M : struct
        {
            // Go through pointers to eliminate bounds checks
            var zeroPtr = (byte*)Unsafe.AsPointer(ref zero);
            for (var i = 0; i < Unsafe.SizeOf<M>(); i += wordSize)
            {
                for (var j = wordSize - 1; j >= 0; --j)
                    zeroPtr[i + j] = (byte)(wordSize - j - 1 + i);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<byte> MakeSwizzle256Fast<T>() where T : unmanaged, IBinaryInteger<T>
        {
            Unsafe.SkipInit(out Vector256<byte> swizzle);
            GetShuffleMask<T, Vector256<byte>>(ref swizzle);
            return swizzle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> MakeSwizzle128Fast<T>() where T : unmanaged, IBinaryInteger<T>
        {
            Unsafe.SkipInit(out Vector128<byte> swizzle);
            GetShuffleMask<T, Vector128<byte>>(ref swizzle);
            return swizzle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector64<byte> MakeSwizzle64Fast<T>() where T : unmanaged, IBinaryInteger<T>
        {
            Unsafe.SkipInit(out Vector64<byte> swizzle);
            GetShuffleMask<T, Vector64<byte>>(ref swizzle);
            return swizzle;
        }

        private static byte[] U16 = [3, 2, 1, 0, 7, 6, 5, 4, 11, 10, 9, 8, 15, 14, 13, 12, 19, 18, 17, 16, 23, 22, 21, 20, 27, 26, 25, 24, 31, 30, 29, 28];
        private static byte[] U32 = [3, 2, 1, 0, 7, 6, 5, 4, 11, 10, 9, 8, 15, 14, 13, 12, 19, 18, 17, 16, 23, 22, 21, 20, 27, 26, 25, 24, 31, 30, 29, 28];
        private static byte[] U64 = [7, 6, 5, 4, 3, 2, 1, 0, 15, 14, 13, 12, 11, 10, 9, 8, 23, 22, 21, 20, 19, 18, 17, 16, 31, 30, 29, 28, 27, 26, 25, 24];
        private static byte[] U128 = [15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17, 16];

        private static unsafe void GetShuffleMask<U, T>(ref T value) where U : unmanaged where T : struct
        {
            if (Unsafe.SizeOf<U>() == 2)
                value = Unsafe.AsRef<T>(Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(U16)));
            else if (Unsafe.SizeOf<U>() == 4)
                value = Unsafe.AsRef<T>(Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(U32)));
            else if (Unsafe.SizeOf<U>() == 8)
                value = Unsafe.AsRef<T>(Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(U64)));
            else if (Unsafe.SizeOf<U>() == 16)
                value = Unsafe.AsRef<T>(Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(U128)));
        }
    }
}
