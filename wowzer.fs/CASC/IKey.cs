using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

using wowzer.fs.Extensions;

namespace wowzer.fs.CASC
{
    /// <summary>
    /// An abstact key used to identify resources in a CASC file system.
    /// </summary>
    public interface IKey<T> : IEquatable<T> where T : IKey<T> {
        [Pure] public ReadOnlySpan<byte> AsSpan();

        [Pure] internal static abstract T From(ReadOnlySpan<byte> data);

        bool IEquatable<T>.Equals(T other) => other.AsSpan().SequenceEqual(AsSpan());

        [Pure, SkipLocalsInit] internal static virtual T[] FromString(ReadOnlySpan<byte> str, byte delimiter = (byte) ' ')
        {
            var sections = str.Split(delimiter, true);
            if (sections.Length == 0)
                return [];

            ref var rawReference = ref MemoryMarshal.GetReference(str);

            var dest = GC.AllocateUninitializedArray<T>(sections.Length);
            var i = 0;
            foreach (var section in sections)
                dest[i] = T.FromString(str[section]);
            return dest;
        }

        [Pure, SkipLocalsInit] internal virtual static T FromString(ReadOnlySpan<byte> sourceChars)
        {
            if (sourceChars.IsEmpty)
                return default;

            Span<byte> workBuffer = stackalloc byte[sourceChars.Length / 2];

            ref byte srcRef = ref MemoryMarshal.GetReference(sourceChars);
            ref byte dstRef = ref MemoryMarshal.GetReference(workBuffer);

            nuint offset = 0;
            if (BitConverter.IsLittleEndian
                && (Ssse3.IsSupported || AdvSimd.Arm64.IsSupported)
                && sourceChars.Length >= Vector128<byte>.Count)
            {
                // Author: Geoff Langdale, http://branchfree.org
                // https://github.com/WojciechMula/toys/blob/master/simd-parse-hex/geoff_algorithm.cpp#L15
                // https://twitter.com/geofflangdale/status/1484460241240539137
                // https://twitter.com/geofflangdale/status/1484460243778097159
                // https://twitter.com/geofflangdale/status/1484460245560684550
                // https://twitter.com/geofflangdale/status/1484460247368355842
                // I wish I'd never comment twitter links in code... but here we are.

                do
                {
                    var v = Vector128.LoadUnsafe(ref srcRef, offset);

                    var t1 = v + Vector128.Create((byte)(0xFF - '9')); // Move digits '0'..'9' into range 0xF6..0xFF.
                    var t2 = SIMD.SubtractSaturate(t1, Vector128.Create((byte)6));
                    var t3 = Vector128.Subtract(t2, Vector128.Create((byte)0xF0));
                    var t4 = v & Vector128.Create((byte)0xDF);
                    var t5 = t4 - Vector128.Create((byte)'A');
                    var t6 = SIMD.AddSaturate(t5, Vector128.Create((byte)10));

                    var t7 = Vector128.Min(t3, t6);
                    var t8 = SIMD.AddSaturate(t7, Vector128.Create((byte)(127 - 15)));

                    if (t8.ExtractMostSignificantBits() != 0)
                        return default;

                    Vector128<byte> t0;
                    if (Sse3.IsSupported)
                    {
                        t0 = Ssse3.MultiplyAddAdjacent(t7,
                            Vector128.Create((short) 0x0110).AsSByte()).AsByte();
                    }
                    else if (AdvSimd.Arm64.IsSupported)
                    {
                        // Workaround for missing MultiplyAddAdjacent on ARM -- Stolen from corelib
                        // Note this is specific to the 0x0110 case - See Convert.FromHexString.
                        var even = AdvSimd.Arm64.TransposeEven(t7, Vector128<byte>.Zero).AsInt16();
                        var odd = AdvSimd.Arm64.TransposeOdd(t7, Vector128<byte>.Zero).AsInt16();
                        even = AdvSimd.ShiftLeftLogical(even, 4).AsInt16();
                        t0 = AdvSimd.AddSaturate(even, odd).AsByte();
                    }
                    else
                    {
                        // Consider sse2neon ?
                        t0 = default;
                        throw new NotImplementedException();
                    }

                    var output = Vector128.Shuffle(t0, Vector128.Create((byte) 0, 2, 4, 6, 8, 10, 12, 14, 0, 0, 0, 0, 0, 0, 0, 0));

                    Unsafe.WriteUnaligned(
                        ref Unsafe.Add(
                            ref MemoryMarshal.GetReference(workBuffer),
                            offset / 2
                        ),
                        output.AsUInt64().ToScalar()
                    );

                    offset += (nuint) Vector128<byte>.Count;
                }
                while (offset < (nuint) sourceChars.Length);
            }

            for (; offset < (nuint) sourceChars.Length; offset += 2)
            {
                var highNibble = Unsafe.Add(ref srcRef, offset);
                highNibble = (byte)(highNibble - (highNibble < 58 ? 48 : 87));

                var lowNibble = Unsafe.Add(ref srcRef, offset + 1);
                lowNibble = (byte)(lowNibble - (lowNibble < 58 ? 48 : 87));

                Unsafe.Add(ref dstRef, offset / 2) = (byte)((highNibble << 4) | lowNibble);
            }

            return T.From(workBuffer);
        }
    }

