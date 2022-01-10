using CPUHelper;
using Kernel.Devices;
using Runtime;
using Runtime.Collections;
using System;
using System.Runtime.InteropServices;

namespace Kernel
{
    public static class Init
    {
        private const string _welcomeMessage = "Hello from C#!";

        [StructLayout(LayoutKind.Sequential)]
        public struct SMAP_entry
        {
            public uint BaseL;
            public uint BaseH;
            public uint LengthL;
            public uint LengthH;
            public uint Type;
            public uint ACPI;
        }

        private static SMAP_entry _entry;
        private static CPU.CPUIDValue cpuId;

        static void EmptyIrq()
        {

        }

        static void Start()
        {
            Logging.LoggingLevel = LogLevel.Warning;

            PIC.SetIrqCallback(0, PIT.Tick);
            PIC.SetIrqCallback(1, Keyboard.OnKeyPress);
            PIC.SetIrqCallback(12, EmptyIrq);   // ps-2 mouse
            PIC.Init();
            PIT.Init(100);

            //PIC.SetIrqCallback(14, EmptyIrq);
            //PIC.SetIrqCallback(15, EmptyIrq);

            //VGA.Clear();
            //VGA.WriteString(_welcomeMessage, 0x0700);
            //VGA.WriteLine();

            Memory.Paging.InitializePaging(256);    // loads the entire kernel + heap into paging

            IO.Filesystem.Init();
            PCI.ScanBus();
            PATA.AttachDriver();

            //Logging.WriteLine(LogLevel.Warning, "Using {0} bytes of memory", Memory.SplitBumpHeap.Instance.UsedBytes);
            //Logging.WriteLine(LogLevel.Warning, "Found {0} PCI devices", (uint)PCI.Devices.Count);
            

            Applications.terminal terminal = new Applications.terminal();
            terminal.Run(IO.Filesystem.Root.Directories[0]);

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

        public static void CopyTo(uint source, ref SMAP_entry destination, int size)
        {
            for (int i = 0; i < size; i++)
                CPU.CopyByte<SMAP_entry>(source, (uint)i, ref destination, (uint)i);
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
