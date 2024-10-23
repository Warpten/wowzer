using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

using CommandLine;

using wowzer.fs.Extensions;

namespace wowzer.fs.benchmarks.Benchmarks
{
    [MemoryDiagnoser]
    public class StreamSkipBenchmark
    {
        public class NonSeekStream(int size) : MemoryStream(new byte[size]) {
            public override bool CanSeek => false;
        }

#pragma warning disable IDE0044 // Add readonly modifier
        public NonSeekStream _dataStorage = new(10000);

        [Params(10, 50, 100, 500, 1000, 4000)]
        public int _skipCount;
#pragma warning restore IDE0044 // Add readonly modifier

        [Benchmark]
        public NonSeekStream TestSkip()
        {
            _dataStorage.Position = 0;
            _dataStorage.Skip(_skipCount);
            return _dataStorage;
        }
    }
}
