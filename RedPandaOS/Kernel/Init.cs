using CPUHelper;
using Kernel.Devices;
using Runtime;
using Runtime.Collections;
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

        public struct Vector2i
        {
            public uint x;
            public uint y;
        }

        /*static Vector2i Modify(Vector2i v)
        {
            v.x++;
            v.y++;
            return v;
        }*/

        static void Modify(Vector2i v, Vector2i v2)
        {
            v.x = 3;
            v.y = 4;

            v2.x += 2;
            v2.y += 2;

            CPU.WriteMemory(0xb8000 + 6, (ushort)((v.x + 48) | 0x0f00));
            CPU.WriteMemory(0xb8000 + 8, (ushort)((v.y + 48) | 0x0f00));

            CPU.WriteMemory(0xb8000 + 12, (ushort)((v2.x + 48) | 0x0f00));
            CPU.WriteMemory(0xb8000 + 14, (ushort)((v2.y + 48) | 0x0f00));
        }

        private static Vector2i original, next;

        //private static Runtime.SimpleList test;

        public class KeyPress
        {
            public char KeyCode;
            public byte Modifiers;
            public byte Reserved;
        }

        static void Start()
        {
            Logging.LoggingLevel = LogLevel.Warning;

            PIC.Init();
            PIC.SetIrqCallback(0, PIT.Tick);
            PIC.SetIrqCallback(1, Keyboard.OnKeyPress);
            PIT.Init(50);

            VGA.Clear();
            VGA.WriteString(_welcomeMessage, 0x0700);
            VGA.WriteLine();

            ObjectPool<KeyPress> pool = new ObjectPool<KeyPress>(10);
            /*ObjectPool<KeyPress> pool = new ObjectPool<KeyPress>(10);

            if (pool.TryAlloc(out var item))
            {
                item.KeyCode = 'c';

                if (pool.TryAlloc(out var anotherItem))
                {
                    anotherItem.KeyCode = 'd';
                }

                VGA.WriteVideoMemoryString("Found char ");
                VGA.WriteVideoMemoryChar(item.KeyCode);
                VGA.WriteLine();

                pool.Free(item);

                for (int i = 0; i < 16; i++)
                {
                    VGA.WriteHex(0x20000 + i * 4);
                    VGA.WriteVideoMemoryString(" : ");
                    VGA.WriteHex(CPU.ReadMemInt((uint)(0x20000 + i * 4)));
                    VGA.WriteLine();
                }
            }
            else VGA.WriteVideoMemoryString("Could not allocate item");*/

            /*VGA.WriteLine();

            // make sure we're in 32 bit mode and have an FPU
            if ((CPU.ReadCR0() & 0x00000011) != 0x00000011)
            {
                VGA.WriteVideoMemoryString("Failed to boot (invalid CR0 support)");
                while (true) ;
            }

            // check for SSE, long mode and SAHF/LAHF support
            if (VerifyCompatibleCPU() != MissingCPUFeature.None)
            {
                VGA.WriteVideoMemoryString("Failed to boot (incompatible CPU) error 0x");
                VGA.WriteHex((byte)VerifyCompatibleCPU());
                while (true) ;
            }*/

            /*CPU.ReadCPUID(CPU.CPUIDLeaf.VirtualAndPhysicalAddressSizes, ref cpuId);
            VGA.WriteVideoMemoryString("Found ");
            PrintInt((int)(cpuId.ecx & 0xff) + 1);
            VGA.WriteVideoMemoryString(" cpu cores!");
            VGA.WriteLine();
            VGA.WriteLine();*/

            //VerifyA20();
            //PCI.ScanBus();

            /*int entries = CPU.ReadMemShort(0x500);
            VGA.WriteLine();

            VGA.WriteVideoMemoryString("Memory Regions as given by BIOS:");
            VGA.WriteLine();

            for (int i = 0; i < entries; i++)
            {
                CopyTo((uint)(0x502 + 24 * i), ref _entry, 24);
                if ((_entry.Type & 1) != 1) continue;
                VGA.WriteVideoMemoryString("Memory Region ");
                VGA.WriteVideoMemoryChar(48 + i);
                VGA.WriteVideoMemoryChar(' ');
                VGA.WriteHex(_entry.BaseH); VGA.WriteHex(_entry.BaseL); VGA.WriteVideoMemoryChar(' ');
                VGA.WriteHex(_entry.LengthH); VGA.WriteHex(_entry.LengthL); VGA.WriteLine();
            }*/

            // verify memory looks correct
            /*int j = 0;
            for (uint i = 0xA000; i < 0x20000; i += 512)
            {
                var b = CPU.ReadMemInt(i);
                VGA.WriteHex((byte)b);
                VGA.WriteVideoMemoryChar(' ');
                if (Math32.Modulo(j + 10, 16) == 0) VGA.WriteLine();
                j++;
            }
            VGA.WriteLine();*/

            /*VGA.WriteLine();

            for (int i = 0; i < 255; i++)
            {
                VGA.WriteVideoMemoryChar(Math32.Modulo(i, 10) + 48, (ushort)(i << 8));
            }

            VGA.WriteLine();*/

            /*VGA.WriteLine();
            VGA.WriteVideoMemoryString("CR0: 0x");
            VGA.WriteHex((int)CPU.ReadCR0());
            VGA.WriteLine();*/

            



            while (true) ;
        }

        private static void Something()
        {
            VGA.WriteString("Testing actions!");
            VGA.WriteLine();
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
                VGA.WriteChar(Math32.Modulo(c, 10) + 48);

                divisor = Math32.Divide(divisor, 10);
            }
        }
    }

    public class TestClass
    {
        public byte Value { get; set; }
        public float Part2 { get; set; }

        public void Print()
        {
            VGA.WriteHex(Value);
            VGA.WriteChar('.');
            Init.PrintFloat(Part2);
            VGA.WriteChar(' ');
        }
    }
}
