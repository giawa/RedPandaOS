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

    public class AsmPlugAttribute : Attribute
    {
        public string AsmMethodName { get; private set; }

        public Architecture Architecture { get; private set; }

        public AsmPlugAttribute(string asmMethodName, Architecture architecture)
        {
            AsmMethodName = asmMethodName;
            Architecture = architecture;
        }
    }
}
