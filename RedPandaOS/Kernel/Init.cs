using Bootloader;
using CPUHelper;
using IL2Asm.BaseTypes;
using Kernel.Devices;
using Kernel.Memory;
using Runtime;
using Runtime.Collections;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Kernel
{
    public static class SampleProcess
    {
        public static void PandaMain(string[] args)
        {
            string temp = "Hello from PandaMain\n";
            string hack = "Pwned";

            for (int i = 0; i < temp.Length; i++) WriteCharToStdOutSysCall(temp[i]);

            //for (int i = 0; i < 1000000; i++) ;

            //CPU.Cli();

            // try to read an address we wrote in the kernel address space
            // this should result in a segmentation fault
            uint t = CPU.ReadMemInt(0x9000);

            //Logging.WriteLine(LogLevel.Trace, "hack: {0}", Kernel.Memory.Utilities.ObjectToPtr(hack));
            //WriteHex(Kernel.Memory.Utilities.ObjectToPtr(hack));

            // if we make it here then the OS didn't configure memory properly
            for (int i = 0; i < hack.Length; i++) WriteCharToStdOutSysCall(hack[i]);

            while (true) ;
        }

        public static void WriteHex(uint value)
        {
            for (int i = 7; i >= 0; i--)
            {
                WriteHexChar((byte)(value >> (4 * i)));
            }
        }

        public static void WriteHex(short value)
        {
            WriteHexChar(value >> 12);
            WriteHexChar(value >> 8);
            WriteHexChar(value >> 4);
            WriteHexChar(value);
        }

        public static void WriteHex(byte value)
        {
            WriteHexChar(value >> 4);
            WriteHexChar(value);
        }

        public static void WriteHexChar(int value)
        {
            value &= 0x0f;
            if (value >= 10) WriteCharToStdOutSysCall((char)(value + 55));
            else WriteCharToStdOutSysCall((char)(value + 48));
        }

        [AsmMethod]
        public static void WriteCharToStdOutSysCall(char c)
        {
            Console.Write(c);
        }

        [AsmPlug("Kernel_SampleProcess_WriteCharToStdOutSysCall_Void_Char", IL2Asm.BaseTypes.Architecture.X86)]
        public static void TempSyscallAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax"); // grab char
            assembly.AddAsm("int 31");// temporary print char interrupt
        }
    }

    public static class Init
    {
        private const string _welcomeMessage = "Hello from C#!";

        //private static SMAP_entry _entry;
        private static CPU.CPUIDValue cpuId;

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
                Memory.SMAP_Entry entry = Memory.Utilities.PtrToObject<Memory.SMAP_Entry>(0x508 + 24 * i);
                if ((entry.Type & 1) == 1) freeMemory.Add(entry);
                //Logging.Write(LogLevel.Warning, "Region {0} {1:X}{2:X}", i, entry.BaseH, entry.BaseL);
                //Logging.WriteLine(LogLevel.Warning, " {0:X}{1:X} {2:X}", entry.LengthH, entry.LengthL, entry.Type);
            }

            // when initializing page we set up how many frames we will make available (each frame must be mapped somewhere in memory)
            // to start we'll support up to 128MiB of memory, which requires a 4kiB allocation of frames
            int frameCount = 128 * 1024 * 1024 / 4096;
            Memory.Paging.InitializePaging(frameCount, freeMemory);    // loads the entire kernel + heap into paging
        }

        private static void ReadSymbols()
        {
            var root = IO.Filesystem.Root;
            IO.Directory dev = null, hda = null, boot = null;
            for (int i = 0; i < root.Directories.Count; i++) if (root.Directories[i].Name == "dev") dev = root.Directories[i];
            if (dev == null) return;
            for (int i = 0; i < dev.Directories.Count; i++) if (dev.Directories[i].Name == "hda") hda = dev.Directories[i];
            if (hda == null) return;
            hda.OnOpen(hda);
            for (int i = 0; i < hda.Directories.Count; i++) if (hda.Directories[i].Name == "BOOT") boot = hda.Directories[i];
            if (boot == null) return;
            boot.OnOpen(boot);
            IO.File symbols = null;
            for (int i = 0; i < boot.Files.Count; i++)
            {
                if (boot.Files[i].Name == "SYMBOLS.BIN") symbols = boot.Files[i];
            }
            if (symbols == null) return;

            symbols.OnOpen(symbols);
            var firstSector = symbols.FileSystem.GetFirstSector(symbols);
            Exceptions.AddSymbols(Memory.Utilities.UnsafeCast<uint[]>(firstSector));

            for (uint i = 512; i < symbols.Size; i += 512)
            {
                firstSector = symbols.FileSystem.OnReadSector(symbols);
                Exceptions.AddSymbols(Memory.Utilities.UnsafeCast<uint[]>(firstSector));
            }
        }

        private static BitmapFont LoadFont()
        {
            var root = IO.Filesystem.Root;
            IO.Directory dev = null, hda = null, etc = null;
            for (int i = 0; i < root.Directories.Count; i++) if (root.Directories[i].Name == "dev") dev = root.Directories[i];
            if (dev == null) return null;
            for (int i = 0; i < dev.Directories.Count; i++) if (dev.Directories[i].Name == "hda") hda = dev.Directories[i];
            if (hda == null) return null;
            hda.OnOpen(hda);
            for (int i = 0; i < hda.Directories.Count; i++) if (hda.Directories[i].Name == "ETC") etc = hda.Directories[i];
            if (etc == null) return null;
            etc.OnOpen(etc);
            IO.File texture = null, charData = null;
            for (int i = 0; i < etc.Files.Count; i++)
            {
                if (etc.Files[i].Name == "INCONS16.BIN") texture = etc.Files[i];
                if (etc.Files[i].Name == "INCONS16.FNT") charData = etc.Files[i];
            }

            if (texture == null || charData == null)
            {
                Logging.WriteLine(LogLevel.Error, "Could not load font");
                return null;
            }

            byte[] fontData = ReadFile(charData);
            byte[] textureData = ReadFile(texture);

            var ptr = Memory.Utilities.ObjectToPtr(fontData) + 8;   // skip over byte array length/size and use values stored by diskmaker

            return new BitmapFont(Memory.Utilities.PtrToObject<BitmapFont.Character[]>(ptr), textureData, 256, 128);
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

        private static void SetWallpaper(BGA bga)
        {
            var root = IO.Filesystem.Root;
            IO.Directory dev = null, hda = null, etc = null;
            for (int i = 0; i < root.Directories.Count; i++) if (root.Directories[i].Name == "dev") dev = root.Directories[i];
            for (int i = 0; i < dev.Directories.Count; i++) if (dev.Directories[i].Name == "hda") hda = dev.Directories[i];
            hda.OnOpen(hda);
            for (int i = 0; i < hda.Directories.Count; i++) if (hda.Directories[i].Name == "ETC") etc = hda.Directories[i];
            if (etc != null) etc.OnOpen(etc);
            IO.File wallpaper = null;
            for (int i = 0; i < etc.Files.Count; i++)
            {
                if (etc.Files[i].Name == "WALLPAPR.BIN") wallpaper = etc.Files[i];
            }

            if (wallpaper != null)
            {
                Logging.WriteLine(LogLevel.Warning, "wallpaper at {0:X}", Memory.Utilities.ObjectToPtr(wallpaper));
                wallpaper.OnOpen(wallpaper);
                uint baseAddr = bga.FrameBufferAddress - 8;

                wallpaper.FileSystem.OnBufferFile(wallpaper, Memory.Utilities.PtrToObject<byte[]>(baseAddr));
            }
        }

        private static void SwitchToTask(int task) { }
        private static void Idle() { }

        public static void Schedule(int timeSlice)
        {
            // make sure timeSlice is modulo 12
            timeSlice = timeSlice % 12;

            // now pick which task to run given our current time slice
            switch (timeSlice)
            {
                case 0:
                case 4:
                case 8:
                    SwitchToTask(1);
                    break;
                case 1:
                case 6:
                    SwitchToTask(2);
                    break;
                case 2:
                    SwitchToTask(3);
                    break;
                default:
                    Idle();
                    break;
            }
        }

        static void Process1()
        {
            while (true) Logging.WriteLine(LogLevel.Panic, "1");
        }

        static void Process2()
        {
            while (true) Logging.WriteLine(LogLevel.Panic, "2");
        }

        private static Process _currentProcess;
        private static Process _p1, _p2;

        public class Process
        {
            public enum ProcessState : byte
            {
                Initialized,
                Running,
                Waiting,
                Terminated
            }

            public Action EntryPoint;
            public int Id;
            public ProcessState State = ProcessState.Initialized;

            public Process(Action entryPoint)
            {
                EntryPoint = entryPoint;
            }
        }

        static void Tick()
        {
            //Logging.WriteLine(LogLevel.Panic, "Tick");
        }

        static void PrintEAX(uint t)
        {
            var sp = CPU.ReadESP();

            uint addr = sp + 15 * 4;    // eax is at this position as pushed by the interrupt
            COM.Write((byte)CPU.ReadMemInt(addr));
        }

        private static void LoadSampleApp()
        {
            Logging.WriteLine(LogLevel.Warning, "Loading Sample App");

            var root = IO.Filesystem.Root;
            IO.Directory dev = null, hda = null, apps = null;
            for (int i = 0; i < root.Directories.Count; i++) if (root.Directories[i].Name == "dev") dev = root.Directories[i];
            for (int i = 0; i < dev.Directories.Count; i++) if (dev.Directories[i].Name == "hda") hda = dev.Directories[i];
            for (int i = 0; i < hda.Directories.Count; i++) if (string.Equals(hda.Directories[i].Name, "apps", StringComparison.CurrentCultureIgnoreCase)) apps = hda.Directories[i];

            var data = ReadFile(apps.Files[0]);

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

            //Paging.SwitchPageDirectory(Paging.KernelDirectory);
            Logging.WriteLine(LogLevel.Trace, "Jumping to code");

            //CPU.Jump(0x400000);
            CPU.JumpUserMode(0x400000);
        }

        static void PrintStack(uint t)
        {
            Logging.WriteLine(LogLevel.Trace, "General Protection Fault");
            Kernel.Exceptions.PrintStackTrace();

            while (true) ;
        }

        static void Start()
        {
            /*for (uint i = 0xb8000; i < 0xb8000 + 80 * 25 * 2; i += 4) CPU.WriteMemInt(i, 0);
            CPU.WriteMemShort(0xb8000, 'H' | 0x0f00);
            CPU.WriteMemShort(0xb8002, 'i' | 0x0f00);
            CPU.LectureTest();
            while (true) ;*/

            //VGA.WriteString("Hello from C#");
            Logging.LoggingLevel = LogLevel.Trace;
            
            COM.Initialize();

            KernelHeap.KernelAllocator = BumpHeap.Instance;

            PIC.SetIrqCallback(14, EmptyIrq);
            PIC.SetIrqCallback(32, EmptyIrq);
            PIC.Init();
            PIC.SetIrqCallback(0, PIT.Tick);

            PIC.SetIsrCallback(13, PrintStack);

            PIC.SetIsrCallback(31, PrintEAX);   // for now this is our only "system call", which prints a character stored in EAX to the COM port

            PIT.Init(100);

            PCI.ScanBus();

            InitializePaging();

            // try setting up TSS by getting its address from the stage 2 boot loader
            var tssAddress = CPU.ReadMemShort(0x502);
            var tss = Kernel.Memory.Utilities.PtrToObject<CPU.TSSEntryWrapper>(tssAddress);

            tss.ss0 = 0x10;
            tss.esp0 = 0x7E00;  // use bootloader stage 1 as the new kernel stack for the TSS
            CPU.FlushTSS();

            IO.Filesystem.Init();
            PATA.AttachDriver();    // needs PIT.Tick attached to irq 0 for Thread.Sleep

            LoadSampleApp();

            //CPUHelper.CPU.Interrupt30('H');
            //CPUHelper.CPU.Interrupt30('i');

            /*_p1 = new Process(Process1);
            _p2 = new Process(Process2);

            _currentProcess = _p1;*/

            //_p1.EntryPoint();
            while (true) ;

            /*InitializePaging();

            // create a page directory for process 1
            PageDirectory dir1 = Paging.CloneDirectory(Paging.CurrentDirectory);
            Paging.SwitchPageDirectory(dir1);

            // create a page at virtual address 0x1000000 for process 1
            var pageProcess1 = Paging.GetPage(0x1000000, true, dir1);
            Paging.AllocateFrame(pageProcess1, false, true);
            CPU.WriteMemInt(0x1000000, 0x12345678);
            Logging.WriteLine(LogLevel.Warning, "From process 1: {0:X}", CPU.ReadMemInt(0x1000000));

            // switch back to kernel page directory
            Paging.SwitchPageDirectory(Paging.KernelDirectory);

            // create a page directory for process 2
            PageDirectory dir2 = Paging.CloneDirectory(Paging.KernelDirectory);
            Paging.SwitchPageDirectory(dir2);

            // create a page at virtual address 0x1000000 for process 2
            var pageProcess2 = Paging.GetPage(0x1000000, true, dir2);
            Paging.AllocateFrame(pageProcess2, false, true);
            CPU.WriteMemInt(0x1000000, 0xDEADC0DE);
            Logging.WriteLine(LogLevel.Warning, "From process 2: {0:X}", CPU.ReadMemInt(0x1000000));

            // switch back to process 1 and print the memory without any modification
            Paging.SwitchPageDirectory(dir1);
            Logging.WriteLine(LogLevel.Warning, "From process 1: {0:X}", CPU.ReadMemInt(0x1000000));

            while (true)
            {
                for (int i = 0; i < 1000000; i++) ;
                //Logging.Write(LogLevel.Trace, ".");
            }*/

            /*PIC.SetIrqCallback(14, EmptyIrq);
            PIC.SetIrqCallback(15, EmptyIrq);
            PCI.ScanBus();
            InitializePaging();

            IO.Filesystem.Init();
            PATA.AttachDriver();

            ReadSymbols();

            Logging.LoggingLevel = LogLevel.Warning;

            kernelTask = Scheduler.GetCurrentTask();
            Scheduler.CreateIdleTask();

            // grab some more memory to use for fonts/etc
            uint addr = 0x100000;
            for (uint i = 0; i < 60; i++)
            {
                var page = Memory.Paging.GetPage(addr, true, Memory.Paging.CurrentDirectory);
                var frameAddr = Memory.Paging.AllocateFrame(page, false, true);
                //Logging.WriteLine(LogLevel.Warning, "Address {0:X} mapped to memory {1:X}", addr, page.Frame);
                if (frameAddr == -1)
                {
                    Logging.WriteLine(LogLevel.Panic, "Could not allocate frame at address 0x{0:X}", addr);
                    while (true) ;
                }
                addr += 0x1000U;
            }
            Memory.KernelHeap.ExpandHeap(0x100000, 60);

            var kernelPagingDirectory = Paging.CurrentDirectory;

            Logging.WriteLine(LogLevel.Warning, "Create firstPagingDirectory");
            var firstPagingDirectory = Paging.CloneDirectory(Paging.CurrentDirectory);
            Paging.SwitchPageDirectory(firstPagingDirectory);
            var stackPage1 = Paging.GetPage(0xDEAD0000, true, firstPagingDirectory);
            Paging.AllocateFrame(stackPage1, true, true);

            // clear the stage 1 bootloader memory in prep for using it as a stack for now
            for (uint i = 0; i < 512; i += 4) CPU.WriteMemInt(0xDEAD0000 + i, 0);

            Paging.SwitchPageDirectory(kernelPagingDirectory);
            Logging.WriteLine(LogLevel.Warning, "Create firstTask");
            firstTask = new Scheduler.Task(0xDEAD0000 + 512, firstPagingDirectory);    // create a new task with a stack in the stage 1 bootloader memory
            firstTask.SetEntryPoint(FirstTaskEntryPoint);

            Logging.WriteLine(LogLevel.Warning, "Create secondPagingDirectory");
            //Paging.SwitchPageDirectory(kernelPagingDirectory);
            var secondPagingDirectory = Paging.CloneDirectory(kernelPagingDirectory);
            Paging.SwitchPageDirectory(secondPagingDirectory);
            var stackPage2 = Paging.GetPage(0xDEAD0000, true, secondPagingDirectory);
            Paging.AllocateFrame(stackPage2, true, true);

            // clear the stage 1 bootloader memory in prep for using it as a stack for now
            for (uint i = 0; i < 512; i += 4) CPU.WriteMemInt(0xDEAD0000 + i, 0);
            Logging.WriteLine(LogLevel.Warning, "Create secondTask");
            //Paging.SwitchPageDirectory(kernelPagingDirectory);
            secondTask = new Scheduler.Task(0xDEAD0000 + 512, secondPagingDirectory);    // create a new task with a stack in the stage 1 bootloader memory
            secondTask.SetEntryPoint(SecondTaskEntryPoint);

            //SchedulerV2.SwitchToTask(secondTask);
            Scheduler.Add(firstTask);
            Scheduler.Add(secondTask);
            Scheduler.PreemptiveScheduler = true;
            //Scheduler.Schedule();

            // this will never happen because the scheduler will switch to a new task
            while (true) ;
            {
                Logging.WriteLine(LogLevel.Warning, "Kernel task, stack {0:X}", CPU.ReadEBP());
            }*/
        }

        /*private static Scheduler.Task kernelTask, firstTask, secondTask;

        static void FirstTaskEntryPoint()
        {
            uint temp = 1;

            while (true)
            {
                //Scheduler.Sleep(firstTask, 1000);
                Logging.WriteLine(LogLevel.Warning, "Awake! Thread {0} working, stack {1:X}", Scheduler.CurrentTask.Id, CPU.ReadEBP());
                //Exceptions.PrintStackTrace();
                for (uint i = 0; i < 100000000; i++) ;
                //temp++;
            }
        }

        static void SecondTaskEntryPoint()
        {
            if (BGA.IsAvailable())
            {
                for (int i = 0; i < PCI.Devices.Count; i++)
                {
                    var device = PCI.Devices[i];

                    if (device.ClassCode == PCI.ClassCode.DisplayController)
                    {
                        BGA bga = new BGA(device);
                        bga.InitializeMode(1280, 720, 32);

                        SetWallpaper(bga);

                        var font = LoadFont();
                        if (font != null) font.DrawString("Hello from C#!", 20, 20, bga.FrameBufferAddress, 1280, 720);
                    }
                }
            }

            while (true)
            {
                Logging.WriteLine(LogLevel.Warning, "Awake! Thread {0} working, stack {1:X}", Scheduler.CurrentTask.Id, CPU.ReadEBP());
                for (uint i = 0; i < 100000000; i++) ;
            }
            /*CPU.Cli();

            Logging.WriteLine(LogLevel.Panic, "In task");
            Exceptions.PrintStackTrace();

            while (true) ;*/

            /*while (true)
            {
                //Scheduler.Sleep(firstTask, 1000);
                Logging.WriteLine(LogLevel.Warning, "Awake! Thread {0} working, stack {1:X}", 2, CPU.ReadEBP());
                //Exceptions.PrintStackTrace();
                for (uint i = 0; i < 100000000; i++) ;
            }*/
        //}

        private enum MissingCPUFeature : byte
        {
            None,
            SSE3,
            Long_Mode,
            LAHF_SAHF_Support
        }

        private static MissingCPUFeature VerifyCompatibleCPU()
        {
            CPU.ReadCPUID(CPU.CPUIDLeaf.ProcessorInfoAndFeatureBits, ref cpuId);
            if ((cpuId.ecx & 0x01) == 0) return MissingCPUFeature.SSE3;

            CPU.ReadCPUID(CPU.CPUIDLeaf.ExtendedProcessorInfoAndFeatureBits, ref cpuId);
            if ((cpuId.edx & 0x20000000) == 0) return MissingCPUFeature.Long_Mode;
            if ((cpuId.ecx & 0x01) == 0) return MissingCPUFeature.LAHF_SAHF_Support;

            return MissingCPUFeature.None;
        }

        private static bool VerifyA20()
        {
            // check if A20 is enabled
            CPU.WriteMemInt(0x112345, 0x112345);
            CPU.WriteMemInt(0x012345, 0x012345);
            uint val1 = CPU.ReadMemInt(0x112345);
            uint val2 = CPU.ReadMemInt(0x012345);

            if (val1 == val2)
            {
                CPU.FastA20();

                CPU.WriteMemInt(0x112345, 0x112345);
                CPU.WriteMemInt(0x012345, 0x012345);
                val1 = CPU.ReadMemInt(0x112345);
                val2 = CPU.ReadMemInt(0x012345);

                return val1 != val2;
            }

            return true;
        }

        public static void PrintFloat(float f)
        {
            float tens = 0;
            float temp = f;

            while (temp < 1 && temp != 0)
            {
                tens--;
                temp *= 10;
            }

            while (temp >= 10 && temp != 0)
            {
                tens++;
                temp /= 10;
            }

            for (int i = 0; i < 7; i++)
            {
                int integer = (int)temp;
                temp -= integer;
                temp *= 10;
                VGA.WriteChar(integer + 48);

                if (i == 0) VGA.WriteChar('.');
            }

            VGA.WriteChar('e');
            PrintInt((int)tens);
        }

        public static void PrintInt(int value)
        {
            int divisor = 1;

            while (divisor * 10 <= value)
            {
                divisor *= 10;
            }

            if (value < 0) VGA.WriteChar('-');

            while (divisor > 0)
            {
                int c = Math32.Divide(value, divisor);
                VGA.WriteChar((c % 10) + 48);

                divisor = Math32.Divide(divisor, 10);
            }
        }
    }
}
