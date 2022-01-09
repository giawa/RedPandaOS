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

            public uint Available { get; private set; }

            public PageAlignedHeap(uint address)
            {
                Address = address;
                Available = 4096;
            }

            public void Free(uint offset, uint size)
            {
                if (size == 4096)
                {
                    Used = null;
                    Available = 4096;
                }
                else
                {
                    // make sure we're always allocating word aligned
                    if ((size & 0x03) != 0)
                    {
                        size &= 0xfffffffc;
                        size += 4;
                    }

                    uint loopTo = (offset >> 2) + (size >> 2);

                    for (uint i = (offset >> 2); i < loopTo; i++)
                        Used[(int)i] = false;

                    Available += size;
                }
            }

            public uint Malloc(uint size)
            {
                if (size > 4096) throw new Exception("Object allocation was larger than available.");

                // if a full page then just return this whole thing
                if (size == 4096)
                {
                    Available = 0;
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
                    Available = 4096 - 140;
                }

                if (size > Available) throw new Exception("Object allocation was larger than available.");

                int newAddr = 0;

                while (newAddr < 1024)
                {
                    newAddr = Used.IndexOfNextZero(newAddr);

                    if (newAddr == -1) break;

                    // make sure we actually have enough consecutive free words
                    bool works = true;
                    for (int i = newAddr; i < newAddr + (int)(size >> 2); i++)
                    {
                        if (Used[i])
                        {
                            works = false;
                            newAddr = i;
                            break;
                        }
                    }

                    // if not enough consecutive words then keep searching
                    if (!works) continue;

                    // otherwise we found a contiguous memory region, so mark it as in use
                    for (int i = newAddr; i < newAddr + (int)size / 4; i++)
                        Used[i] = true;

                    Available -= size;

                    return (uint)newAddr * 4 + Address;
                }

                return 0;
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

        private uint _startAddress = 0x21000;

        private SplitBumpHeap()
        {
            _memory = new List<PageAlignedHeap>(95);
            for (uint i = 0; i < 95; i++) _memory.Add(new PageAlignedHeap(_startAddress + i * 4096));
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
                if (_memory[i].Available >= size)
                {
                    addr = _memory[i].Malloc(size);
                    if (addr == 0) continue;    // if we failed to allocate then try the next page
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
                if (_memory[i].Available >= 4096)
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

        public void Free(uint addr, uint size)
        {
            var internalAddress = (addr - _startAddress) >> 12;
            if (internalAddress >= (uint)_memory.Count) throw new ArgumentOutOfRangeException("Invalid address");

            // make sure we're always allocating word aligned
            if ((size & 0x03) != 0)
            {
                size &= 0xfffffffc;
                size += 4;
            }

            if (size > 4096) throw new ArgumentOutOfRangeException("Invalid size");

            var page = _memory[(int)internalAddress];
            var offset = addr & 0xfff;

            page.Free(offset, size);

            Logging.WriteLine(LogLevel.Trace, "[SBH] Freeing {0} bytes at 0x{1:X}", size, addr);
        }

        public void Free<T>(T[] array)
        {
            var baseAddr = Utilities.ObjectToPtr(array);
            var size = CPU.ReadMemInt(baseAddr);
            var elementSize = CPU.ReadMemInt(baseAddr + 4);
            Logging.WriteLine(LogLevel.Trace, "[SBH] Got size {0} elementSize {1}", size, elementSize);
            Free(baseAddr, size * elementSize + 8);
        }

        public void Free(string s)
        {
            var baseAddr = Utilities.ObjectToPtr(s);
            var size = CPU.ReadMemInt(baseAddr);
            Free(baseAddr, size * 2 + 8);
        }

        public uint UsedBytes
        {
            get
            {
                uint used = 0;
                for (int i = 0; i < _memory.Count; i++)
                {
                    used += (4096 - _memory[i].Available);
                }
                return used;
            }
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
