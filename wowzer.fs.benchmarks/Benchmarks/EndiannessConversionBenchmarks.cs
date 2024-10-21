using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

using BenchmarkDotNet.Attributes;

using wowzer.fs.Extensions;

namespace wowzer.fs.benchmarks.Benchmarks
{
    [DisassemblyDiagnoser(exportCombinedDisassemblyReport: true, printSource: true), SimpleJob]
    public class EndiannessConversionBenchmarks
    {
        private Stream _dataStream = new MemoryStream();

        [GlobalSetup]
        public void Setup()
        {
            using var writer = new BinaryWriter(_dataStream, Encoding.UTF8, true);
            for (int i = 0; i < 21; ++i)
                writer.Write(0x11223344u);
            writer.Flush();
        }

        [Benchmark(Description = "Endian swap 21 32-bits numbers")]
        public uint[] BenchmarkEndianness()
        {
            _dataStream.Position = 0;

            var value = _dataStream.ReadUInt32BE(21);
            return value;
        }

        // Tries to be as fair as possible
        [Benchmark(Description = "Endian swap 21 32-bits numbers - Builtin", Baseline = true)]
        public uint[] BenchmarkEndiannessBuiltin()
        {
            _dataStream.Position = 0;

            var integers = new uint[21];
            _dataStream.ReadExactly(MemoryMarshal.AsBytes(integers.AsSpan()));

            BinaryPrimitives.ReverseEndianness(integers.AsSpan(), integers.AsSpan());
            return integers;
        }
    }
}
