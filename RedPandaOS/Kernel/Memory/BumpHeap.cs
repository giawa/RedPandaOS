using CPUHelper;
using IL2Asm.BaseTypes;
using System.Runtime.InteropServices;

namespace Kernel.Memory
{
    public class BumpHeap : IHeapAllocator
    {
        private static BumpHeap _instance;

        public static BumpHeap Instance
        {
            get 
            { 
                if (_instance == null) _instance = new BumpHeap();
                return _instance; 
            }
        }

        private BumpHeap()
        {
            _addr = 0x21000;
        }

        private uint _addr = 0x21000;    // the start of the bump heap

        [Allocator]
        public uint Malloc(uint size, uint init = 0)
        {
            if ((_addr & 0x03) != 0)
            {
                Logging.WriteLine(LogLevel.Trace, "Addr 0x{0:X} was not word aligned", _addr);
                _addr = _addr & 0xfffffffc;
                _addr += 4;
            }

            for (uint i = _addr; i < _addr + size; i += 4)
                CPU.WriteMemInt(i, init);

            Logging.WriteLine(LogLevel.Trace, "Allocating {0} bytes at 0x{1:X}", size, _addr);

            var addr = _addr;
            _addr += size;
            return addr;
        }

        [Allocator]
        public uint MallocPageAligned(uint size, uint init = 0)
        {
            if ((_addr & 0xfff) != 0)
            {
                Logging.WriteLine(LogLevel.Trace, "Addr 0x{0:X} was not page aligned", _addr);
                _addr = _addr & 0xfffff000;
                _addr += 0x1000;
            }

            for (uint i = _addr; i < _addr + size; i += 4)
                CPU.WriteMemInt(i, init);

            Logging.WriteLine(LogLevel.Trace, "Allocating {0} bytes at 0x{1:X}", size, _addr);

            var addr = _addr;
            _addr += size;
            return addr;
        }

        public void Free(uint addr, uint size)
        {
            // nop for now
        }

        public T Malloc<T>()
        {
            uint size = (uint)Marshal.SizeOf<T>();
            uint addr = Malloc(size);

            return Utilities.PtrToObject<T>(addr);
        }

        public T Malloc<T>(uint size)
        {
            uint addr = Malloc(size);

            return Utilities.PtrToObject<T>(addr);
        }

        public T[] MallocArray<T>(uint arraySize)
        {
            uint size = (uint)Marshal.SizeOf<T>();
            size *= arraySize;
            uint addr = Malloc(size);

            return Utilities.PtrToObject<T[]>(addr);
        }

        public void Free<T>(T obj)
        {
            Free(Utilities.ObjectToPtr(obj), (uint)Marshal.SizeOf<T>());
        }

        public void ExpandHeap(uint address, uint pages)
        {
            throw new System.Exception("Unsupported");
            //for (uint i = 0; i < pages; i++) _memory.Add(new PageAlignedHeap(address + i * 4096));
        }
    }
}
