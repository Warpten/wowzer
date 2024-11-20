using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wowzer.fs.CASC
{
    /// <summary>
    /// A single-use 
    /// </summary>
    public record Handle : IDisposable
    {
        private readonly Stream _dataStream;

        internal Handle(Stream dataStream)
        {
            _dataStream = dataStream;
        }

        public Stream Open() => _dataStream;

        public void Dispose() => _dataStream.Dispose();

        public bool IsValid => _dataStream != Stream.Null;

        public static Handle Empty { get; } = new Handle(Stream.Null);
    }
}
