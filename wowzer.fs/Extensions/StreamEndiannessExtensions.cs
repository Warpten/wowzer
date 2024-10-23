using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace wowzer.fs.Extensions
{
    public static class StreamEndiannessExtensions
    {
        public static unsafe UInt128 ReadUInt128LE(this Stream stream) => ReadEndianAware<UInt128>(stream, !BitConverter.IsLittleEndian);
        public static unsafe ulong ReadUInt64LE(this Stream stream) => ReadEndianAware<ulong>(stream, !BitConverter.IsLittleEndian);
        public static unsafe uint ReadUInt32LE(this Stream stream) => ReadEndianAware<uint>(stream, !BitConverter.IsLittleEndian);
        public static unsafe ushort ReadUInt16LE(this Stream stream) => ReadEndianAware<ushort>(stream, !BitConverter.IsLittleEndian);

        public static unsafe Int128 ReadInt128LE(this Stream stream) => ReadEndianAware<Int128>(stream, !BitConverter.IsLittleEndian);
        public static unsafe long ReadInt64LE(this Stream stream) => ReadEndianAware<long>(stream, !BitConverter.IsLittleEndian);
        public static unsafe int ReadInt32LE(this Stream stream) => ReadEndianAware<int>(stream, !BitConverter.IsLittleEndian);
        public static unsafe short ReadInt16LE(this Stream stream) => ReadEndianAware<short>(stream, !BitConverter.IsLittleEndian);

        public static unsafe UInt128[] ReadUInt128LE(this Stream stream, int count) => ReadEndianAware<UInt128>(stream, count, !BitConverter.IsLittleEndian);
        public static unsafe ulong[] ReadUInt64LE(this Stream stream, int count) => ReadEndianAware<ulong>(stream, count, !BitConverter.IsLittleEndian);
        public static unsafe uint[] ReadUInt32LE(this Stream stream, int count) => ReadEndianAware<uint>(stream, count, !BitConverter.IsLittleEndian);
        public static unsafe ushort[] ReadUInt16LE(this Stream stream, int count) => ReadEndianAware<ushort>(stream, count, !BitConverter.IsLittleEndian);

        public static unsafe Int128[] ReadInt128LE(this Stream stream, int count) => ReadEndianAware<Int128>(stream, count, !BitConverter.IsLittleEndian);
        public static unsafe long[] ReadInt64LE(this Stream stream, int count) => ReadEndianAware<long>(stream, count, !BitConverter.IsLittleEndian);
        public static unsafe int[] ReadInt32LE(this Stream stream, int count) => ReadEndianAware<int>(stream, count, !BitConverter.IsLittleEndian);
        public static unsafe short[] ReadInt16LE(this Stream stream, int count) => ReadEndianAware<short>(stream, count, !BitConverter.IsLittleEndian);

        public static unsafe UInt128 ReadUInt128BE(this Stream stream) => ReadEndianAware<UInt128>(stream, BitConverter.IsLittleEndian);
        public static unsafe ulong ReadUInt64BE(this Stream stream) => ReadEndianAware<ulong>(stream, BitConverter.IsLittleEndian);
        public static unsafe uint ReadUInt32BE(this Stream stream) => ReadEndianAware<uint>(stream, BitConverter.IsLittleEndian);
        public static unsafe ushort ReadUInt16BE(this Stream stream) => ReadEndianAware<ushort>(stream, BitConverter.IsLittleEndian);

        public static unsafe Int128 ReadInt128BE(this Stream stream) => ReadEndianAware<Int128>(stream, BitConverter.IsLittleEndian);
        public static unsafe long ReadInt64BE(this Stream stream) => ReadEndianAware<long>(stream, BitConverter.IsLittleEndian);
        public static unsafe int ReadInt32BE(this Stream stream) => ReadEndianAware<int>(stream, BitConverter.IsLittleEndian);
        public static unsafe short ReadInt16BE(this Stream stream) => ReadEndianAware<short>(stream, BitConverter.IsLittleEndian);

        public static unsafe UInt128[] ReadUInt128BE(this Stream stream, int count) => ReadEndianAware<UInt128>(stream, count, BitConverter.IsLittleEndian);
        public static unsafe ulong[] ReadUInt64BE(this Stream stream, int count) => ReadEndianAware<ulong>(stream, count, BitConverter.IsLittleEndian);
        public static unsafe uint[] ReadUInt32BE(this Stream stream, int count) => ReadEndianAware<uint>(stream, count, BitConverter.IsLittleEndian);
        public static unsafe ushort[] ReadUInt16BE(this Stream stream, int count) => ReadEndianAware<ushort>(stream, count, BitConverter.IsLittleEndian);

        public static unsafe Int128[] ReadInt128BE(this Stream stream, int count) => ReadEndianAware<Int128>(stream, count, !BitConverter.IsLittleEndian);
        public static unsafe long[] ReadInt64BE(this Stream stream, int count) => ReadEndianAware<long>(stream, count, BitConverter.IsLittleEndian);
        public static unsafe int[] ReadInt32BE(this Stream stream, int count) => ReadEndianAware<int>(stream, count, BitConverter.IsLittleEndian);
        public static unsafe short[] ReadInt16BE(this Stream stream, int count) => ReadEndianAware<short>(stream, count, BitConverter.IsLittleEndian);

        [MethodImpl(MethodImplOptions.AggressiveInlining), SkipLocalsInit]
        public static unsafe T ReadEndianAware<T>(this Stream stream, bool reverse) where T : unmanaged, IBinaryInteger<T>
        {
            var value = default(T);
            var valueSpan = MemoryMarshal.CreateSpan(ref value, 1);
            var valueBytes = MemoryMarshal.AsBytes(valueSpan);

            stream.ReadExactly(valueBytes);

            if (reverse)
                valueSpan.ReverseEndianness();

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining), SkipLocalsInit]
        public static unsafe T[] ReadEndianAware<T>(this Stream stream, int count, bool reverse) where T : unmanaged, IBinaryInteger<T>
        {
            var value = GC.AllocateUninitializedArray<T>(count);
            var valueBytes = MemoryMarshal.AsBytes(value.AsSpan());

            stream.ReadExactly(valueBytes);

            if (reverse)
                value.AsSpan().ReverseEndianness();

            return value;
        }
    }
}
