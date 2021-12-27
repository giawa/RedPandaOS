﻿using IL2Asm.BaseTypes;
using System;

namespace Plugs
{
    public static class SystemPlugs
    {
        [AsmPlug("System.Array.Clear_Void_Class_I4_I4", Architecture.X86, AsmFlags.None)]
        private static void ArrayClearAsm(IAssembledMethod assembly)
        {
            var loopLabel = assembly.GetUniqueLabel("LOOP");
            var endLabel = assembly.GetUniqueLabel("END");
            var loopLabelByte = assembly.GetUniqueLabel("LOOP_BYTE");

            assembly.AddAsm("mov ebx, [esp + 4]");  // length
            assembly.AddAsm("mov eax, [esp + 8]");  // index
            assembly.AddAsm("mov esi, [esp + 12]"); // start of array

            assembly.AddAsm("push ecx");    // we use ecx as 0
            assembly.AddAsm("xor ecx, ecx");

            assembly.AddAsm("mov edi, [esi + 4]");  // size per element
            assembly.AddAsm("push edx");            // multiply clobbers edx
            assembly.AddAsm("mul edi");             // eax now contains index*sizePerElement
            //assembly.AddAsm("pop edx");             // recover edx after clobber

            assembly.AddAsm("lea esi, [8 + esi + 1 * eax]");  // actual start of data

            // the length is in terms of elements, we need to translate to dwords
            // eax is no longer needed, so we can use it here
            assembly.AddAsm("mov eax, ebx");
            assembly.AddAsm("mul edi");     // eax now contains the length in bytes
            assembly.AddAsm("pop edx");

            // we need to work out whether we can do 4 bytes at a time, or only a single byte
            // ebx and edi is available for us to use
            assembly.AddAsm("mov ebx, edi");
            assembly.AddAsm("and ebx, 0xfffffffc");
            assembly.AddAsm("cmp ebx, edi");
            assembly.AddAsm($"jne {loopLabelByte}");

            // 32bit DWORD
            assembly.AddAsm("shr eax, 2");  // eax now contains the length in dwords
            assembly.AddAsm($"{loopLabel}:");
            assembly.AddAsm("cmp eax, 0");
            assembly.AddAsm($"je {endLabel}");
            assembly.AddAsm("mov [esi], ecx");
            assembly.AddAsm("add esi, 4");
            assembly.AddAsm("dec eax");
            assembly.AddAsm($"jmp {loopLabel}");
            // END DWORD

            // 8bit BYTE
            assembly.AddAsm($"{loopLabelByte}:");
            assembly.AddAsm("cmp eax, 0");
            assembly.AddAsm($"je {endLabel}");
            assembly.AddAsm("mov byte [esi], cl");
            assembly.AddAsm("inc esi");
            assembly.AddAsm("dec eax");
            assembly.AddAsm($"jmp {loopLabelByte}");
            // END BYTE

            assembly.AddAsm($"{endLabel}:");
            assembly.AddAsm("pop ecx");

            assembly.AddAsm("ret");
        }
    }
}