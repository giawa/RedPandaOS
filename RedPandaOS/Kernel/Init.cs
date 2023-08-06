using CPUHelper;
using Kernel.Devices;
using Kernel.Memory;
using Runtime.Collections;
using System;
using System.Threading.Tasks;

namespace Kernel
{
    public static class Init
    {
        static void EmptyIrq()
        {

        }

        private static void InitializePaging()
        {
            // mark unavailable chunks of memory as 'used' in the page allocator
            uint entries = CPU.ReadMemShort(0x504);

            List<Memory.SMAP_Entry> freeMemory = new List<Memory.SMAP_Entry>((int)entries);

            Logging.WriteLine(LogLevel.Trace, "Found {0} SMAP_Entry from stage2", entries);

            for (uint i = 0; i < entries; i++)
            {
                Memory.SMAP_Entry entry = Runtime.Memory.Utilities.PtrToObject<Memory.SMAP_Entry>(0x508 + 24 * i);
                if ((entry.Type & 1) == 1) freeMemory.Add(entry);
                //Logging.Write(LogLevel.Warning, "Region {0} {1:X}{2:X}", i, entry.BaseH, entry.BaseL);
                //Logging.WriteLine(LogLevel.Warning, " {0:X}{1:X} {2:X}", entry.LengthH, entry.LengthL, entry.Type);
            }

            // when initializing page we set up how many frames we will make available (each frame must be mapped somewhere in memory)
            // to start we'll support up to 128MiB of memory, which requires a 4kiB allocation of frames
            int frameCount = 128 * 1024 * 1024 / 4096;
            Memory.Paging.InitializePaging(frameCount, freeMemory);    // loads the entire kernel + heap into paging
        }

        static void SyscallHandler(uint t)
        {
            var bp = CPU.ReadEBP();

            uint ecx = CPU.ReadMemInt(bp + 12 * 4);

            switch (ecx)
            {
                case 1: // print character
                    uint addr = bp + 13 * 4;    // eax is at this position as pushed by the interrupt
                    COM.Write((byte)CPU.ReadMemInt(addr));
                    break;
                case 2: // get pid
                    uint pidLoc = bp + 13 * 4;
                    uint id = Scheduler.CurrentTask?.Id ?? 0;
                    CPU.WriteMemInt(pidLoc, id);
                    break;
                default:
                    Logging.WriteLine(LogLevel.Warning, "Unknown syscall {0}", ecx);
                    break;
            }
        }

        private static byte[] ReadFile(IO.File file)
        {
            byte[] data = new byte[file.Size];

            uint remaining = file.Size;
            int offset = 0;

            file.OnOpen(file);
            byte[] fileData = file.FileSystem.GetFirstSector(file);

            do
            {
                int toCopy = (remaining > 512 ? 512 : (int)remaining);

                for (int i = 0; i < toCopy; i++) data[offset + i] = fileData[i];
                offset += toCopy;
                remaining -= (uint)toCopy;

                if (remaining > 0) fileData = file.FileSystem.OnReadSector(file);

            } while (remaining > 0);

            return data;
        }

        private static void LoadSampleApp()
        {
            Logging.WriteLine(LogLevel.Warning, "Loading Sample App");

            var root = IO.Filesystem.Root;
            IO.Directory dev = null, hda = null, apps = null;
            for (int i = 0; i < root.Directories.Count; i++) if (root.Directories[i].Name == "dev") dev = root.Directories[i];
            for (int i = 0; i < dev.Directories.Count; i++) if (dev.Directories[i].Name == "hda") hda = dev.Directories[i];
            for (int i = 0; i < hda.Directories.Count; i++) if (string.Equals(hda.Directories[i].Name, "apps", StringComparison.CurrentCultureIgnoreCase)) apps = hda.Directories[i];

            var data = ReadFile(apps.Files[1]);

            // create a new page directory for this process before copying to memory
            PageDirectory samplePage = Paging.CloneDirectory(Paging.KernelDirectory);
            Paging.SwitchPageDirectory(samplePage);

            // create page to store the program
            var page = Paging.GetPage(0x400000, true, samplePage);
            var result = Paging.AllocateFrame(page, false, true);

            // copy the data to memory since we know the PE layout without processing it atm
            for (uint i = 0; i < 512; i ++)
            {
                CPU.WriteMemByte(0x400000 + i, data[i + 512]);   // offset by 512 to jump past the DOS/COFF/etc headers
            }

            Paging.SwitchPageDirectory(Paging.KernelDirectory);
            Logging.WriteLine(LogLevel.Trace, "Jumping to code");

            Scheduler.Task task = new Scheduler.Task(0x400000, samplePage);
            Scheduler.Add(task);

            // create a new page directory for this process before copying to memory
            PageDirectory anotherPage = Paging.CloneDirectory(Paging.KernelDirectory);
            Paging.SwitchPageDirectory(anotherPage);

            // create page to store the program
            var page2 = Paging.GetPage(0x400000, true, anotherPage);
            var result2 = Paging.AllocateFrame(page2, false, true);

            // copy the data to memory since we know the PE layout without processing it atm
            for (uint i = 0; i < 512; i++)
            {
                CPU.WriteMemByte(0x400000 + i, data[i + 512]);   // offset by 512 to jump past the DOS/COFF/etc headers
            }

            Paging.SwitchPageDirectory(Paging.KernelDirectory);
            Logging.WriteLine(LogLevel.Trace, "Jumping to code");

            Scheduler.Task task2 = new Scheduler.Task(0x400000, anotherPage);
            Scheduler.Add(task2);



            Scheduler.UseScheduler = true;
            while (true) ;
        }

        static void PrintStack(uint t)
        {
            Logging.WriteLine(LogLevel.Trace, "General Protection Fault");
            Kernel.Exceptions.PrintStackTrace();

            while (true) ;
        }

        static void SetupTSS()
        {
            // try setting up TSS by getting its address from the stage 2 boot loader
            var tssAddress = CPU.ReadMemShort(0x502);
            var tss = Runtime.Memory.Utilities.PtrToObject<CPU.TSSEntryWrapper>(tssAddress);

            tss.ss0 = 0x10;
            tss.esp0 = 0x7E00;  // use bootloader stage 1 as the new kernel stack for the TSS
            PIC.InterruptStackStart = tss.esp0;
            CPU.FlushTSS();
        }

        static void Start()
        {
            //Logging.LoggingLevel = LogLevel.Trace;
            
            COM.Initialize();
            SetupTSS();

            KernelHeap.KernelAllocator = BumpHeap.Instance;

            PIC.SetIrqCallback(14, EmptyIrq);
            PIC.SetIrqCallback(32, EmptyIrq);
            PIC.Init();
            PIC.SetIrqCallback(0, PIT.Tick);

            PIC.SetIsrCallback(13, PrintStack);

            PIC.SetIsrCallback(31, SyscallHandler);   // for now this is our only "system call", which prints a character stored in EAX to the COM port

            PIT.Init(100);

            PCI.ScanBus();

            InitializePaging();

            IO.Filesystem.Init();
            PATA.AttachDriver();    // needs PIT.Tick attached to irq 0 for Thread.Sleep

            LoadSampleApp();

            while (true) ;
        }
    }
}
