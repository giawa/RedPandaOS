using CPUHelper;
using Kernel.Devices;
using Runtime;
using Runtime.Collections;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Kernel
{
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
            uint entries = CPU.ReadMemShort(0x500);

            List<Memory.SMAP_Entry> freeMemory = new List<Memory.SMAP_Entry>((int)entries);

            for (uint i = 0; i < entries; i++)
            {
                Memory.SMAP_Entry entry = Memory.Utilities.PtrToObject<Memory.SMAP_Entry>(0x504 + 24 * i);
                if ((entry.Type & 1) == 1) freeMemory.Add(entry);
                //Logging.Write(LogLevel.Warning, "Region {0} {1:X}{2:X}", i, entry.BaseH, entry.BaseL);
                //Logging.WriteLine(LogLevel.Warning, " {0:X}{1:X} {2:X}", entry.LengthH, entry.LengthL, entry.Type);
            }

            // when initializing page we set up how many frames we will make available (each frame must be mapped somewhere in memory)
            // to start we'll support up to 64MiB of memory, which requires an 2kiB allocation of frames
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
            for (int i = 0; i < boot.Contents.Count; i++)
            {
                if (boot.Contents[i].Name == "SYMBOLS.BIN") symbols = boot.Contents[i];
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
            for (int i = 0; i < etc.Contents.Count; i++)
            {
                if (etc.Contents[i].Name == "INCONS16.BIN") texture = etc.Contents[i];
                if (etc.Contents[i].Name == "INCONS16.FNT") charData = etc.Contents[i];
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
            for (int i = 0; i < etc.Contents.Count; i++)
            {
                if (etc.Contents[i].Name == "WALLPAPR.BIN") wallpaper = etc.Contents[i];
            }

            if (wallpaper != null)
            {
                Logging.WriteLine(LogLevel.Warning, "wallpaper at {0:X}", Memory.Utilities.ObjectToPtr(wallpaper));
                wallpaper.OnOpen(wallpaper);
                uint baseAddr = bga.FrameBufferAddress - 8;

                wallpaper.FileSystem.OnBufferFile(wallpaper, Memory.Utilities.PtrToObject<byte[]>(baseAddr));
            }
        }

        static void Start()
        {
            COM.Initialize();

            Logging.LoggingLevel = LogLevel.Warning;
            VGA.EnableScrolling = true;

            PIC.SetIrqCallback(0, PIT.Tick);
            PIC.SetIrqCallback(1, Keyboard.OnKeyPress);
            PIC.SetIrqCallback(12, EmptyIrq);   // ps-2 mouse
            PIC.Init();
            PIT.Init(100);

            PIC.SetIrqCallback(14, EmptyIrq);
            PIC.SetIrqCallback(15, EmptyIrq);

            //VGA.Clear();
            //VGA.WriteString(_welcomeMessage, 0x0700);
            //VGA.WriteLine();

            PCI.ScanBus();
            InitializePaging();

            var newDirectory = Memory.Paging.CloneDirectory(Memory.Paging.KernelDirectory);
            Memory.Paging.SwitchPageDirectory(newDirectory);

            /*var page = Memory.Paging.GetPage(0xDEAD0000, true, newDirectory);
            Memory.Paging.AllocateFrame(page, false, true);
            //CPU.WriteMemInt(0xDEAD0000, 0x12345678U);
            for (uint i = 0; i < 1024; i++)
                CPU.WriteMemInt(0xDEAD0000 + i * 4, i + 0x12345678U);

            var userDirectory = Memory.Paging.CloneDirectory(newDirectory);

            Logging.WriteLine(LogLevel.Warning, "Original (0x12345678U): 0x{0:X}", CPU.ReadMemInt(0xDEAD0000));
            CPU.WriteMemInt(0xDEAD0000, 0x23456789U);
            Logging.WriteLine(LogLevel.Warning, "Updated (0x23456789U): 0x{0:X}", CPU.ReadMemInt(0xDEAD0000));
            Memory.Paging.SwitchPageDirectory(userDirectory);
            Logging.WriteLine(LogLevel.Warning, "New Directory (0x12345678U): 0x{0:X}", CPU.ReadMemInt(0xDEAD0000));

            for (uint i = 0; i < 4; i++)
                Logging.WriteLine(LogLevel.Warning, "{0:X} : {1:X}", 0xDEAD0000 + (i * 4), CPU.ReadMemInt(0xDEAD0000 + i * 4));

            Memory.Paging.SwitchPageDirectory(newDirectory);

            for (uint i = 0; i < 4; i++)
                Logging.WriteLine(LogLevel.Warning, "{0:X} : {1:X}", 0xDEAD0000 + (i * 4), CPU.ReadMemInt(0xDEAD0000 + i * 4));*/

            Memory.Stack.MoveStack(0x9000, 0xDEAE000, 8192);

            Scheduler.Init();

            //Logging.WriteLine(LogLevel.Warning, "init is back!");

            /*for (uint i = 0; i < 5; i++)
            {
                var page = Memory.Paging.GetPage(0xDEAD0000 + (i << 12), true, Memory.Paging.KernelDirectory);
                Memory.Paging.AllocateFrame(page, true, true);

                CPU.WriteMemInt(0xDEAD0000 + (i << 12), 0x12345678U);
            }*/

            //Logging.WriteLine(LogLevel.Warning, "After:");
            //Memory.Paging.Draw(); VGA.WriteLine();

            IO.Filesystem.Init();
            PATA.AttachDriver();

            ReadSymbols();

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

            //Logging.WriteLine(LogLevel.Warning, "Page fault by reading 0xA09000: 0x{0:X}", CPU.ReadMemInt(0xA09000));

            //Logging.WriteLine(LogLevel.Warning, "Using {0} bytes of memory", Memory.SplitBumpHeap.Instance.UsedBytes);
            //Logging.WriteLine(LogLevel.Warning, "Found {0} PCI devices", (uint)PCI.Devices.Count);

            uint result = 0;// Scheduler.Fork();

            if (result != 0)
            {
                // idle task
                Logging.WriteLine(LogLevel.Warning, "Idle thread has pid {0}", Scheduler.CurrentTask.Id);
                while (true)
                {
                    System.Threading.Thread.Sleep(1000);
                    Logging.WriteLine(LogLevel.Warning, "Sleeping 1");
                    //CPU.Halt();
                }
            }
            else
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

                            /*uint color = 0;
                            uint[] row = new uint[1280];
                            uint ptr = Memory.Utilities.ObjectToPtr(row) + 8;
                            while (true)
                            {
                                for (int j = 0; j < row.Length; j++) row[j] = color;

                                uint fb = bga.FrameBufferAddress;
                                uint end = fb + 1280 * 720 * 4;
                                for (uint k = 0; k < 1280 * 720; k++, fb += 4)
                                //for (; fb < end; fb += (uint)row.Length << 2)
                                {
                                    //CPU.FastCopyDWords(ptr, fb, (uint)row.Length);
                                    CPU.WriteMemInt(fb, color);
                                    if ((fb & 63) == 0)// && k < 917504)
                                    {
                                        color = (color + 1) & 0x00ffffffU;
                                    }
                                }
                                color = (color & 0xff0000) + 0x010000;
                            }*/

                            var watch = new Runtime.Stopwatch();
                            watch.Start();

                            SetWallpaper(bga);

                            watch.Stop();
                            Logging.WriteLine(LogLevel.Warning, "Took {0} ticks", watch.ElapsedTicks);

                            var font = LoadFont();
                            if (font != null) font.DrawString("Hello from C#!", 20, 20, bga.FrameBufferAddress, 1280, 720);

                            Logging.WriteLine(LogLevel.Warning, "Entered BGA mode");
                        }
                    }
                }
                else
                {
                    Logging.WriteLine(LogLevel.Warning, "No BGA support found, running text mode command prompt.");
                    Applications.terminal terminal = new Applications.terminal();
                    terminal.Run(IO.Filesystem.Root);
                }
            }

            while (true) ;
        }

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
