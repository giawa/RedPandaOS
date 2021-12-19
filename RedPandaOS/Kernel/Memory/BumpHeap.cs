﻿using CPUHelper;
using IL2Asm.BaseTypes;
using System.Runtime.InteropServices;

namespace Kernel.Memory
{
    public static class BumpHeap
    {
        private static uint _addr = 0x20000;    // the start of the kernel heap

        [Allocator]
        public static uint Malloc(uint size, uint init = 0)
        {
            uint modulo = Runtime.Math32.Modulo(size, 4);
            if (modulo != 0) size = size + (4 - modulo);

            for (uint i = _addr; i < _addr + size; i += 4)
                CPU.WriteMemInt(i, init);

            Logging.WriteLine(LogLevel.Trace, "Allocating {0} bytes at 0x{1}", size, _addr);

            var addr = _addr;
            _addr += size;
            return addr;
        }

        [Allocator]
        public static uint MallocPageAligned(uint size, uint init = 0)
        {
            if ((_addr & 0xfffff000) != 0)
            {
                _addr = _addr & 0xfffff000;
                _addr += 0x1000;
            }

            for (uint i = _addr; i < _addr + size; i += 4)
                CPU.WriteMemInt(i, init);

            Logging.WriteLine(LogLevel.Trace, "Allocating {0} bytes at 0x{1}", size, _addr);

            var addr = _addr;
            _addr += size;
            return addr;
        }

        public static void Free(uint addr)
        {
            // nop for now
        }

        public static T Malloc<T>()
        {
            uint size = (uint)Marshal.SizeOf<T>();
            uint addr = Malloc(size);

            return PtrToObject<T>(addr);
        }

        public static T Malloc<T>(uint size)
        {
            uint addr = Malloc(size);

            return PtrToObject<T>(addr);
        }

        public static T[] MallocArray<T>(uint arraySize)
        {
            uint size = (uint)Marshal.SizeOf<T>();
            size *= arraySize;
            uint addr = Malloc(size);

            return PtrToObject<T[]>(addr);
        }

        [AsmMethod]
        public static T PtrToObject<T>(uint addr)
        {
            return default(T);
        }

        [AsmPlug("Kernel_Memory_BumpHeap_PtrToObject_MVar_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void PtrToObjectTAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm($"; PtrToObject nop");
        }

        [AsmMethod]
        public static uint ObjectToPtr<T>(T obj)
        {
            return 0;
        }

        [AsmPlug("Kernel_Memory_BumpHeap_ObjectToPtr_U4_MVar", IL2Asm.BaseTypes.Architecture.X86)]
        private static void ObjectToPtrTAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm($"; ObjectToPtr nop");
        }
    }
}