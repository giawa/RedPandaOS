using CPUHelper;
using Runtime;

namespace Bootloader
{
    static class BiosUtilities
    {
        public class Partition
        {
            public byte Bootable;
            public byte StartingHead;
            public byte StartingSector;
            public byte StartingCylinder;
            public byte PartitionType;
            public byte EndingHead;
            public byte EndingSector;
            public byte EndingCylinder;
            public ushort RelativeSector1, RelativeSector2;
            public ushort TotalSectors1, TotalSectors2;
        }

        public static bool LoadDiskWithRetry(Partition partition, ushort highAddr, /*ushort lowAddr,*/ byte disk, byte sectors)
        {
            ushort retry = 0;
            ushort sectorsRead;

            do
            {
                sectorsRead = Bios.LoadDisk(partition.StartingCylinder, partition.StartingHead, partition.StartingSector, highAddr, disk, sectors);
                if (sectorsRead != sectors) Bios.ResetDisk();
            } while (sectorsRead != sectors && retry++ < 5);

            return sectorsRead == sectors;
        }

        public static bool LoadDiskWithRetry(ushort cylinder, byte head, byte sector, ushort highAddr, ushort lowAddr, byte disk, byte sectors)
        {
            int retry = 0;
            ushort sectorsRead;

            sector = (byte)((sector & 0x3f) | ((cylinder >> 2) & 0xC0));

            do
            {
                sectorsRead = Bios.LoadDisk((byte)cylinder, head, sector, highAddr, disk, sectors);
                if (sectorsRead != sectors) Bios.ResetDisk();
            } while (sectorsRead != sectors && retry++ < 5);

            return sectorsRead == sectors;
        }

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
            if (value >= 10) Bios.WriteByte((byte)(value + 55));
            else Bios.WriteByte((byte)(value + 48));
        }

        public static void WriteLine(string s)
        {
            Write(s);
            Bios.WriteByte((byte)'\n');
            Bios.WriteByte((byte)'\r');
        }

        public static void WriteLine()
        {
            Bios.WriteByte((byte)'\n');
            Bios.WriteByte((byte)'\r');
        }

        public static void Write(string s)
        {
            // do not refactor this to use s.Length as the real mode assembler does not support it
            int i = 0;

            while (s[i] != 0)
            {
                Bios.WriteChar(s[i]);
                i += 1;
            }
        }

        public static void WriteInt(short value)
        {
            short divisor = 1;

            while (divisor * 10 <= value)
            {
                divisor *= 10;
            }

            while (divisor > 0)
            {
                short c = Math16.Divide(value, divisor);
                Bios.WriteByte((byte)(Math16.Modulo(c, 10) + 48));

                divisor = Math16.Divide(divisor, 10);
            }
        }
    }
}
