using CPUHelper;
using Kernel.Devices;
using Runtime;
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

        static void Start()
        {
            VGA.Clear();
            VGA.WriteVideoMemoryString(_welcomeMessage, 0x0700);
            VGA.WriteLine();

            // check if A20 is enabled
            CPU.WriteMemInt(0x112345, 0x112345);
            CPU.WriteMemInt(0x012345, 0x012345);
            uint val1 = CPU.ReadMemInt(0x112345);
            uint val2 = CPU.ReadMemInt(0x012345);

            if (val1 == val2) CPU.FastA20();

            int entries = CPU.ReadMemShort(0x500);
            VGA.WriteLine();

            VGA.WriteVideoMemoryString("Memory Regions as given by BIOS:");
            VGA.WriteLine();

            for (int i = 0; i < entries; i++)
            {
                CopyTo((uint)(0x502 + 24 * i), ref _entry, 24);
                VGA.WriteVideoMemoryString("Memory Region ");
                VGA.WriteVideoMemoryChar(48 + i);
                VGA.WriteVideoMemoryChar(' ');
                VGA.WriteHex(_entry.BaseL); VGA.WriteVideoMemoryChar(' ');
                VGA.WriteHex(_entry.BaseH); VGA.WriteVideoMemoryChar(' ');
                VGA.WriteHex(_entry.LengthL); VGA.WriteVideoMemoryChar(' ');
                VGA.WriteHex(_entry.LengthH); VGA.WriteVideoMemoryChar(' ');
                VGA.WriteHex(_entry.Type); VGA.WriteLine();
                //VGA.WriteHex(_entry.ACPI); VGA.WriteLine();
            }

            VGA.WriteLine();

            for (int i = 0; i < 255; i++)
            {
                VGA.WriteVideoMemoryChar(Math32.Modulo(i, 10) + 48, (ushort)(i << 8));
            }

            VGA.WriteLine();
            VGA.WriteLine();
            VGA.WriteVideoMemoryString("CR0: 0x");
            VGA.WriteHex((int)CPU.ReadCR0());

            while (true) ;
        }

        public static void CopyTo(uint source, ref SMAP_entry destination, int size)
        {
            for (int i = 0; i < size; i++)
                CPU.CopyByte<SMAP_entry>(source, (uint)i, ref destination, (uint)i);
        }
    }
}
