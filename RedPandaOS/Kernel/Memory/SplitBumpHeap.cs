using CPUHelper;
using IL2Asm.BaseTypes;
using Runtime.Collections;
using System;
using System.Runtime.InteropServices;

namespace Kernel.Memory
{
    public class SplitBumpHeap : IHeapAllocator
    {
        public class PageAlignedHeap
        {
            public uint Address { get; private set; }

            public BitArray Used { get; private set; }

            public uint Free { get; private set; }

            public PageAlignedHeap(uint address)
            {
                Address = address;
                Free = 4096;
            }

            public uint Malloc(uint size)
            {
                if (size > 4096) throw new Exception("Object allocation was larger than available.");

                // if a full page then just return this whole thing
                if (size == 4096)
                {
                    Free = 0;
                    return Address;
                }

                // make sure we're always allocating word aligned
                if ((size & 0x03) != 0)
                {
                    size &= 0xfffffffc;
                    size += 4;
                }

                if (Used == null)
                {
                    // we can't use a normal 'new' here because it would have to allocate with the kernel alloc
                    // so instead we can place the memory in this page at the very start on demand
                    // this means manually populating the data structure in the method it expects
                    // byte 0->3: ptr to the _array
                    // byte 4->7: length of the _array
                    // byte 8->135: the actual contents of _array
                    Used = Utilities.PtrToObject<BitArray>(this.Address);   // 4 bytes (pointer to the array)
                    Used._array = Utilities.PtrToObject<uint[]>(this.Address + 4);  // 136 bytes (4 byte size of array, 4 byte size of elements, 128 entries)
                    CPU.WriteMemInt(this.Address + 4, 128); // put the size of the array in the first 4 bytes of the array (as if created normally)
                    CPU.WriteMemInt(this.Address + 8, 1);   // put the size of each array element in the next 4 bytes of the array (as if created normally)

                    for (uint i = 3; i < 35; i++)
                        CPU.WriteMemInt(this.Address + (i << 2), 0);
                    //Array.Clear(Used._array, 0, Used._array.Length);
                    
                    for (int i = 0; i < 140 / 4; i++) Used[i] = true;
                    Free = 4096 - 140;
                }

                if (size > Free) throw new Exception("Object allocation was larger than available.");

                var newAddr = Used.IndexOfFirstZero();
                for (int i = newAddr; i < newAddr + (int)size / 4; i++)
                    Used[i] = true;

                Free -= size;

                return (uint)newAddr * 4 + Address;
            }
        }

        private static SplitBumpHeap _instance;

        public static SplitBumpHeap Instance
        {
            get
            {
                if (_instance == null) _instance = new SplitBumpHeap();
                return _instance;
            }
        }

        private List<PageAlignedHeap> _memory;

        private SplitBumpHeap()
        {
            _memory = new List<PageAlignedHeap>(95);
            for (uint i = 0; i < 95; i++) _memory.Add(new PageAlignedHeap(0x21000 + i * 4096));
        }

        [Allocator]
        public uint Malloc(uint size, uint init = 0)
        {
            uint addr = uint.MaxValue;

            // make sure we're always allocating word aligned
            if ((size & 0x03) != 0)
            {
                size &= 0xfffffffc;
                size += 4;
            }

            for (int i = 0; i < _memory.Count; i++)
            {
                if (_memory[i].Free >= size)
                {
                    addr = _memory[i].Malloc(size);
                    break;
                }
            }

            if (addr == uint.MaxValue) throw new Exception("Could not find free section of memory");

            for (uint i = addr; i < addr + size; i += 4)
                CPU.WriteMemInt(i, init);

            Logging.WriteLine(LogLevel.Trace, "[SBH] Allocating {0} bytes at 0x{1:X}", size, addr);

            return addr;
        }

        [Allocator]
        public uint MallocPageAligned(uint size, uint init = 0)
        {
            uint addr = uint.MaxValue;

            for (int i = 0; i < _memory.Count; i++)
            {
                if (_memory[i].Free >= 4096)
                {
                    addr = _memory[i].Malloc(size);
                    break;
                }
            }

            for (uint i = addr; i < addr + size; i += 4)
                CPU.WriteMemInt(i, init);

            Logging.WriteLine(LogLevel.Trace, "[SBH] Allocating {0} bytes at 0x{1:X}", size, addr);

            return addr;
        }

        public void Free(uint addr)
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
    }
}
