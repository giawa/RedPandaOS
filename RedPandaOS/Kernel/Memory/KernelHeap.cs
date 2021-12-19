using CPUHelper;
using IL2Asm.BaseTypes;

namespace Kernel.Memory
{
    public static class KernelHeap
    {
        public static BumpHeap KernelAllocator;

        private static uint _addr;

        static KernelHeap()
        {
            if (_addr == 0) _addr = 0x20000;    // the start of the kernel heap
            KernelAllocator = BumpHeap.Instance;
        }

        [Allocator]
        public static uint Malloc(uint size, uint init = 0)
        {
            // it's possible the static constructor gets called after other things trying to malloc
            // so check for the case where the address is still zero and the constructor hasn't been called
            if (_addr == 0) _addr = 0x20000;    

            if (KernelAllocator != null)
                return KernelAllocator.Malloc(size, init);

            uint modulo = Runtime.Math32.Modulo(size, 4);
            if (modulo != 0) size = size + (4 - modulo);

            for (uint i = _addr; i < _addr + size; i += 4)
                CPU.WriteMemInt(i, init);

            Logging.WriteLine(LogLevel.Trace, "[KernelHeap] Allocating {0} bytes at 0x{1}", size, _addr);

            var addr = _addr;
            _addr += size;
            return addr;
        }
    }
}
