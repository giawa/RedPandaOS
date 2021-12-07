using System.Collections.Generic;

namespace IL2Asm
{
    public interface IAssembler
    {
        //void AddToData(string label, string s);
        List<AssembledMethod> Methods { get; }
    }
}
