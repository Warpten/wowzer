using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace wowzer.fs.Extensions
{
    internal static class RangeExtensions
    {
        public static int Count(this Range range) => range.End.Value - range.Start.Value;

        public static bool Contains(this Range range, Index index) => range.Start.Value <= index.Value && index.Value < range.End.Value;
    }
}
