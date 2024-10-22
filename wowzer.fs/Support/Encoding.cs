using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using wowzer.fs.Extensions;
using wowzer.fs.IO;

namespace wowzer.fs.Support
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

        public Encoding(Stream dataStream, LoadFlags loadFlags)
        {
            var header = Header.Read(dataStream);
            dataStream.Skip(header.EncodingSpec);

            var contentMap = new Dictionary<IContentKey, Entry>();
            if (loadFlags.HasFlag(LoadFlags.Content))
            {
                ReadSection(dataStream, header.Content, 1 + 5 + header.Content.KeySize, (ref SpanCursor cursor, Spec spec) =>
                {
                    var firstContentKey = IContentKey.From(cursor.Consume(spec.KeySize));
                    var checksum = cursor.ReadLE<UInt128>();

                    return (firstContentKey, checksum);
                }, (ref SpanCursor cursor, Spec spec, (IContentKey, UInt128) pageHeader) =>
                {
                    var keyAndSize = cursor.Consume(6);

                    var keyCount = keyAndSize[0];
                    var fileSize = ((ulong) keyAndSize[1..].ReadBE<int>() >> 8) | keyAndSize[5];
                    var contentKey = IContentKey.From(cursor.Consume(spec.KeySize));

                    var allKeyData = cursor.Consume(keyCount * header.Encoding.KeySize);

                    var keys = new List<IEncodingKey>(keyCount);
                    for (var i = 0; i < keyCount; ++i)
                    {
                        ref byte keyData = ref Unsafe.Add(
                            ref MemoryMarshal.GetReference(allKeyData),
                            i * header.Encoding.KeySize
                        );

                        keys.Add(IEncodingKey.From(MemoryMarshal.CreateSpan(ref keyData, header.Encoding.KeySize)));
                    }

                    var entry = new Entry(keys, fileSize);
                    contentMap.Add(contentKey, entry);
                });
            }
            else
            {
                dataStream.Skip(header.Content.PageCount * (header.Content.KeySize + 0x10 + header.Content.PageSize));
            }

            var encodingMap = new Dictionary<IEncodingKey, (uint, ulong)>();
            if (loadFlags.HasFlag(LoadFlags.Encoding))
            {
                ReadSection(dataStream, header.Encoding, 4 + 5 + header.Encoding.KeySize, (ref SpanCursor cursor, Spec spec) =>
                {
                    var firstKey = IEncodingKey.From(cursor.Consume(spec.KeySize));
                    var checksum = cursor.ReadLE<UInt128>();

                    return (firstKey, checksum);
                }, (ref SpanCursor cursor, Spec spec, (IEncodingKey, UInt128) pageHeader) =>
                {
                    var encodingKey = IEncodingKey.From(cursor.Consume(spec.KeySize));
                    var index = cursor.ReadBE<uint>();

                    var rawFileSize = cursor.Consume(5);

                    var fileSize = ((ulong) rawFileSize.ReadBE<int>() >> 8) | rawFileSize[4];

                    encodingMap.Add(encodingKey, (index, fileSize));
                });
            }
            else
            {
                dataStream.Skip(header.Encoding.PageCount * (header.Encoding.KeySize + 0x10 + header.Encoding.PageSize));
            }
        }

        private delegate void SpanParser<T>(ref SpanCursor cursor, Spec spec, T pageHeader);
        private delegate T HeaderParser<T>(ref SpanCursor cursor, Spec spec);

        private static void ReadSection<T>(Stream dataStream, Spec spec, int size, HeaderParser<T> header, SpanParser<T> parser)
        {
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

    internal record struct Entry(List<IEncodingKey> Keys, ulong FileSize);
}