    public static class KeyExtensions
    {
        /// <summary>
        /// Converts the given hex ASCII string to an implementation of <see cref="IKey{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of key to produce</typeparam>
        /// <param name="str">An ASCII hex string to parse.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining), Pure]
        public static T AsKeyString<T>(this ReadOnlySpan<byte> str) where T : struct, IKey<T>
            => T.FromString(str);

        /// <summary>
        /// Converts the given bytes into an implementation of <see cref="IKey{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of key to produce</typeparam>
        /// <param name="bytes">The bytes to treat as a <typeparamref name="T"/></param>.
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining), Pure]
        public static T AsKey<T>(this ReadOnlySpan<byte> bytes) where T : struct, IKey<T>
            => T.From(bytes);
    }

    // All this code looks stupid but I'm trying to teach the JIT that 0x10 keys is a hot path that should be preferred and optimized.
    public readonly struct ContentKey : IKey<ContentKey>, IEquatable<ContentKey>
    {
        private readonly IKeyStorage _storage;

        [Pure] private ContentKey(ReadOnlySpan<byte> data)
            => _storage = data.Length == 0x10
                ? new InlinedKeyStorage(data)
                : new HeapKeyStorage(data);

        [MethodImpl(MethodImplOptions.AggressiveInlining), Pure] 
        public readonly ReadOnlySpan<byte> AsSpan() => _storage.AsSpan();

        [MethodImpl(MethodImplOptions.AggressiveInlining), Pure] 
        public static ContentKey From(ReadOnlySpan<byte> data) => new(data);
    }

    /// <summary>
    /// A so-called encoding key used to identify resources in a CASC file system.
    /// </summary>
    public readonly struct EncodingKey : IKey<EncodingKey>
    {
        private readonly IKeyStorage _storage;

        [Pure] private EncodingKey(ReadOnlySpan<byte> data)
            => _storage = data.Length == 0x10
                ? new InlinedKeyStorage(data)
                : new HeapKeyStorage(data);

        [MethodImpl(MethodImplOptions.AggressiveInlining), Pure]
        public readonly ReadOnlySpan<byte> AsSpan() => _storage.AsSpan();

        [MethodImpl(MethodImplOptions.AggressiveInlining), Pure] 
        public static EncodingKey From(ReadOnlySpan<byte> data) => new(data);
    }

    interface IKeyStorage {
        [Pure] public ReadOnlySpan<byte> AsSpan();
    }

    [InlineArray(0x10)]
    struct InlinedKeyStorage : IKeyStorage
    {
        private byte _rawData;

        [Pure] public InlinedKeyStorage(ReadOnlySpan<byte> sourceData) {
            sourceData.CopyTo(MemoryMarshal.CreateSpan(ref _rawData, 0x10));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining), Pure]
        public ReadOnlySpan<byte> AsSpan() => MemoryMarshal.CreateSpan(ref _rawData, 0x10);
    }

    readonly struct HeapKeyStorage : IKeyStorage {
        private readonly byte[] _rawData;

        [Pure] public HeapKeyStorage(ReadOnlySpan<byte> sourceData) {
            _rawData = GC.AllocateUninitializedArray<byte>(sourceData.Length);
            sourceData.CopyTo(_rawData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining), Pure]
        public ReadOnlySpan<byte> AsSpan() => _rawData;
    }
}
