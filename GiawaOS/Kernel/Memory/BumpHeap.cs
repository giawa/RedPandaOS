using CPUHelper;
using System.Runtime.InteropServices;

namespace Kernel.Memory
{
    public static class BumpHeap
    {
        private static uint _addr = 0x20000;    // the start of the kernel heap

        public static uint Malloc(uint size, uint init = 0)
        {
            uint modulo = Runtime.Math32.Modulo(size, 4);
            if (modulo != 0) size = size + (4 - modulo);

            for (uint i = _addr; i < _addr + size; i += 4)
                CPUHelper.CPU.WriteMemInt(i, init);

            /*Devices.VGA.WriteVideoMemoryString("Allocating ");
            Devices.VGA.WriteHex(size);
            Devices.VGA.WriteVideoMemoryString(" bytes at ");
            Devices.VGA.WriteHex(_addr);
            Devices.VGA.WriteLine();*/

            var addr = _addr;
            _addr += size;
            return addr;
        }

        public static void Free(uint addr)
        {
            // nop for now
        }

        public static T Malloc<T>() where T : new()
        {
            uint size = (uint)Marshal.SizeOf<T>();
            uint addr = Malloc(size, 0);

            return PtrToObject<T>(addr);
        }

        public static T[] MallocArray<T>(uint arraySize) where T : new()
        {
            uint size = (uint)Marshal.SizeOf<T>();
            size *= arraySize;
            uint addr = Malloc(size, 0);

            return PtrToObject<T[]>(addr);
        }

        [AsmMethod]
        private static T PtrToObject<T>(uint addr)
        {
            return default(T);
        }
    }
}
