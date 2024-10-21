using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using wowzer.fs.Extensions;

namespace wowzer.fs.IO
{
    public ref struct SpanCursor
    {
        private readonly Span<byte> _data;
        private int _offset = 0;

        public SpanCursor(Span<byte> data) => _data = data;

        public readonly int Remaining => _data.Length - _offset;
        public readonly int Length => _data.Length;
        public int Position
        {
            readonly get => _offset;
            set => _offset = value;
        }

        public readonly byte Peek() => _data[_offset];

        public byte ReadUInt8()
        {
            var value = _data[_offset];
            _offset += 1;
            return value;
        }

        public sbyte ReadInt8() => (sbyte) ReadUInt8();

        public Span<byte> Consume(int offset)
        {
            var value = _data[..offset];
            _offset += offset;
            return value;
        }

        public T ReadLE<T>() where T : unmanaged, IBinaryInteger<T>
        {
            var value = _data[_offset..].ReadLE<T>();
            _offset += Unsafe.SizeOf<T>();
            return value;
        }

        public T ReadBE<T>() where T : unmanaged, IBinaryInteger<T>
        {
            var value = _data[_offset..].ReadBE<T>();
            _offset += Unsafe.SizeOf<T>();
            return value;
        }

        public T[] ReadLE<T>(int count) where T : unmanaged, IBinaryInteger<T>
        {
            var value = _data[_offset..].ReadLE<T>(count);
            _offset += Unsafe.SizeOf<T>() * count;
            return value;
        }

        public T[] ReadBE<T>(int count) where T : unmanaged, IBinaryInteger<T>
        {
            var value = _data[_offset..].ReadBE<T>(count);
            _offset += Unsafe.SizeOf<T>() * count;
            return value;
        }
    }
}
