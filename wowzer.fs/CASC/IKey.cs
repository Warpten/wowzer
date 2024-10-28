using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace wowzer.fs.CASC
{
    /// <summary>
    /// An abstact key used to identify resources in a CASC file system.
    /// </summary>
    public interface IKey {
        [Pure] ReadOnlySpan<byte> AsSpan();
    }

    // All this code looks stupid but I'm trying to teach the JIT that 0x10 keys is a hot path that should be preferred and optimized.

    /// <summary>
    /// A so-called encoding key used to identify resources in a CASC file system.
    /// </summary>
    public interface IEncodingKey : IKey {
        [Pure]
        public static IEncodingKey From(ReadOnlySpan<byte> data) {
            if (data.Length == 0x10)
                return new EncodingKey<InlineKeyStorage>(new InlineKeyStorage(data));

            return new EncodingKey<KeyStorage>(new KeyStorage(data));
        }
    }

    /// <summary>
    /// A so-called content key used to identify resources in a CASC file system.
    /// </summary>
    public interface IContentKey : IKey {
        [Pure]
        public static IContentKey From(ReadOnlySpan<byte> data) {
            if (data.Length == 0x10)
                return new ContentKey<InlineKeyStorage>(new InlineKeyStorage(data));

            return new ContentKey<KeyStorage>(new KeyStorage(data));
        }
    }

    [InlineArray(0x10)]
    internal struct InlineKeyStorage : IKey
    {
        private byte _rawData;

        [Pure]
        public InlineKeyStorage(ReadOnlySpan<byte> sourceData) {
            sourceData.CopyTo(MemoryMarshal.CreateSpan(ref _rawData, 0x10));
        }

        [Pure]
        public ReadOnlySpan<byte> AsSpan() => MemoryMarshal.CreateSpan(ref _rawData, 0x10);
    }

    readonly struct KeyStorage : IKey {
        private readonly byte[] _rawData;

        [Pure]
        public KeyStorage(ReadOnlySpan<byte> sourceData) {
            _rawData = GC.AllocateUninitializedArray<byte>(sourceData.Length);
            sourceData.CopyTo(_rawData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining), Pure]
        public ReadOnlySpan<byte> AsSpan() => _rawData;
    }

    readonly struct EncodingKey<T>(T storage) : IEncodingKey where T : struct, IKey {
        private readonly T _storage = storage;

        [MethodImpl(MethodImplOptions.AggressiveInlining), Pure]
        public ReadOnlySpan<byte> AsSpan() => _storage.AsSpan();
    }

    readonly struct ContentKey<T>(T storage) : IContentKey where T : struct, IKey {
        private readonly T _storage = storage;

        [MethodImpl(MethodImplOptions.AggressiveInlining), Pure]
        public ReadOnlySpan<byte> AsSpan() => _storage.AsSpan();
    }

}
