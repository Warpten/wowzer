using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using wowzer.fs.Extensions;
using wowzer.fs.IO;

namespace wowzer.fs.CASC
{
    internal record struct Spec(int KeySize, int PageSize, int PageCount);

    internal record struct Header(Spec Encoding, Spec Content, int EncodingSpec)
    {
        public static Header Read(Stream dataStream)
        {
            var signature = dataStream.ReadUInt16BE();
            var version = dataStream.ReadUInt8();

            var ckeySize = dataStream.ReadUInt8();
            var ekeySize = dataStream.ReadUInt8();
            var cpageSize = dataStream.ReadUInt8();
            var epageSize = dataStream.ReadUInt8();
            var ccount = dataStream.ReadUInt8();
            var ecount = dataStream.ReadUInt8();

            dataStream.Skip(1); // Unknown
            var especSize = dataStream.ReadUInt8();

            var encoding = new Spec(ekeySize, epageSize, ecount);
            var content = new Spec(ckeySize, cpageSize, ccount);
            return new Header(encoding, content, especSize);
        }
    }

    public class Encoding
    {
        [Flags]
        public enum LoadFlags
        {
            Content,
            Encoding,
            EncodingSpec // NYI
        }

        private readonly Dictionary<ContentKey, Entry> _contentMap = []; // TODO: Calculate expected capacity.
        private readonly Dictionary<EncodingKey, (uint, ulong)> _encodingMap = []; // ^

        public Encoding(Stream dataStream, LoadFlags loadFlags)
        {
            var header = Header.Read(dataStream);
            dataStream.Skip(header.EncodingSpec);

            if (loadFlags.HasFlag(LoadFlags.Content))
            {
                ReadSection(dataStream, header.Content, 1 + 5 + header.Content.KeySize, (ref SpanCursor cursor, Spec spec) =>
                {
                    var firstContentKey = ContentKey.From(cursor.Consume(spec.KeySize));
                    var checksum = cursor.ReadLE<UInt128>();

                    return (firstContentKey, checksum);
                }, (ref SpanCursor cursor, Spec spec, (ContentKey, UInt128) pageHeader) =>
                {
                    var keyAndSize = cursor.Consume(6);

                    var keyCount = keyAndSize[0];
                    var fileSize = ((ulong) keyAndSize[1..].ReadBE<int>() << 8) | keyAndSize[5];
                    var contentKey = ContentKey.From(cursor.Consume(spec.KeySize));

                    var allKeyData = cursor.Consume(keyCount * header.Encoding.KeySize);

                    var keys = new List<EncodingKey>(keyCount);
                    for (var i = 0; i < keyCount; ++i)
                    {
                        ref byte keyData = ref Unsafe.Add(
                            ref MemoryMarshal.GetReference(allKeyData),
                            i * header.Encoding.KeySize
                        );

                        keys.Add(EncodingKey.From(MemoryMarshal.CreateSpan(ref keyData, header.Encoding.KeySize)));
                    }

                    var entry = new Entry(keys, fileSize);
                    _contentMap.Add(contentKey, entry);
                });
                _contentMap.TrimExcess();
            }
            else
            {
                dataStream.Skip(header.Content.PageCount * (header.Content.KeySize + 0x10 + header.Content.PageSize));
            }

            if (loadFlags.HasFlag(LoadFlags.Encoding))
            {
                ReadSection(dataStream, header.Encoding, 4 + 5 + header.Encoding.KeySize, (ref SpanCursor cursor, Spec spec) =>
                {
                    var firstKey = EncodingKey.From(cursor.Consume(spec.KeySize));
                    var checksum = cursor.ReadLE<UInt128>();

                    return (firstKey, checksum);
                }, (ref SpanCursor cursor, Spec spec, (EncodingKey, UInt128) pageHeader) =>
                {
                    var encodingKey = EncodingKey.From(cursor.Consume(spec.KeySize));
                    var index = cursor.ReadBE<uint>();

                    var rawFileSize = cursor.Consume(5);

                    var fileSize = ((ulong) rawFileSize.ReadBE<int>() << 8) | rawFileSize[4];

                    _encodingMap.Add(encodingKey, (index, fileSize));
                });
                _encodingMap.TrimExcess();
            }
            else
            {
                dataStream.Skip(header.Encoding.PageCount * (header.Encoding.KeySize + 0x10 + header.Encoding.PageSize));
            }
        }

        public IEnumerable<EncodingKey> Find(ContentKey contentKey)
        {
            if (_contentMap.TryGetValue(contentKey, out var encodingKey))
                return encodingKey.Keys;

            return [];
        }

        private delegate void SpanParser<T>(ref SpanCursor cursor, Spec spec, T pageHeader);
        private delegate T HeaderParser<T>(ref SpanCursor cursor, Spec spec);

        private static void ReadSection<T>(Stream dataStream, Spec spec, int size, HeaderParser<T> header, SpanParser<T> parser) {
            var pagesSize = spec.PageCount * (spec.KeySize + 0x10 + spec.PageSize);

            var section = ArrayPool<byte>.Shared.Rent(pagesSize);
            dataStream.ReadExactly(section);

            var cursor = new SpanCursor(section);

            for (var i = 0; i < spec.PageCount; ++i)
            {
                var pageHeader = header(ref cursor, spec);

                while (cursor.Remaining > size && cursor.Peek() != 0x00)
                    parser(ref cursor, spec, pageHeader);
            }

            ArrayPool<byte>.Shared.Return(section, false);
        }
    }

    internal record struct Entry(List<EncodingKey> Keys, ulong FileSize);
}
