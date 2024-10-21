using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace wowzer.fs.IO
{
    /// <summary>
    /// A <see cref="Stream"/> over a <see cref="Span{T}"/>. Note that this class is not thread-safe, and
    /// despite being a reference type, it should never outlive the <see cref="Span{T}"/> it is constructed from.
    /// </summary>
    public sealed unsafe class SpanStream : Stream
    {
        private byte* _cursor;
        private byte* _start;
        private byte* _end;

        private readonly bool _canWrite;

        public override bool CanWrite => _canWrite;
        public override bool CanSeek { get; } = true;
        public override bool CanRead { get; } = true;

        public override long Length => _end - _start;
        public override long Position {
            get => _cursor - _start;
            set => _cursor = _start + value;
        }

        public SpanStream(Span<byte> data) : this(data, true) { }
        public SpanStream(ReadOnlySpan<byte> data) : this(data, false) { }

        private SpanStream(ReadOnlySpan<byte> data, bool canWrite)
        {
            _canWrite = canWrite;

            _cursor = _start = (byte*) Unsafe.AsPointer(ref MemoryMarshal.GetReference(data));
            _end = _start + data.Length;
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var readCount = (int) Math.Max(count, _end - _cursor);
            if (readCount > 0)
            {
                var dst = new Span<byte>(buffer, offset, count);
                var src = new Span<byte>(_cursor, readCount);
                src.CopyTo(dst);
            }

            return readCount;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _start - (origin switch {
                SeekOrigin.Begin => _cursor = _start + offset,
                SeekOrigin.Current => _cursor += offset,
                SeekOrigin.End => _cursor = _end + offset,
                _ => throw new NotImplementedException()
            });
        }

        public override void SetLength(long value) => _end = _start + value;

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!_canWrite)
                return;

            var dst = new Span<byte>(_cursor, (int)(_end - _cursor));
            buffer.AsSpan().Slice(offset, count).CopyTo(dst);
            _cursor += count;
        }
    }
}
