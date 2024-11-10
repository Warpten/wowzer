using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

namespace wowzer.fs.benchmarks.Benchmarks
{
    public class BooleanToIntegerBenchmark
	{
		[ParamsAllValues]
		public bool Flag;

		[Benchmark, ArgumentsSource(nameof(Numbers))]
		public int Ternary(int seed) => (Flag ? seed : 0);

		[Benchmark, ArgumentsSource(nameof(Numbers))]
		public int TernaryProduct(int seed) => (Flag ? 1 : 0) * seed;

		[Benchmark, ArgumentsSource(nameof(Numbers))]
		public int Promotion(int seed) => Unsafe.As<bool, byte>(ref Flag) * seed;

		public IEnumerable<int> Numbers() {
			unchecked {
				yield return (int)0xDEADBEEF;
			}
		}
    }
}
