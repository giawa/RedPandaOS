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
                    Used._array = Utilities.PtrToObject<uint[]>(this.Address + 4);  // 136 bytes (4 byte size of array, 4 byte size of elements, 32 entries)
                    CPU.WriteMemInt(this.Address + 4, 32);  // put the size of the array in the first 4 bytes of the array (as if created normally)
                    CPU.WriteMemInt(this.Address + 8, 4);   // put the size of each array element in the next 4 bytes of the array (as if created normally)

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
                        if (i >= (Used.Length << 5) || Used[i])
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

                return uint.MaxValue;
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

        private uint _startAddress = 0x61000;

        private SplitBumpHeap()
        {
            _memory = new List<PageAlignedHeap>(100);
            // allocate the first chunk of memory that is guaranteed available
            for (uint i = 0; i < 31; i++) _memory.Add(new PageAlignedHeap(_startAddress + i * 4096));
            //for (uint i = 0; i < 95; i++) _memory.Add(new PageAlignedHeap(_startAddress + i * 4096));
        }

        public void ExpandHeap(uint address, uint pages)
        {
            for (uint i = 0; i < pages; i++) _memory.Add(new PageAlignedHeap(address + i * 4096));
        }

        private uint MallocLarge(uint size, uint init = 0)
        {
            uint subsequentPages = (size >> 12) + 1;
            Logging.WriteLine(LogLevel.Warning, "Trying to allocate {0} subsequent pages", subsequentPages);

            for (int i = 0; i < _memory.Count; i++)
            {
                // we only do large allocations across full pages
                if (_memory[i].Available == 4096)
                {
                    uint consecutive = 1;

                    for (int j = i + 1; j < _memory.Count && consecutive < subsequentPages; j++)
                    {
                        if (_memory[j].Address != _memory[j - 1].Address + 4096) break;
                        if (_memory[j].Available != 4096) break;

                        consecutive++;
                    }

                    // if we found a large enough contiguous region then mark all of those pages as taken
                    if (consecutive >= subsequentPages)
                    {
                        var addr = _memory[i].Malloc(4096);
                        for (int j = 1; j < (int)subsequentPages; j++)
                        {
                            _memory[i + j].Malloc(4096);
                        }

                        /*if (_traces != null)
                        {
                            for (int j = 0; j < _traces.Count; j++) _traces[j].Trace(addr, size);
                        }*/

                        return addr;
                    }
                }
            }

            throw new Exception("Failed to allocate enough memory");
        }

        /*private List<TraceableHeap> _traces;

        public TraceableHeap AddTrace(TraceableHeap trace)
        {
            if (_traces == null) _traces = new List<TraceableHeap>();
            _traces.Add(trace);
            return trace;
        }

        public void RemoveTrace(TraceableHeap trace)
        {
            if (_traces == null) return;

            for (int i = 0; i < _traces.Count; i++)
            {
                if (_traces[i] == trace)
                {
                    _traces.RemoveAt(i);
                    break;
                }
            }
        }*/

        public void PrintSpace()
        {
            return;

            for (int i = 0; i < 10; i++)
                Logging.Write(LogLevel.Warning, " {0} has {1}", (uint)i, _memory[i].Available);
            Logging.WriteLine(LogLevel.Warning, "");
            Logging.WriteLine(LogLevel.Warning, "First at 1 is 0x{0:X}", (uint)_memory[1].Used.IndexOfFirstZero());
        }

        public void DebugPrint(string info)
        {
            Logging.Write(LogLevel.Warning, "[Debug] ");
            Logging.Write(LogLevel.Warning, info);
            Logging.WriteLine(LogLevel.Warning, " {0:X} {1:X} {2}", CPU.ReadMemInt(0x62000), CPU.ReadMemInt(0x62004), _memory[1].Available);
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

            if (size > 4096) return MallocLarge(size, init);

            for (int i = 0; i < _memory.Count; i++)
            {
                if (_memory[i].Available >= size)
                {
                    addr = _memory[i].Malloc(size);

                    if (addr == uint.MaxValue) continue;    // if we failed to allocate then try the next page
                    else break;
                }
            }

            if (addr == uint.MaxValue) throw new OutOfMemoryException("Could not find free section of memory");

            for (uint i = addr; i < addr + size; i += 4)
                CPU.WriteMemInt(i, init);

            Logging.WriteLine(LogLevel.Trace, "[SBH] Allocating {0} bytes at 0x{1:X}", size, addr);

            /*if (_traces != null)
            {
                for (int i = 0; i < _traces.Count; i++) _traces[i].Trace(addr, size);
            }*/

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
                    if (addr == uint.MaxValue) throw new Exception("Could not allocated page?");
                    break;
                }
            }

            if (addr == uint.MaxValue) throw new OutOfMemoryException("Could not find free section of memory");

            for (uint i = addr; i < addr + size; i += 4)
                CPU.WriteMemInt(i, init);

            Logging.WriteLine(LogLevel.Trace, "[SBH] Allocating {0} bytes at 0x{1:X}", size, addr);

            return addr;
        }

        public void Free<T>(T obj)
        {
            Free(Utilities.ObjectToPtr(obj), (uint)Marshal.SizeOf<T>());
        }

        public void Free(uint addr, uint size)
        {
            // make sure we're always allocating word aligned
            if ((size & 0x03) != 0)
            {
                size &= 0xfffffffc;
                size += 4;
            }

            if (size > 4096) throw new ArgumentOutOfRangeException("Invalid size");

            for (int i = 0; i < _memory.Count; i++)
            {
                var page = _memory[i];

                if (page.Address <= addr && page.Address + 4096 > addr)
                {
                    var offset = addr & 0xfff;

                    page.Free(offset, size);

                    /*if (_traces != null)
                    {
                        for (int j = 0; j < _traces.Count; j++)
                        {
                            _traces[j].Untrace(addr, size);
                        }
                    }*/

                    Logging.WriteLine(LogLevel.Trace, "[SBH] Freeing {0} bytes at 0x{1:X}", size, addr);
                    return;
                }
            }

            throw new Exception("Invalid address");
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

    public class TraceableHeap : IDisposable
    {
        private uint pid;

        public TraceableHeap()
        {
            _addr = new List<uint>(100);
            _size = new List<uint>(100);

            pid = Scheduler.CurrentTask.Id;
        }

        private List<uint> _addr;
        private List<uint> _size;

        public void Trace(uint addr, uint size)
        {
            if (Scheduler.CurrentTask.Id != pid) return;

            _addr.Add(addr);
            _size.Add(size);
        }

        public void Untrace(uint addr, uint size)
        {
            if (Scheduler.CurrentTask.Id != pid) return;

            int index = -1;
            for (int i = 0; i < _addr.Count; i++)
            {
                if (_addr[i] == addr)
                {
                    index = i;
                    break;
                }
            }

            if (index == -1) return;

            if (_size[index] != size) throw new Exception("Freed incorrect size");
            _size.RemoveAt(index);
            _addr.RemoveAt(index);
        }

        public void Dispose()
        {
            //KernelHeap.KernelAllocator.RemoveTrace(this);
            for (int i = 0; i < _addr.Count; i++)
            {
                KernelHeap.KernelAllocator.Free(_addr[i], _size[i]);
            }
            _addr.Clear();
            _size.Clear();
        }

        public void Free()
        {
            _addr.Dispose();
            _size.Dispose();
            KernelHeap.KernelAllocator.Free(_addr);
            KernelHeap.KernelAllocator.Free(_size);
            KernelHeap.KernelAllocator.Free(this);
        }
    }
}
