using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace wowzer.fs.Utils
{
    internal unsafe class UnsafeSpanStream : Stream
    {
        private byte* _start;
        private byte* _end;
        private byte* _cursor;
        private readonly bool _readOnly;

        public UnsafeSpanStream(Span<byte> target)
        {
            _start = _cursor = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(target));
            _end = _start + target.Length;
            _readOnly = false;
        }

        public UnsafeSpanStream(ReadOnlySpan<byte> target)
        {
            _start = _cursor = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(target));
            _end = _start + target.Length;
            _readOnly = true;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => !_readOnly;

        public override long Length => _end - _start;

        public override long Position
        {
            get => _cursor - _start;
            set => _cursor = _start + value;
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var adjustedLength = (int) Math.Min(count, _end - _cursor);
            new Span<byte>(_cursor, adjustedLength).CopyTo(buffer.AsSpan().Slice(offset, adjustedLength));
            return adjustedLength;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    _cursor = _start + offset;
                    if (_cursor > _end)
                        _cursor = _end;
                    break;
                case SeekOrigin.End:
                    _cursor = _end + offset;
                    if (_cursor < _start)
                        _cursor = _start;
                    break;
                case SeekOrigin.Current:
                    _cursor += offset;
                    if (_cursor < _start) _cursor = _start;
                    if (_cursor > _end) _cursor = _end;
                    break;
            }

            return _cursor - _start;
        }

        public override void SetLength(long value)
        {
            _end = _start + value;
            if (_cursor > _end)
                _cursor = _end;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var adjustedSize = (int) Math.Min(count, _end - _cursor);
            buffer.AsSpan().Slice(offset, adjustedSize).CopyTo(new Span<byte>(_cursor, adjustedSize));
        }
    }
}
