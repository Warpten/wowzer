using System.Text;
using System.Text.Unicode;
using BenchmarkDotNet.Running;

using wowzer.fs.benchmarks.Benchmarks;

namespace wowzer.fs.benchmarks
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var hs = new HexStringParserBenchmark();
            var dataSource = Enumerable.Single(hs.ArgumentsSource());
            Console.WriteLine("Source: {0}", string.Join(' ', dataSource.Item2));
            Console.WriteLine("Source: {0}", dataSource.Item1);
            Console.WriteLine("Reference: {0}", Convert.ToHexString(hs.Baseline(dataSource)));
            Console.WriteLine("Vectorized: {0}", Convert.ToHexString(hs.VectorizedBytes(dataSource)));


            var cb = new EndiannessConversionBenchmarks();
            cb.Setup();
            cb.BenchmarkEndianness();
            cb.BenchmarkEndiannessBuiltin();

            var summary = BenchmarkRunner.Run(typeof(Program).Assembly);
        }
    }
}
