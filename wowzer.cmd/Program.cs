using System.Diagnostics;

using wowzer.fs.CASC;
using wowzer.fs.Extensions;

namespace wowzer.cmd
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var stopwatch = new Stopwatch();

            stopwatch.Start();
            // var filesystem = new FileSystem(@"D:/01 - Games/World of Warcraft/", "08bb65d7bb507e5ea8c94683913ac978", "f40a44cc2fb3ac88f42f91b3d16889da");

            using (var compressedStream = File.OpenRead("/workspaces/root"))
            using (var blte = compressedStream.ReadBLTE()) {
                var root = new Root(blte);
            }
            stopwatch.Stop();

            Console.WriteLine(stopwatch.Elapsed);
            Console.ReadLine();
        }
    }
}
