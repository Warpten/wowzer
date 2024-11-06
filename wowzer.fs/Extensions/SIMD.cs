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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> MultiplyAddAdjacent(Vector128<byte> left, Vector128<short> right)
        {
            if (Ssse3.IsSupported)
                return Ssse3.MultiplyAddAdjacent(left, right.AsSByte()).AsByte();
            else if (AdvSimd.Arm64.IsSupported)
            {
                var vl = AdvSimd.Multiply(
                    AdvSimd.ZeroExtendWideningLower(left.GetLower()).AsInt16(),
                    right
                ).AsInt16();
                var vu = AdvSimd.Multiply(
                    AdvSimd.ZeroExtendWideningLower(left.GetUpper()).AsInt16(),
                    right
                ).AsInt16();
                return AdvSimd.AddSaturate(
                    AdvSimd.Arm64.UnzipEven(vl, vu),
                    AdvSimd.Arm64.UnzipOdd(vl, vu)
                ).AsByte();
            }
            else
                throw new NotSupportedException();
        }
    }
}
