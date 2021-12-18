using Kernel.Devices;
using System.Runtime.InteropServices;
using Runtime.Collections;

namespace Kernel.Memory
{
    public class Page
    {
        public uint Layout;

        public bool Present 
        { 
            get { return (Layout & 0x01) != 0; } 
            set { Layout = (Layout & ~0x01U) | (value ? 0x01U : 0x00U); }
        }

        public bool ReadWrite 
        { 
            get { return (Layout & 0x02) != 0; }
            set { Layout = (Layout & ~0x02U) | (value ? 0x02U : 0x00U); }
        }

        public bool User 
        { 
            get { return (Layout & 0x04) != 0; }
            set { Layout = (Layout & ~0x04U) | (value ? 0x04U : 0x00U); }
        }

        public bool Accessed 
        { 
            get { return (Layout & 0x08) != 0; }
            set { Layout = (Layout & ~0x08U) | (value ? 0x08U : 0x00U); }
        }

        public bool Dirty 
        { 
            get { return (Layout & 0x10) != 0; }
            set { Layout = (Layout & ~0x10U) | (value ? 0x10U : 0x00U); }
        }

        public byte Unused { get { return (byte)((Layout >> 5) & 0x7f); } }

        public uint Frame 
        { 
            get { return Layout >> 12; } 
            set { Layout = (Layout & 0x00000fffU) | (value << 12); }
        }
    }

    public class PageTable
    {
        public Page GetPage(uint num)
        {
            var baseAddr = BumpHeap.ObjectToPtr(this);
            baseAddr += num * (uint)Marshal.SizeOf<Page>();
            return BumpHeap.PtrToObject<Page>(baseAddr);
        }
    }

    public class PageDirectory
    {
        public PageTable GetPageTable(uint num)
        {
            var baseAddr = BumpHeap.ObjectToPtr(this);
            baseAddr += num * (uint)Marshal.SizeOf<PageTable>();
            return BumpHeap.PtrToObject<PageTable>(CPUHelper.CPU.ReadMemInt(baseAddr));
        }

        public void SetPageTable(uint num, PageTable pageTable)
        {
            var baseAddr = BumpHeap.ObjectToPtr(this);
            baseAddr += num * (uint)Marshal.SizeOf<PageTable>();
            CPUHelper.CPU.WriteMemInt(baseAddr, BumpHeap.ObjectToPtr(pageTable));
        }

        public uint GetTableAddress(uint num)
        {
            var baseAddr = BumpHeap.ObjectToPtr(this);
            baseAddr += 1024 * (uint)Marshal.SizeOf<PageTable>();
            baseAddr += (num << 2);
            return CPUHelper.CPU.ReadMemInt(baseAddr);
        }

        public void SetTableAddress(uint num, uint value)
        {
            var baseAddr = BumpHeap.ObjectToPtr(this);
            baseAddr += 1024 * (uint)Marshal.SizeOf<PageTable>();
            baseAddr += (num << 2);
            CPUHelper.CPU.WriteMemInt(baseAddr, value);
        }

        public uint GetTableAddressesOffset()
        {
            var baseAddr = BumpHeap.ObjectToPtr(this);
            baseAddr += 1024 * (uint)Marshal.SizeOf<PageTable>();
            return baseAddr;
        }

        // note: This seems like a major waste because this un-page aligns this whole object
        // maybe we should have an array of PageDirectory addresses somewhere instead
        public uint PhysicalAddress
        {
            get
            {
                var baseAddr = BumpHeap.ObjectToPtr(this);
                baseAddr += 1024 * (uint)Marshal.SizeOf<PageTable>();
                baseAddr += 1024 * 4;
                return CPUHelper.CPU.ReadMemInt(baseAddr);
            }
            set
            {
                var baseAddr = BumpHeap.ObjectToPtr(this);
                baseAddr += 1024 * (uint)Marshal.SizeOf<PageTable>();
                baseAddr += 1024 * 4;
                CPUHelper.CPU.WriteMemInt(baseAddr, value);
            }
        }
    }

    public static class Paging
    {
        private static BitArray _frames;
        private static PageDirectory _kernelDirectory, _currentDirectory;

        public static void InitializePaging(int frameCount)
        {
            if (_frames != null)
            {
                VGA.WriteString("Paging was already initialized");
                return;
            }

            // get all the non-aligned memory allocation out of the way
            _frames = new BitArray(frameCount);
            PIC.SetIdtCallback(14, PageFault);  // allocates an Action

            var directoryAddr = BumpHeap.MallocPageAligned(4 * 1024 * 2 + 4);
            _kernelDirectory = BumpHeap.PtrToObject<PageDirectory>(directoryAddr);
            _kernelDirectory.PhysicalAddress = directoryAddr;

            uint addr = 0;

            while (addr < 0xFFFFFU) // extend all the way to 0xFFFFF to cover VGA address range, etc
            {
                var result = AllocateFrame(GetPage(addr, true, _kernelDirectory), true, true);
                if (result == -1)
                {
                    VGA.WriteString("Could not allocate frame at address: 0x");
                    VGA.WriteHex(addr);
                    while (true) ;
                }
                addr += 0x1000U;
            }

            SwitchPageDirectory(_kernelDirectory);
        }

        private static Page _emptyPage = new Page();

        public static Page GetPage(uint address, bool make, PageDirectory dir)
        {
            // turn address into index
            address = (address >> 12);

            uint tableIndex = address >> 10;

            if (dir.GetPageTable(tableIndex) != null)
            {
                return dir.GetPageTable(tableIndex).GetPage(address & 0x3ff);
            }
            else if (make)
            {
                uint newTableAddress = BumpHeap.MallocPageAligned(4096);
                PageTable newTable = BumpHeap.PtrToObject<PageTable>(newTableAddress);
                dir.SetPageTable(tableIndex, newTable);
                dir.SetTableAddress(tableIndex, newTableAddress | 0x7U);
                return newTable.GetPage(address & 0x3ff);
            }
            else return _emptyPage;
        }

        public static void SwitchPageDirectory(PageDirectory dir)
        {
            _currentDirectory = dir;

            // the table addresses beging after the page tables, which is 4096 bytes into this object
            CPUHelper.CPU.SetPageDirectory(dir.PhysicalAddress + 4096U);
        }

        public static int AllocateFrame(Page page, bool isKernel, bool isWriteable)
        {
            if (page.Frame != 0) return 0;

            int idx = _frames.IndexOfFirstZero();
            if (idx == -1)
            {
                VGA.WriteString("No free frame found");
                VGA.WriteLine();
                return -1;
            }
            _frames[idx] = true;

            page.Present = true;
            page.ReadWrite = isWriteable;
            page.User = !isKernel;
            page.Frame = (uint)idx;

            return 0;
        }

        public static void FreeFrame(Page page)
        {
            if (page.Frame == 0) return;

            _frames[(int)page.Frame] = false;
            page.Frame = 0;
        }

        public static void PageFault()
        {
            var addr = CPUHelper.CPU.ReadCR2();

            VGA.WriteString("Got page fault interrupt at address 0x");
            VGA.WriteHex(addr);

            while (true) ;
        }
    }
}
