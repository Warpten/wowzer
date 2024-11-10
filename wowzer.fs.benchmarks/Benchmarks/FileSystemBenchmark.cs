using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

using wowzer.fs.CASC;

namespace wowzer.fs.benchmarks.Benchmarks
{
    [MemoryDiagnoser]
    public class FileSystemBenchmark
    {
        [Benchmark]
        public FileSystem Benchmark()
        {
            return new FileSystem(@"D:/01 - Games/World of Warcraft/", "08bb65d7bb507e5ea8c94683913ac978", "f40a44cc2fb3ac88f42f91b3d16889da");
        }
    }
}
