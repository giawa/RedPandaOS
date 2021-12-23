using System;

namespace IL2Asm.BaseTypes
{
    public enum Architecture
    {
        X86_Real,
        X86,
        X86_64,
        ARM32,
        ARM64,
        IA32,
        IA64,
        MIPS,
        RiscV
    }

    [Flags]
    public enum AsmFlags : byte
    {
        None = 0x00,
        Inline = 0x01
    }

    public class AsmPlugAttribute : Attribute
    {
        public string AsmMethodName { get; private set; }

        public Architecture Architecture { get; private set; }

        public AsmFlags Flags { get; private set; }

        public AsmPlugAttribute(string asmMethodName, Architecture architecture, AsmFlags flags = AsmFlags.Inline)
        {
            AsmMethodName = asmMethodName;
            Architecture = architecture;
            Flags = flags;
        }
    }
}
