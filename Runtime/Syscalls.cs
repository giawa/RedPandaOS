using IL2Asm.BaseTypes;
using System;

namespace Runtime
{
    public static class Syscalls
    {
        [AsmMethod]
        public static void WriteCharToStdOutSysCall(char c)
        {
            Console.Write(c);
        }

        [AsmPlug("Runtime.Syscalls.WriteCharToStdOutSysCall_Void_Char", IL2Asm.BaseTypes.Architecture.X86)]
        public static void TempSyscallAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax");     // grab char
            assembly.AddAsm("mov ebx, 1");  // WriteCharToStdOut
            assembly.AddAsm("int 31");      // temporary print char interrupt
        }
    }
}