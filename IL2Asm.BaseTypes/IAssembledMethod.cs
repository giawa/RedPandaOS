using System;
using System.Collections.Generic;

namespace IL2Asm.BaseTypes
{
    public interface IAssembledMethod
    {
        public List<string> Assembly { get; }

        public void AddAsm(string asm);

        public string GetUniqueLabel(string name);

        public string HeapAllocatorMethod { get; }
        public string ThrowExceptionMethod { get; }
    }
}
