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
        public uint PhysicalAddress;
        public FixedArray<PageTable> PageTables;
        public FixedArray<uint> TableAddresses;

        public PageDirectory()
        {
            PageTables = new FixedArray<PageTable>(1024, 1);
            TableAddresses = new FixedArray<uint>(1024, 1);
            PhysicalAddress = TableAddresses.AddressOfArray;
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
            PIC.SetIsrCallback(14, PageFault);  // allocates an Action

            _kernelDirectory = new PageDirectory();

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

            if (dir.PageTables[tableIndex] != null)
            {
                return dir.PageTables[tableIndex].GetPage(address & 0x3ff);
            }
            else if (make)
            {
                uint newTableAddress = BumpHeap.MallocPageAligned(4096);
                PageTable newTable = BumpHeap.PtrToObject<PageTable>(newTableAddress);
                dir.PageTables[tableIndex] = newTable;
                dir.TableAddresses[tableIndex] = newTableAddress | 0x7U;
                return newTable.GetPage(address & 0x3ff);
            }
            else return _emptyPage;
        }

        public static void SwitchPageDirectory(PageDirectory dir)
        {
            _currentDirectory = dir;

            CPUHelper.CPU.SetPageDirectory(dir.PhysicalAddress);
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
