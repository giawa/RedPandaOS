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
            if (_addr == 0) _addr = 0x60000;    // the start of the kernel heap
            //KernelAllocator = SplitBumpHeap.Instance;
        }

        [Allocator]
        public static uint Malloc(uint size, uint init = 0)
        {
            if (KernelAllocator != null)
                return KernelAllocator.Malloc(size, init);

            return MallocEternal(size, init);
        }

        public static uint MallocEternal(uint size, uint init = 0)
        {
            // it's possible the static constructor gets called after other things trying to malloc
            // so check for the case where the address is still zero and the constructor hasn't been called
            if (_addr == 0) _addr = 0x60000;

            if ((_addr & 0x03) != 0)
            {
                Logging.WriteLine(LogLevel.Trace, "[KernelHeap] Addr 0x{0:X} was not word aligned", _addr);
                _addr = _addr & 0xfffffffc;
                _addr += 4;
            }

            for (uint i = _addr; i < _addr + size; i += 4)
                CPU.WriteMemInt(i, init);

            Logging.WriteLine(LogLevel.Trace, "[KernelHeap] Allocating {0} bytes at 0x{1:X}", size, _addr);

            var addr = _addr;
            _addr += size;

            /*if (_addr >= 0x61000)
            {
                Logging.WriteLine(LogLevel.Panic, "[KernelHeap] Out of memory");
                while (true) ;
            }*/

            return addr;
        }

        public static void ExpandHeap(uint address, uint pages)
        {
            if (KernelAllocator != null) KernelAllocator.ExpandHeap(address, pages);
        }
    }
}
