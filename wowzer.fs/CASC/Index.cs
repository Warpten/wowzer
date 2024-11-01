using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using wowzer.fs.Extensions;

namespace wowzer.fs.CASC
{
    public class Index
    {
        public record struct EntrySpec {
            public byte Size;
            public byte Offset;
            public byte Key;
            public byte OffsetBits;

            public EntrySpec(Stream dataStream)
            {
                Size = dataStream.ReadUInt8();
                Offset = dataStream.ReadUInt8();
                Key = dataStream.ReadUInt8();
                OffsetBits = dataStream.ReadUInt8();
            }
        }

        public Index(Stream dataStream)
        {
            var position = dataStream.Position;

            var hash_size = dataStream.ReadUInt32LE();
            var hash = dataStream.ReadUInt32LE();

            var version = dataStream.ReadUInt16LE();
            var bucket = dataStream.ReadUInt8();
            var extraBytes = dataStream.ReadUInt8();

            var spec = new EntrySpec(dataStream);

            var archiveSize = dataStream.ReadUInt64LE();

            var padding = (dataStream.Position - position + 8) & ~7;
            dataStream.Seek(padding - dataStream.Position, SeekOrigin.Current);

            var entriesSize = dataStream.ReadInt32LE();
            var entriesHash = dataStream.ReadUInt32LE();

            var dataBuffer = GC.AllocateUninitializedArray<byte>(entriesSize);
            dataStream.ReadExactly(dataBuffer);
        }
    }
}
