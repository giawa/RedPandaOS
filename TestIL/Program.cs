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

        [BootEntryPoint]
        [RealMode]
        static void Main()
        {
            // BIOS stores the disk in dl, the lowest 8 bits of dx
            byte disk = (byte)CPU.ReadDX();

            if (DetectMemory(0x500, 10) != 0)
            {
                if (LoadDiskWithRetry(0x0000, 0x9000, disk, 4))
                {
                    _gdt.KernelCodeSegment.segmentLength = 0xffff;
                    _gdt.KernelCodeSegment.flags1 = 0x9A;
                    _gdt.KernelCodeSegment.flags2 = 0xCF;

                    _gdt.KernelDataSegment.segmentLength = 0xffff;
                    _gdt.KernelDataSegment.flags1 = 0x92;
                    _gdt.KernelDataSegment.flags2 = 0xCF;

                    Bios.EnterProtectedMode(ref _gdt);
                }
                else
                {
                    //Write(_disk);
                }
            }
            else
            {
                //Write(_mem);
            }

            //Write(_fail);

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
                sectorsRead = Bios.LoadDisk(0x0000, 0x9000, disk, sectors);
            } while (sectorsRead != sectors && retry++ < 3);

            return sectorsRead == sectors;
        }

        static void Main32()
        {
            VGA.Clear();
            VGA.WriteVideoMemoryString(_welcomeMessage, 0x0700);
            VGA.WriteLine();

            for (int i = 0; i < 255; i++)
            {
                VGA.WriteVideoMemoryChar(MathHelper.Modulo(i, 10) + 48, (ushort)(i << 8));
            }

            VGA.WriteLine();
            VGA.WriteVideoMemoryString("CR0: 0x");
            VGA.WriteHex((int)CPU.ReadCR0());

            while (true) ;
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
        [RealMode]
        public static void WriteHex(int value)
        {
            WriteHexChar(value >> 12);
            WriteHexChar(value >> 8);
            WriteHexChar(value >> 4);
            WriteHexChar(value);
        }

        [RealMode]
        public static void WriteHex(byte value)
        {
            WriteHexChar(value >> 4);
            WriteHexChar(value);
        }

        [RealMode]
        public static void WriteHexChar(int value)
        {
            value &= 0x0f;
            if (value >= 10) Bios.WriteByte(value + 55);
            else Bios.WriteByte(value + 48);
        }

        [RealMode]
        public static void WriteLine(string s)
        {
            Write(s);
            Bios.WriteByte((byte)'\n');
            Bios.WriteByte((byte)'\r');
        }

        [RealMode]
        public static void Write(string s)
        {
            int i = 0;

            while (s[i] != 0)
            {
                Bios.WriteByte((byte)s[i]);
                i += 1;
            }
        }

        [RealMode]
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
