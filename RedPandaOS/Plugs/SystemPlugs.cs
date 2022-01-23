using IL2Asm.BaseTypes;
using System;

namespace Plugs
{
    public static class SystemPlugs
    {
        [AsmPlug("System.Char.ToString_String", Architecture.X86)]
        private static void CharToStringAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("push 12"); // 8 bytes for array metadata, then 2 bytes for char and 2 bytes for null termination
            assembly.AddAsm("push 0");
            assembly.AddAsm($"call {assembly.HeapAllocatorMethod}");
            // the array is now in eax
            assembly.AddAsm("mov dword [eax], 2");      // the array length of 2
            assembly.AddAsm("mov dword [eax + 4], 2");  // the size per element of 2
            assembly.AddAsm("pop ebx");                 // the contents to put in the array
            assembly.AddAsm("mov ebx, [ebx]");          // place the contents in the array
            assembly.AddAsm("mov [eax + 8], bx");       // place the contents in the array
            assembly.AddAsm("mov word [eax + 10], 0");  // null terminate the string
            assembly.AddAsm("push eax");                // push the resulting 'string' object back on to the stack
        }

        [AsmPlug("System.Object..ctor_Void", Architecture.X86)]
        private static void ObjectConstructorAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax");
        }

        [AsmPlug("System.Action`1.Invoke_Void_Var", Architecture.X86)]
        private static void ActionInvoke1Asm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop edi"); // pop the arg
            assembly.AddAsm("pop eax"); // pop the action
            assembly.AddAsm("mov ebx, [eax + 4]");  // grab the object we're calling this on
            assembly.AddAsm("push ebx");    // push the object
            assembly.AddAsm("push edi");    // push the arg again
            assembly.AddAsm("call [eax]");
        }

        [AsmPlug("System.Action.Invoke_Void", Architecture.X86)]
        private static void ActionInvokeAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax");
            assembly.AddAsm("call [eax]");
        }

        [CSharpPlug("System.Threading.Thread.Sleep_Void_I4")]
        private static void ThreadingThreadSleep(int milliseconds)
        {
            uint start = Kernel.Devices.PIT.TickCount;
            uint end = start + Kernel.Devices.PIT.Frequency * (uint)milliseconds / 1000;
            if (start == end) end++;    // delay was less than the accuracy of the timer, so always wait at least 1 tick

            while (Kernel.Devices.PIT.TickCount < end)
            {
                if (Kernel.Devices.PIT.TickCount < end - 1)
                    Kernel.Scheduler.Yield();   // since we're not doing any work yield to a different thread
            }
        }

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

            assembly.AddAsm("ret 12");  // pop all the pushed items off the stack
        }
    }
}
