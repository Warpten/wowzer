using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace wowzer.fs.Support
{
    /// <summary>
    /// An abstact key used to identify resources in a CASC file system.
    /// </summary>
    public interface IKey {
        ReadOnlySpan<byte> AsSpan();
    }

    /// <summary>
    /// A so-called encoding key used to identify resources in a CASC file system.
    /// </summary>
    public interface IEncodingKey : IKey {
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

        public InlineKeyStorage(ReadOnlySpan<byte> sourceData) {
            sourceData.CopyTo(MemoryMarshal.CreateSpan(ref _rawData, 0x10));
        }

        public ReadOnlySpan<byte> AsSpan() => MemoryMarshal.CreateSpan(ref _rawData, 0x10);
    }

    readonly struct KeyStorage : IKey {
        private readonly byte[] _rawData;

        public KeyStorage(ReadOnlySpan<byte> sourceData) {
            _rawData = GC.AllocateUninitializedArray<byte>(sourceData.Length);
            sourceData.CopyTo(_rawData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> AsSpan() => _rawData;
    }

    readonly struct EncodingKey<T>(T storage) : IEncodingKey where T : struct, IKey {
        private readonly T _storage = storage;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> AsSpan() => _storage.AsSpan();
    }

    readonly struct ContentKey<T>(T storage) : IContentKey where T : struct, IKey {
        private readonly T _storage = storage;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> AsSpan() => _storage.AsSpan();
    }

}
