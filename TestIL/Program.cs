using System;
using System.Runtime.InteropServices;
using CPUHelper;

namespace TestIL
{
    class Program
    {
        private const string _disk = "Disk";
        private const string _mem = "Mem";
        private const string _fail = " fail";
        private static CPU.GDT _gdt;
        //private static CPU.GDTPointer _gdtPointer;

        static void Main()
        {

        }

        [BootSector]
        [RealMode(0x7C00)]
        static void BootloaderStage1()
        {
            // BIOS stores the disk in dl, the lowest 8 bits of dx
            byte disk = (byte)CPU.ReadDX();

            if (LoadDiskWithRetry(0x0000, 0x9000, disk, 24))
            {
                CPU.Jump(0x9000);
            }
            else
            {
                Write("Failed to read from disk 0x");
                WriteHex(disk);
            }

            while (true) ;
        }

        [RealMode(0x9000)]
        static void BootloaderStage2()
        {
            if (DetectMemory(0x500, 10) == 0) ErrorAndHang("Failed to get memory map");
            Bios.EnableA20();

            _gdt.KernelCodeSegment.segmentLength = 0xffff;
            _gdt.KernelCodeSegment.flags1 = 0x9A;
            _gdt.KernelCodeSegment.flags2 = 0xCF;

            _gdt.KernelDataSegment.segmentLength = 0xffff;
            _gdt.KernelDataSegment.flags1 = 0x92;
            _gdt.KernelDataSegment.flags2 = 0xCF;

            Bios.EnterProtectedMode(ref _gdt);
            Write("Failed to enter protected mode");

            while (true) ;
        }

        static void ErrorAndHang(string s)
        {
            Write(s);
            while (true) ;
        }

        private static Bios.SMAP_ret _smap_ret;

        public static short DetectMemory(ushort address, int maxEntries)
        {
            short entries = 0;

            do
            {
                if (Bios.DetectMemory((ushort)(address + entries * 24 + 2), ref _smap_ret) == 0xff) break;
                entries++;
            } while ((_smap_ret.contId1 != 0 || _smap_ret.contId2 != 0) && entries < maxEntries);

            CPU.WriteMemory(address, entries);

            return entries;
        }

        public static bool LoadDiskWithRetry(ushort highAddr, ushort lowAddr, byte disk, byte sectors)
        {
            int retry = 0;
            ushort sectorsRead;

            do
            {
                sectorsRead = Bios.LoadDisk(highAddr, lowAddr, disk, sectors);
            } while (sectorsRead != sectors && retry++ < 3);

            return sectorsRead == sectors;
        }

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

        static void Main32()
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
            //VGA.WriteHex(entries);
            /*for (int i = 0; i < entries * 24; i ++)
            {
                VGA.WriteHex(CPU.ReadMemByte((ushort)(0x502 + i)));
                VGA.WriteVideoMemoryChar(' ');
            }

            VGA.WriteLine();
            VGA.WriteHex(CPU.ReadMemByte((ushort)(0x502 + 9)));

            VGA.WriteLine();*/
            //VGA.WriteHex(0x12345678);

            //_entry = Marshal.PtrToStructure<SMAP_entry>((IntPtr)0x502);
            VGA.WriteVideoMemoryString("Memory Regions as given by BIOS:");
            VGA.WriteLine();
            //int i = 0;
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
                VGA.WriteVideoMemoryChar(MathHelper.Modulo(i, 10) + 48, (ushort)(i << 8));
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

        public static int Factorial(int num)
        {
            if (num == 1) return 1;
            else return num * Factorial(num - 1);
        }

        public static int IsPrime(int num)
        {
            if (num == 1) return 0;
            else
            {
                for (int i = 2; i < num; i++)
                {
                    if (MathHelper.Modulo(num, i) == 0) return 0;
                }
                return num;
            }
        }

        #region Bios Helpers
        public static void WriteHex(int value)
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
            if (value >= 10) Bios.WriteByte(value + 55);
            else Bios.WriteByte(value + 48);
        }

        public static void WriteLine(string s)
        {
            Write(s);
            Bios.WriteByte((byte)'\n');
            Bios.WriteByte((byte)'\r');
        }

        public static void Write(string s)
        {
            int i = 0;

            while (s[i] != 0)
            {
                Bios.WriteByte((byte)s[i]);
                i += 1;
            }
        }

        public static void WriteInt(int value)
        {
            int divisor = 1;

            while (divisor * 10 <= value)
            {
                divisor *= 10;
            }

            while (divisor > 0)
            {
                int c = MathHelper.Divide(value, divisor);
                Bios.WriteByte(MathHelper.Modulo(c, 10) + 48);

                divisor = MathHelper.Divide(divisor, 10);
            }
        }
        #endregion
    }
}
