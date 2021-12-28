using System;

namespace IL2Asm.BaseTypes
{
    public class CSharpPlugAttribute : Attribute
    {
        public string AsmMethodName { get; private set; }

        public CSharpPlugAttribute(string asmMethodName)
        {
            AsmMethodName = asmMethodName;
        }
    }
}
