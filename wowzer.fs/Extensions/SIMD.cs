using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

namespace wowzer.fs.Extensions
{
    public static class SIMD
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> AddSaturate(Vector128<byte> left, Vector128<byte> right)
        {
#if NET9_0
            return Vector128.AddSaturate(left, right);
#else
            if (Sse2.IsSupported)
                return Sse2.AddSaturate(left, right);
            else if (AdvSimd.Arm64.IsSupported)
                return AdvSimd.AddSaturate(left, right);
            else
                throw new NotSupportedException();
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> SubtractSaturate(Vector128<byte> left, Vector128<byte> right)
        {
#if NET9_0
            return Vector128.SubtractSaturate(left, right);
#else
            if (Sse2.IsSupported)
                return Sse2.SubtractSaturate(left, right);
            else if (AdvSimd.Arm64.IsSupported)
                return AdvSimd.SubtractSaturate(left, right);
            else
                throw new NotSupportedException();
#endif
        }
    }
}
