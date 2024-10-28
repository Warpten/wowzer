using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using Enc = System.Text.Encoding;

namespace wowzer.fs.CASC
{
    public class Configuration
    {
        private readonly byte[] _rawData;
        private Dictionary<string, Range> _values = [];

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

        internal ReadOnlySpan<byte> this[string key]
            => _values.TryGetValue(key, out var range)
                ? _rawData[range]
                : ReadOnlySpan<byte>.Empty;
    }

    public readonly ref struct RootSpec
    {
        public static bool TryParse(Configuration configuration, ref IContentKey contentKey)
        {
            var rawData = configuration["root"];
            if (rawData.Length == 0)
                return false;

            ref var rawReference = ref MemoryMarshal.GetReference(rawData);

            Span<byte> contentData = stackalloc byte[rawData.Length / 2];
            for (var i = 0; i < contentData.Length; ++i)
            {
                var highNibble = Unsafe.Add(ref rawReference, i * 2);
                highNibble = (byte) (highNibble - (highNibble < 58 ? 48 : 87));

                var lowNibble = Unsafe.Add(ref rawReference, i * 2 + 1);
                lowNibble = (byte) (lowNibble - (lowNibble < 58 ? 48 : 87));

                contentData[i] = (byte)((highNibble << 4)
                    | lowNibble);
            }

            contentKey = IContentKey.From(contentData);
            return true;
        }
    }

    public readonly ref struct EncodingSpec
    {
        public required IContentKey ContentKey { get; init; }
        public required IEncodingKey EncodingKey { get; init; }

        public static bool TryParse(Configuration configuration, ref EncodingSpec output)
        {
            var rawData = configuration["encoding"];
            ref var rawReference = ref MemoryMarshal.GetReference(rawData);

            var delimiterIndex = rawData.IndexOf((byte) ' ');
            if (delimiterIndex < 0)
                return false;

            Span<byte> contentData = stackalloc byte[delimiterIndex / 2];
            for (var i = 0; i < contentData.Length; ++i)
                contentData[i] = (byte) ((Unsafe.Add(ref rawReference, i * 2) << 4)
                    | Unsafe.Add(ref rawReference, i * 2 + 1));

            Span<byte> encodingData = stackalloc byte[(rawData.Length - delimiterIndex) / 2];
            for (var i = 0; i < encodingData.Length; ++i)
                encodingData[i] = (byte) ((Unsafe.Add(ref rawReference, delimiterIndex + i * 2) << 4)
                    | Unsafe.Add(ref rawReference, delimiterIndex + i * 2 + 1));

            var contentKey = IContentKey.From(contentData);
            var encodingKey = IEncodingKey.From(encodingData);
            output = new EncodingSpec {
                ContentKey = contentKey,
                EncodingKey = encodingKey
            };

            return true;
        }
    }
}
