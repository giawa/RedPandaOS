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
            assembly.AddAsm("mov ecx, 1");  // WriteCharToStdOut
            assembly.AddAsm("int 31");      // temporary print char interrupt
        }

        public static uint GetPidSysCall()
        {
            return 0;
        }

        [AsmPlug("Runtime.Syscalls.GetPidSysCall_U4", IL2Asm.BaseTypes.Architecture.X86)]
        public static void GetPidSysCallAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("mov ecx, 2");  // WriteCharToStdOut
            assembly.AddAsm("int 31");      // temporary print char interrupt
            assembly.AddAsm("push eax");    // push the resulting eax back on to the stack to recover from the main process
        }

        public static void QuitSysCall(uint exitCode)
        {
        }

        [AsmPlug("Runtime.Syscalls.QuitSysCall_Void_U4", IL2Asm.BaseTypes.Architecture.X86)]
        public static void QuitSysCallAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax");     // grab exitCode
            assembly.AddAsm("mov ecx, 3");  // WriteCharToStdOut
            assembly.AddAsm("int 31");      // temporary print char interrupt
        }
    }
}