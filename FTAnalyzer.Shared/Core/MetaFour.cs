using System;
using System.Runtime.InteropServices;

namespace FTAnalyzer
{

    /* Conceptuallly Meta4 is a structure (union) that looks like this, but C# does not like that, so the code here simply uses Uint32
     * would love to use a typedefa, but C# does not support that either
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct meta4
    {
        [FieldOffset(0)] public readonly UInt32 u;
        [FieldOffset(0)] public byte[4] s;
    }*/


    public class MetaFour
    {
        public static UInt32 InitMeta4(in string sz)
        {
            UInt32 u = 0;

            if (null != sz)
            {
                int strlen = sz.Length;
                for (int i = 0; i < 4; i++)
                {
                    u <<= 8;
                    if (i < strlen) u += (byte)sz[i];
                };
            };

            return u;
        }
    }
}
