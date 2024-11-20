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
                        var keyString = Enc.UTF8.GetString(_rawData.AsSpan()[keyRange]);

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
                : OpaqueProperty.Empty;

        [SkipLocalsInit]
        public readonly ref struct OpaqueProperty(ReadOnlySpan<byte> rawData)
        {
            public static OpaqueProperty Empty => new(ReadOnlySpan<byte>.Emùpty);

            private readonly ReadOnlySpan<byte> _rawData = rawData;

            public delegate T Transform<T>(ReadOnlySpan<byte> data);

            /// <summary>
            /// Parses this property as a space-separated list of encoding keys.
            /// </summary>
            public EncodingKey[] AsEncodingKeys() => AsArray(data => data.AsKeyString<EncodingKey>());

            /// <summary>
            /// Parses this property as an encoding key.
            /// </summary>
            public EncodingKey AsEncodingKey() => As(data => data.AsKeyString<EncodingKey>());

            /// <summary>
            /// Parses this property as a space-separated list of content keys.
            /// </summary>
            public ContentKey[] AsContentKeys() => AsArray(data => data.AsKeyString<ContentKey>());

            /// <summary>
            /// Parses this property as a content key.
            /// </summary>
            public ContentKey AsContentKey() => As(data => data.AsKeyString<ContentKey>());

            public ReadOnlySpan<byte> AsSpan() => _rawData;

            public bool HasValue => _rawData.Length != 0;

            /// <summary>
            /// Parses this property as a pair of values as defined by the provided transformation operations and separated by the given delimiter.
            /// </summary>
            /// <typeparam name="T">The type of the first value.</typeparam>
            /// <typeparam name="U">The type of the second value.</typeparam>
            /// <param name="left">An operation that returns an instance of the first value.</param>
            /// <param name="right">An operation that returns an instance of the second value.</param>
            /// <param name="delimiter">The single-character delimiter to use.</param>
            /// <returns></returns>
            /// <exception cref="InvalidOperationException">If <paramref name="delimiter"/> was not found in the raw bytes.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public (T, U) As<T, U>(Transform<T> left, Transform<U> right, byte delimiter = (byte) ' ')
            {
                var delimiterIndex = _rawData.IndexOf(delimiter);
                if (delimiterIndex < 0)
                    throw new InvalidOperationException();

                var leftValue = left(_rawData[.. delimiterIndex]);
                var rightValue = right(_rawData[(delimiterIndex + 1) ..]);

                return (leftValue, rightValue);
            }

            /// <summary>
            /// Parses this property as a value returned by the provided transformation function.
            /// </summary>
            /// <typeparam name="T">The type of value returned.</typeparam>
            /// <param name="transform">A transformative function.</param>
            /// <returns></returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T As<T>(Transform<T> transform) => transform(_rawData);


            /// <summary>
            /// Parses this property as an array of values returned by the provided transformation function, separated by the ggiven delimiter.
            /// </summary>
            /// <typeparam name="T">The type of value returned.</typeparam>
            /// <param name="transform">A transformative function.</param>
            /// <param name="delimiter">The delimiter separating individual values.</param>
            /// <returns></returns>
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
