using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using wowzer.fs.Extensions;

using Enc = System.Text.Encoding;

namespace wowzer.fs.CASC
{
    public class Configuration
    {
        private readonly byte[] _rawData;
        private readonly Dictionary<string, Range> _values = [];

        [SkipLocalsInit]
        public Configuration(Stream dataSource)
        {
            _rawData = new byte[(int) dataSource.Length];
            dataSource.ReadExactly(_rawData);

            var keyRange = default(Range);

            var lineStart = 0;
            for (var i = 0; i < _rawData.Length; ++i)
            {
                if (_rawData[i] == '\r' || _rawData[i] == '\n')
                {
                    if (keyRange.Start.Value != keyRange.End.Value)
                    {
                        var keyString = Enc.UTF8.GetString(_rawData[keyRange]);

                        _values.Add(keyString, new(keyRange.End.Value + 3, i));
                    }

                    lineStart = i + 1;
                    keyRange = default;
                }

                if (_rawData[i] == '=')
                    keyRange = new(lineStart, i - 1);
            }
        }

        public OpaqueProperty this[string key]
            => _values.TryGetValue(key, out var range)
                ? new(_rawData[range])
                : new(ReadOnlySpan<byte>.Empty);

        [SkipLocalsInit]
        public readonly ref struct OpaqueProperty(ReadOnlySpan<byte> rawData)
        {
            private readonly ReadOnlySpan<byte> _rawData = rawData;

            public delegate T Transform<T>(ReadOnlySpan<byte> data);

            public EncodingKey[] AsEncodingKeys() => AsArray(data => data.AsKeyString<EncodingKey>());
            public EncodingKey AsEncodingKey() => As(data => data.AsKeyString<EncodingKey>());

            public ContentKey[] AsContentKeys() => AsArray(data => data.AsKeyString<ContentKey>());
            public ContentKey AsContentKey() => As(data => data.AsKeyString<ContentKey>());

            public ReadOnlySpan<byte> AsString() => _rawData;

            public bool HasValue => _rawData.Length != 0;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public (T, U) As<T, U>(Transform<T> left, Transform<U> right)
            {
                var delimiterIndex = _rawData.IndexOf((byte) ' ');
                if (delimiterIndex < 0)
                    return (default, default);

                var leftValue = left(_rawData[.. delimiterIndex]);
                var rightValue = right(_rawData[(delimiterIndex + 1) ..]);

                return (leftValue, rightValue);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T As<T>(Transform<T> transform) => transform(_rawData);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T[] AsArray<T>(Transform<T> transform, byte delimiter = (byte) ' ')
            {
                if (_rawData.Length == 0)
                    return [];

                ref var rawReference = ref MemoryMarshal.GetReference(_rawData);

                var segments = _rawData.Split(delimiter, true);
                var keys = GC.AllocateUninitializedArray<T>(segments.Length);

                for (var i = 0; i < segments.Length; ++i)
                    Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(keys), i) = transform(_rawData[segments[i]]);

                return keys;
            }
        }
    }
}
