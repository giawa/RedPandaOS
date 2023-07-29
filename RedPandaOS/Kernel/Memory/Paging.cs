using Kernel.Devices;
using System.Runtime.InteropServices;
using Runtime.Collections;
using System;

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
            var baseAddr = Utilities.ObjectToPtr(this);
            baseAddr += num * (uint)Marshal.SizeOf<IntPtr>();
            return Utilities.PtrToObject<Page>(baseAddr);
        }
    }

    public class PageDirectory
    {
        public uint PhysicalAddress;
        public FixedArrayPtr<PageTable> PageTables;
        public FixedArray<uint> TableAddresses;

        public PageDirectory()
        {
            PageTables = new FixedArrayPtr<PageTable>(1024, 1);
            TableAddresses = new FixedArray<uint>(1024, 1);
            PhysicalAddress = TableAddresses.AddressOfArray;
        }

        public void Free()
        {
            for (int i = 0; i < 1024; i++)
            {
                if (PageTables[i] == null) continue;

                if (Paging.KernelDirectory.PageTables[i] == PageTables[i]) continue;
                else
                {
                    FreeTable(PageTables[i]);
                }
            }

            if (PageTables != null) PageTables.Free();
            if (TableAddresses != null) TableAddresses.Free();

            if (PageTables != null) KernelHeap.KernelAllocator.Free(Utilities.ObjectToPtr(PageTables), 8);
            if (TableAddresses != null) KernelHeap.KernelAllocator.Free(Utilities.ObjectToPtr(TableAddresses), 8);

            PageTables = null;
            TableAddresses = null;
            PhysicalAddress = 0;
        }

        private void FreeTable(PageTable pageTable)
        {
            for (uint i = 0; i < 1024; i++)
            {
                var page = pageTable.GetPage(i);
                if (page == null) continue;

                Paging.FreeFrame(pageTable.GetPage(i));
            }

            // finally free the page table that was allocated
            KernelHeap.KernelAllocator.Free(Utilities.ObjectToPtr(pageTable), 4096);
        }
    }

    public static class Paging
    {
        private static BitArray _frames;
        private static PageDirectory _kernelDirectory, _currentDirectory;

        public static PageDirectory KernelDirectory { get { return _kernelDirectory; } }

        public static PageDirectory CurrentDirectory { get { return _currentDirectory; } set { _currentDirectory = value; } }

        public static PageDirectory CloneDirectory(PageDirectory original)
        {
            PageDirectory clone = new PageDirectory();

            for (int i = 0; i < 1024; i++)
            {
                if (original.PageTables[i] == null) continue;

                if (_kernelDirectory.PageTables[i] == original.PageTables[i])
                {
                    clone.PageTables[i] = _kernelDirectory.PageTables[i];
                    clone.TableAddresses[i] = _kernelDirectory.TableAddresses[i];
                }
                else
                {
                    uint newTableAddress = KernelHeap.KernelAllocator.MallocPageAligned(4096);
                    PageTable newTable = Utilities.PtrToObject<PageTable>(newTableAddress);
                    CopyTable(original.PageTables[i], newTable);
                    clone.PageTables[i] = newTable;
                    clone.TableAddresses[i] = newTableAddress | 0x7U;
                }
            }

            return clone;
        }

        private static PageTable CopyTable(PageTable source, PageTable destination)
        {
            for (uint i = 0; i < 1024; i++)
            {
                var page = source.GetPage(i);
                if (page == null) continue;

                AllocateFrame(destination.GetPage(i), false, false);
                // copy the flags from destination to source
                var layout = destination.GetPage(i).Layout & 0xfffff000U;
                destination.GetPage(i).Layout = layout | (page.Layout & 0xfffU);

                CopyPage(source.GetPage(i).Frame * 0x1000, destination.GetPage(i).Frame * 0x1000);
            }

            return destination;
        }

        [IL2Asm.BaseTypes.AsmMethod]
        private static void CopyPage(uint source, uint destination)
        {

        }

        [IL2Asm.BaseTypes.AsmPlug("Kernel_Memory_Paging_CopyPage_Void_U4_U4", IL2Asm.BaseTypes.Architecture.X86, IL2Asm.BaseTypes.AsmFlags.None)]
        private static void CopyPageAsm(IL2Asm.BaseTypes.IAssembledMethod assembly)
        {
            assembly.AddAsm("push ecx");
            assembly.AddAsm("push esi");
            assembly.AddAsm("push edi");
            assembly.AddAsm("pushf");
            assembly.AddAsm("cli");
            assembly.AddAsm("mov esi, [esp + 24]"); // source address
            assembly.AddAsm("mov edi, [esp + 20]"); // destination address

            assembly.AddAsm("mov edx, cr0");
            assembly.AddAsm("and edx, 0x7fffffff"); // clear paging bit
            assembly.AddAsm("mov cr0, edx");        // disable paging

            assembly.AddAsm("mov ecx, 1024");
            assembly.AddAsm("rep movsd");

            assembly.AddAsm("mov edx, cr0");
            assembly.AddAsm("or edx, 0x80000000");  // enable paging bit
            assembly.AddAsm("mov cr0, edx");        // enable paging

            assembly.AddAsm("popf");
            assembly.AddAsm("pop edi");
            assembly.AddAsm("pop esi");
            assembly.AddAsm("pop ecx");
            assembly.AddAsm("ret 8");   // pop the source and destination arguments
        }

        [IL2Asm.BaseTypes.AsmMethod]
        public static void FlushTLB()
        {

        }

        [IL2Asm.BaseTypes.AsmPlug("Kernel_Memory_Paging_FlushTLB_Void", IL2Asm.BaseTypes.Architecture.X86, IL2Asm.BaseTypes.AsmFlags.Inline)]
        private static void FlushTLBAsm(IL2Asm.BaseTypes.IAssembledMethod assembly)
        {
            assembly.AddAsm("mov eax, cr3");
            assembly.AddAsm("mov cr3, eax");
        }

        private static void MarkFramesUnavailable(int frameCount, List<SMAP_Entry> freeMemory)
        {
            for (int i = 0; i < frameCount; i++)
            {
                bool isAvailable = false;
                for (int j = 0; j < freeMemory.Count && !isAvailable; j++)
                {
                    if (freeMemory[j].ContainsFrame((uint)i)) isAvailable = true;
                }

                if (!isAvailable) _frames[i] = true;
            }
        }

        public static void InitializePaging(int frameCount, List<SMAP_Entry> freeMemory)
        {
            if (_frames != null)
            {
                Logging.WriteLine(LogLevel.Warning, "Paging was already initialized");
                return;
            }

            // get all the non-aligned memory allocation out of the way
            _frames = new BitArray(frameCount);
            PIC.SetIsrCallback(14, PageFault);  // allocates an Action

            _kernelDirectory = new PageDirectory();

            uint addr = 0;

            while (addr < 0xBFFFFU) // extend all the way to 0xFFFFF to cover VGA address range, etc
            {
                var page = GetPage(addr, true, _kernelDirectory);
                var result = AllocateFrame(page, true, true);
                if (result == -1)
                {
                    Logging.WriteLine(LogLevel.Panic, "Could not allocate frame at address 0x{0:X}", addr);
                    while (true) ;
                }
                addr += 0x1000U;
            }

            // check for PCI devices that need their memory identity mapped
            uint end = 0;
            for (int i = 0; i < PCI.Devices.Count; i++)
            {
                var device = PCI.Devices[i];

                if ((device.BAR0 & 0x1) == 0 && device.BAR0 != 0)
                {
                    addr = device.BAR0 & 0xFFFFFFF0U;
                    end = addr + device.BAR0Size;

                    while (addr < end)
                    {
                        var page = GetPage(addr, true, _kernelDirectory);
                        var result = AllocateFrame(page, addr >> 12, true, true);
                        if (result == -1)
                        {
                            Logging.WriteLine(LogLevel.Panic, "Could not allocate frame at address 0x{0:X}", addr);
                            while (true) ;
                        }
                        addr += 0x1000U;
                    }
                    //Logging.WriteLine(LogLevel.Warning, "Identity mapping {0:X} to {1:X}", addr, end);
                }

                if ((device.BAR1 & 0x1) == 0 && device.BAR1 != 0)
                {
                    addr = device.BAR1 & 0xFFFFFFF0U;
                    end = addr + device.BAR1Size;

                    while (addr < end)
                    {
                        var page = GetPage(addr, true, _kernelDirectory);
                        var result = AllocateFrame(page, addr >> 12, true, true);
                        if (result == -1)
                        {
                            Logging.WriteLine(LogLevel.Panic, "Could not allocate frame at address 0x{0:X}", addr);
                            while (true) ;
                        }
                        addr += 0x1000U;
                    }
                    //Logging.WriteLine(LogLevel.Warning, "Identity mapping {0:X} to {1:X}", addr, end);
                }
            }

            //_currentDirectory = CloneDirectory(_kernelDirectory);
            SwitchPageDirectory(_kernelDirectory);

            MarkFramesUnavailable(frameCount, freeMemory);
        }

        public static void Draw()
        {
            _frames.Draw();
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
                uint newTableAddress = KernelHeap.KernelAllocator.MallocPageAligned(4096);
                PageTable newTable = Utilities.PtrToObject<PageTable>(newTableAddress);
                dir.PageTables[tableIndex] = newTable;
                dir.TableAddresses[tableIndex] = newTableAddress | 0x7U;
                return newTable.GetPage(address & 0x3ff);
            }
            else return _emptyPage;
        }

        public static void SwitchPageDirectoryFast(PageDirectory dir)
        {
            _currentDirectory = dir;

            CPUHelper.CPU.SetPageDirectoryFast(dir.PhysicalAddress);
        }

        public static void SwitchPageDirectory(PageDirectory dir)
        {
            _currentDirectory = dir;

            CPUHelper.CPU.SetPageDirectory(dir.PhysicalAddress);
        }

        public static int AllocateFrame(Page page, uint frame, bool isKernel, bool isWriteable)
        {
            if (page.Frame != 0) return 0;

            page.Present = true;
            page.ReadWrite = isWriteable;
            page.User = !isKernel;
            page.Frame = frame;

            return 0;
        }

        public static int AllocateFrame(Page page, bool isKernel, bool isWriteable)
        {
            if (page.Frame != 0) return 0;

            int idx = _frames.IndexOfFirstZero();
            if (idx == -1)
            {
                Logging.WriteLine(LogLevel.Error, "No free frame found");
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

        public static void PageFault(uint error_code)
        {
            var addr = CPUHelper.CPU.ReadCR2();

            Logging.WriteLine(LogLevel.Panic, "Got page fault at address 0x{0:X}", addr);
            Exceptions.PrintStackTrace();
            while (true) ;

            if (addr >= 0xA00000 && addr <= 0xAFFFFF)
            {
                addr = 0;
                while (addr < 0xFFFFFU) // extend all the way to 0xFFFFF to cover VGA address range, etc
                {
                    var page = GetPage(0xA00000U | addr, true, _kernelDirectory);
                    var result = AllocateFrame(page, (addr >> 12), true, true);
                    if (result == -1)
                    {
                        Logging.WriteLine(LogLevel.Panic, "Could not allocate frame at address 0x{0:X}", addr);
                        while (true) ;
                    }
                    addr += 0x1000U;
                }
            }
            else
            {
                Logging.WriteLine(LogLevel.Panic, "Got page fault at address 0x{0:X}", addr);

                while (true) ;
            }
        }
    }
}
