using BenchmarkDotNet.Running;

using wowzer.fs.benchmarks.Benchmarks;

namespace wowzer.fs.benchmarks
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run(typeof(Program).Assembly, null, args);
        }
    }
}
