using IL2Asm.BaseTypes;

namespace Kernel.Memory
{
    public class SMAP_Entry
    {
        public uint BaseL;
        public uint BaseH;
        public uint LengthL;
        public uint LengthH;
        public uint Type;
        public uint ACPI;

        public bool ContainsFrame(uint frame)
        {
            uint baseFrame = (BaseL >> 12) | (BaseH << 20);
            uint lengthFrame = (LengthL >> 12) | (LengthH << 20);

            return frame >= baseFrame && frame < (baseFrame + lengthFrame);
        }
    }

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
            assembly.AddAsm($"; UnsafeCast nop");
        }

        [AsmPlug("Kernel_Memory_Utilities_UnsafeCast_MVar_Object", IL2Asm.BaseTypes.Architecture.X86_Real)]
        private static void UnsafeCastTAsmReal(IAssembledMethod assembly)
        {
            assembly.AddAsm($"; UnsafeCast nop");
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

        [AsmMethod]
        public static uint StructToPtr<T>(ref T s) where T : struct
        {
            return 0;
        }

        [AsmPlug("Kernel_Memory_Utilities_StructToPtr_U4_ByRef", IL2Asm.BaseTypes.Architecture.X86)]
        private static void StructToPtrTAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm($"; StructToPtr nop");
        }

        public static T Clone<T>(T original, uint size) where T : class
        {
            var addr = KernelHeap.Malloc(size);

            CPUHelper.CPU.FastCopyBytes(ObjectToPtr(original), addr, size);

            return PtrToObject<T>(addr);
        }
    }
}
