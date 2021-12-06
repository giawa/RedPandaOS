using System;

namespace IL2Asm.BaseTypes
{
    public class RealModeAttribute : Attribute
    {
        public uint Offset;

        public RealModeAttribute(uint offset)
        {
            Offset = offset;
        }
    }

    public class BootSectorAttribute : Attribute
    {

    }

    public class AsmMethodAttribute : Attribute
    {

    }
}
