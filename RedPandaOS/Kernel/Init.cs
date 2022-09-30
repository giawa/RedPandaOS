using CPUHelper;
using Kernel.Devices;
using Kernel.Memory;
using Runtime;
using Runtime.Collections;
using System;
using System.Diagnostics;
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

            PIC.SetIrqCallback(0, PIT.Tick);
            PIC.Init();

            Logging.LoggingLevel = LogLevel.Trace;

            InitializePaging();

            kernelTask = SchedulerV2.GetCurrentTask();

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
            firstTask = new TaskV2(0xDEAD0000 + 512, firstPagingDirectory);    // create a new task with a stack in the stage 1 bootloader memory
            firstTask.SetEntryPoint(FirstStack);

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
            secondTask = new TaskV2(0xDEAD0000 + 512, secondPagingDirectory);    // create a new task with a stack in the stage 1 bootloader memory
            secondTask.SetEntryPoint(SecondStack);

            SchedulerV2.SwitchToTask(secondTask);

            while (true)
            {
                Logging.WriteLine(LogLevel.Warning, "Kernel task, stack {0:X}", CPU.ReadEBP());
            }
        }

        private static TaskV2 kernelTask, firstTask, secondTask;

        static void FirstStack()
        {
            uint temp = 1;

            while (true)
            {
                Logging.WriteLine(LogLevel.Warning, "Thread {0}, stack {1:X}", temp, CPU.ReadEBP());
                SchedulerV2.SwitchToTask(secondTask);
            }
        }

        static void SecondStack()
        {
            uint temp = 2;

            while (true)
            {
                Logging.WriteLine(LogLevel.Warning, "Thread {0}, stack {1:X}", temp, CPU.ReadEBP());
                SchedulerV2.SwitchToTask(firstTask);
            }
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
