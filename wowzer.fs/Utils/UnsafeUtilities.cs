using System;
using System.Runtime.CompilerServices;

namespace wowzer.fs.Utils
{
    public static class UnsafeUtilities
    {
        /// <summary>
        /// Promotes the given boolean to an integer (0 or 1).
        /// 
        /// <br />
        /// 
        /// Using a ternary expression such as <c>x ? 1 : 0</c> results in suboptimal assembly being generated:
        /// 
        /// <code>
        /// test eax, eax
        /// setne al
        /// movzx eax, al
        /// </code>
        /// 
        /// This is especially useful when you want to reinterpret the boolean as an integer and perform arithmetics:
        /// 
        /// <code>
        /// var value = (x ? 8 : 0) * y
        /// </code>
        /// 
        /// This entire piece of code can be simplified by promoting x to an integer, shifting left by 3 bytes, and multiplying
        /// the result. The code above produces:
        /// 
        /// <code>
        /// mov   eax, 8
        /// xor   ecx, ecx
        /// test  dil, dil  ; dil = x
        /// cmove eax, ecx
        /// imul  eax, esi  ; esi = y ; eax holds result
        /// </code>
        /// 
        /// While the alternative produces:
        /// <code>
        /// movzx rax, dil  ; dil = x
        /// shl   eax, 3
        /// imul  eax, esi  ; esi = y ; eax holds result
        /// </code>
        /// 
        /// </summary>
        /// <param name="booleanValue"></param>
        /// <returns></returns>
        public static int ToInteger(bool booleanValue) {
            if (sizeof(bool) == sizeof(byte)) {
                return (int) Unsafe.As<bool, byte>(ref booleanValue);
            } else if (sizeof(bool) == sizeof(short)) {
                return (int) Unsafe.As<bool, short>(ref booleanValue); // ? 8 : 0;
            } else if (sizeof(bool) == sizeof(int)) {
                return (int) Unsafe.As<bool, int>(ref booleanValue); // ? 8 : 0;
            } else {
                return booleanValue ? 8u : 0u;
            }
        }
    }
}
