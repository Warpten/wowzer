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
    public interface IKey<T> : IEquatable<T>, IComparable<T> where T : IKey<T> {
        /// <summary>
        /// Returns a read-only view of the bytes stored in this object.
        /// </summary>
        /// <returns></returns>
        [Pure] public ReadOnlySpan<byte> AsSpan();

        /// <summary>
        /// Accesses the <paramref name="index"/>-th byte of this key.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        /// <remarks><b>This function is unsafe and avoids bounds checks</b>; see <see cref="Length"/>.</remarks>
        public byte this[int index] => Unsafe.Add(ref MemoryMarshal.GetReference(AsSpan()), index);

        /// <summary>
        /// Returns the actual amount of bytes this object encapsulates.
        /// </summary>
        public int Length { get; }

        [Pure] internal static abstract T From(ReadOnlySpan<byte> data);

        bool IEquatable<T>.Equals(T other) => other.AsSpan().SequenceEqual(AsSpan());

        int IComparable<T>.CompareTo(T other) => other.AsSpan().SequenceCompareTo(AsSpan());

        [Pure, SkipLocalsInit] internal static virtual T[] FromString(ReadOnlySpan<byte> str, byte delimiter)
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
                    var t2 = SIMD.SubtractSaturate(t1, Vector128.Create((byte)6)); // 6 blanks above 0..9
                    var t3 = Vector128.Subtract(t2, Vector128.Create((byte)0xF0)); // Remove high nibble
                    var t4 = v & Vector128.Create((byte)0xDF); // Squash case
                    var t5 = t4 - Vector128.Create((byte)'A'); // Move 'A'..'F' to 0..5.
                    var t6 = SIMD.AddSaturate(t5, Vector128.Create((byte)10)); // And now to 10..15 (and clamp to that range)

                    var t7 = Vector128.Min(t3, t6); // Pick minimum.
                    var t8 = SIMD.AddSaturate(t7, Vector128.Create((byte)(127 - 15)));

                    if (t8.ExtractMostSignificantBits() != 0)
                        return default;

                    // t7 contains 0..15 (as in, only nibbles). Merge.

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
                        // General implementation lives in SIMD.MultiplyAddAdjacent.
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
                        ref Unsafe.Add(ref dstRef, offset / 2),
                        output.AsUInt64().ToScalar()
                    );

                    offset += (nuint) Vector128<byte>.Count;
                }
                while (offset < (nuint) sourceChars.Length);
            }

            for (; offset < (nuint) sourceChars.Length; offset += 2)
            {
                // There's a way to make this branchless, I guess...
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
        /// Converts the given <paramref name="delimiter"/>-separated hex ASCII string to an array of implementations of <see cref="IKey{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of key to produce</typeparam>
        /// <param name="str">An ASCII hex string to parse.</param>
        /// <param name="delimiter">A delimiter indicating how to split the given <paramref name="str"/>.
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining), Pure]
        public static T[] AsKeyStringArray<T>(this ReadOnlySpan<byte> str, byte delimiter = (byte)' ') where T : struct, IKey<T>
            => T.FromString(str, delimiter);

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

    public interface IContentKey { }

    // All this code looks stupid but I'm trying to teach the JIT that 16-bytes keys is a hot path that should be preferred and optimized.
    public readonly struct ContentKey : IKey<ContentKey>, IContentKey
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

        public int Length => _storage.Length;
    }

    public interface IEncodingKey { }

    /// <summary>
    /// A so-called encoding key used to identify resources in a CASC file system.
    /// </summary>
    public readonly struct EncodingKey : IKey<EncodingKey>, IEncodingKey
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

        public int Length => _storage.Length;
    }

    interface IKeyStorage {
        [Pure] public ReadOnlySpan<byte> AsSpan();

        [Pure] public int Length { get; }
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

        public int Length { get; } = 0x10;
    }

    readonly struct HeapKeyStorage : IKeyStorage {
        private readonly byte[] _rawData;

        [Pure] public HeapKeyStorage(ReadOnlySpan<byte> sourceData) {
            _rawData = GC.AllocateUninitializedArray<byte>(sourceData.Length);
            sourceData.CopyTo(_rawData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining), Pure]
        public ReadOnlySpan<byte> AsSpan() => _rawData;

        public int Length => _rawData.Length;
    }
}
