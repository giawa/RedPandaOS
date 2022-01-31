using IL2Asm.BaseTypes;
using System;

namespace Plugs
{
    public static class BitConverterPlugs
    {
        [AsmPlug("System.BitConverter.ToUInt16_U2_SzArray_I4", Architecture.X86)]
        private static void ToUInt16Asm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax"); // index
            assembly.AddAsm("pop ebx"); // array
            assembly.AddAsm("lea ebx, [8 + eax + ebx]");    // offset by 8 due to array count/type
            assembly.AddAsm("movzx eax, word [ebx]");
            assembly.AddAsm("push eax");
        }

        
        [AsmPlug("System.BitConverter.ToUInt32_U4_SzArray_I4", Architecture.X86)]
        private static void ToUInt32Asm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax"); // index
            assembly.AddAsm("pop ebx"); // array
            assembly.AddAsm("lea ebx, [8 + eax + ebx]");    // offset by 8 due to array count/type
            assembly.AddAsm("mov eax, [ebx]");
            assembly.AddAsm("push eax");
        }
    }
}
