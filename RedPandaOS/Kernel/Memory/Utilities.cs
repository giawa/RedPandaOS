using IL2Asm.BaseTypes;

namespace Kernel.Memory
{
    public static class Utilities
    {
        [AsmMethod]
        public static T UnsafeCast<T>(object obj)
        {
            return default(T);
        }

        [AsmPlug("Kernel_Memory_Utilities_UnsafeCast_MVar_Object", IL2Asm.BaseTypes.Architecture.X86)]
        private static void UnsafeCastTAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm($"; PtrToObject nop");
        }

        [AsmPlug("Kernel_Memory_Utilities_UnsafeCast_MVar_Object", IL2Asm.BaseTypes.Architecture.X86_Real)]
        private static void UnsafeCastTAsmReal(IAssembledMethod assembly)
        {
            assembly.AddAsm($"; PtrToObject nop");
        }

        [AsmMethod]
        public static T PtrToObject<T>(uint addr)
        {
            return default(T);
        }

        [AsmPlug("Kernel_Memory_Utilities_PtrToObject_MVar_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void PtrToObjectTAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm($"; PtrToObject nop");
        }

        [AsmPlug("Kernel_Memory_Utilities_PtrToObject_MVar_U4", IL2Asm.BaseTypes.Architecture.X86_Real)]
        private static void PtrToObjectTAsmReal(IAssembledMethod assembly)
        {
            assembly.AddAsm($"; PtrToObject nop");
        }

        [AsmMethod]
        public static uint ObjectToPtr<T>(T obj)
        {
            return 0;
        }

        [AsmPlug("Kernel_Memory_Utilities_ObjectToPtr_U4_MVar", IL2Asm.BaseTypes.Architecture.X86)]
        private static void ObjectToPtrTAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm($"; ObjectToPtr nop");
        }

        [AsmPlug("Kernel_Memory_Utilities_ObjectToPtr_U4_MVar", IL2Asm.BaseTypes.Architecture.X86_Real)]
        private static void ObjectToPtrTAsmReal(IAssembledMethod assembly)
        {
            assembly.AddAsm($"; ObjectToPtr nop");
        }
    }
}
