using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualBasic;

using wowzer.fs.Extensions;
using wowzer.fs.IO;

namespace wowzer.fs.CASC
{
    public class Root
    {
        private readonly Page[] _pages;
        private readonly Dictionary<ulong, (int, int)> _hashes = [];

        public Root(Stream dataStream)
        {
            var magic = dataStream.ReadUInt32LE();
            dataStream.Seek(-4, SeekOrigin.Current);

            var (format, version, totalFileCount, namedFileCount) = magic switch {
                0x5453464D => ParseMFST(dataStream), // MFST
                _ => (Format.Legacy, 0, 0, 0)
            };

            var allowUnnamedFiles = format switch {
                Format.MSFT => totalFileCount != namedFileCount,
                Format.Legacy => false,
                _ => throw new UnreachableException()
            };

            var pages = new List<Page>();
            while (dataStream.Position != dataStream.Length) {
                var recordCount = dataStream.ReadInt32LE();
                var contentFlags = dataStream.ReadUInt32LE();
                var localeFlags = dataStream.ReadUInt32LE();

                if (recordCount == 0)
                    continue;

                var fdids = new int[recordCount];
                {
                    var fdid = -1;
                    for (var i = 0; i < recordCount; ++i) {
                        var increment = dataStream.ReadInt32LE();
                        Debug.Assert(increment >= 0);

                        fdid += increment + 1;
                        fdids[i] = fdid;
                    }
                }

                var records = format switch {
                    Format.Legacy => ParseLegacy(dataStream, recordCount, fdids),
                    Format.MSFT => ParseManifest(dataStream, recordCount, contentFlags, allowUnnamedFiles, fdids),
                    _ => throw new UnreachableException()
                };

                var page = new Page(records, contentFlags, localeFlags);
                pages.Add(page);
            }
            _pages = [.. pages];

            Debug.Assert(format == Format.Legacy || totalFileCount == pages.Sum(p => p.Records.Length));

            for (var i = 0; i < pages.Count; ++i) {
                var page = pages[i];
                if (allowUnnamedFiles && (page.ContentFlags & 0x10000000) != 0) {
                    for (var j = 0; j < page.Records.Length; ++j)
                        _hashes.Add(page.Records[j].NameHash, (i, j));
                }
            }

            _hashes.TrimExcess();
        }

        public Record? FindFileDataID(int fileDataID)
        {
            var pageIndex = _pages.BinarySearchBy(page => {
                if (fileDataID < page.Records[0].FileDataID)
                    return -1;
                else if (fileDataID <= page.Records[^0].FileDataID)
                    return 0;
                else
                    return 1;
            });

            if (pageIndex == -1)
                return null;

            var page = _pages.UnsafeIndex(pageIndex);

            var recordIndex = page.Records.BinarySearchBy(record => record.FileDataID.CompareTo(fileDataID));
            if (recordIndex == -1)
                return null;

            return page.Records.UnsafeIndex(recordIndex);
        }

        public Record? FindHash(ulong nameHash)
        {
            if (_hashes.TryGetValue(nameHash, out (int pageIndex, int recordIndex) value))
                return _pages.UnsafeIndex(value.pageIndex).Records.UnsafeIndex(value.recordIndex);

            return null;
        }

        private static Record[] ParseLegacy(Stream dataStream, int recordCount, int[] fdids)
        {
            var records = GC.AllocateUninitializedArray<Record>(recordCount);
            for (var i = 0; i < records.Length; ++i) {
                var contentKey = ContentKey.From(dataStream.ReadExactly(16));
                var nameHash = dataStream.ReadUInt64LE();

                records[i] = new(contentKey, nameHash, fdids[i]);
            }

            return records;
        }

        private static Record[] ParseManifest(Stream dataStream, int recordCount, uint contentFlags, bool allowUnnamedFiles, int[] fdids)
        {
            var nameHashSize = !(allowUnnamedFiles && (contentFlags & 0x10000000) != 0)
                ? 8
                : 0;

            var ckr = new Range(0, recordCount * 16);
            var nhr = new Range(ckr.End.Value, ckr.End.Value + !(allowUnnamedFiles && (contentFlags & 0x10000000) != 0) switch {
                true => 8 * recordCount,
                false => 0
            });

            var sectionContents = GC.AllocateUninitializedArray<byte>(nhr.End.Value);
            dataStream.ReadExactly(sectionContents);

            var contentKeys = sectionContents[ckr];
            var nameHashes = new SpanCursor(sectionContents[nhr]);

            var records = GC.AllocateUninitializedArray<Record>(recordCount);
            for (var i = 0; i < recordCount; ++i)
            {
                var contentKey = ContentKey.From(contentKeys.Slice(i * 16, (i + 1) * 16));
                var nameHash = nameHashes.Remaining switch {
                    >= 8 => nameHashes.ReadBE<ulong>(),
                    _ => 0uL
                };

                records[i] = new(contentKey, nameHash, fdids[i]);
            }
            return records;
        }

        private static (Format, int Version, int TotalFileCount, int NamedFileCount) ParseMFST(Stream dataStream)
        {
            var headerSize = dataStream.ReadInt32LE();
            var version = dataStream.ReadInt32LE();
            if (headerSize > 1000)
                return (Format.MSFT, 0, headerSize, version);

            var totalFileCount = dataStream.ReadInt32LE();
            var namedFileCount = dataStream.ReadInt32LE();
            dataStream.Skip(4); // u32 _

            return (Format.MSFT, version, totalFileCount, namedFileCount);
        }

        public record Record(ContentKey ContentKey, ulong NameHash, int FileDataID);
        private record Page(Record[] Records, uint ContentFlags, uint LocaleFlags);

        private enum Format
        {
            Legacy,
            MSFT
        }
    }
}
