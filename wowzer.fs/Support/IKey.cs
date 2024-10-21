using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wowzer.fs.Support
{
    public interface IKey<T> where T : IKey<T>
    {
    }

    public record struct EncodingKey : IKey<EncodingKey>
    {
        public EncodingKey(Span<byte> keyData)
        {

        }
    }

    public record struct ContentKey : IKey<ContentKey>
    {
        public ContentKey(Span<byte> keyData)
        {

        }
    }
}
