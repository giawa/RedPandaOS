using CPUHelper;
using IL2Asm.BaseTypes;

namespace Bootloader
{
    public static class Stage2
    {
        private static CPU.GDT _gdt;
        private static Bios.SMAP_ret _smap_ret;

        [RealMode(0x9000)]
        public static void Start()
        {
            // the stage 1 kernel stored some information for us to load in once we get to stage 2
            byte disk = (byte)CPU.ReadMemShort(0x500);  // the disk as reported by the bios
            BiosUtilities.Partition partition = Kernel.Memory.Utilities.PtrToObject<BiosUtilities.Partition>(CPU.ReadMemShort(0x502));

            var geometry = Bios.GetGeometry(disk);
            byte heads = (byte)((geometry >> 8) + 1);
            byte sectorsPerTrack = (byte)(geometry & 0x3f);

            // sanity check floppy drive geometry
            if (disk <= 0x01 && heads != 2)
            {
                // floppy drive defaults
                BiosUtilities.Write("Bad geometry - using floppy defaults");
                sectorsPerTrack = 18;
                heads = 2;
            }

            // read the kernel into memory
            AdvanceSectors(partition, 8, heads, sectorsPerTrack);

            for (ushort addr = 0x0A00; addr < 0x2000; addr += 0x20)
            {
                if (!BiosUtilities.LoadDiskWithRetry(partition, addr, 0x0000, disk, 1))
                {
                    BiosUtilities.Write("LoadDiskWithRetry failed at addr 0x");
                    BiosUtilities.WriteHex(addr);
                    BiosUtilities.WriteLine();
                    ShowDiskFailure();
                }
                AdvanceSectors(partition, 1, heads, sectorsPerTrack);
            }

            if (DetectMemory(0x500, 10) == 0)
            {
                BiosUtilities.Write("Failed to get memory map");
                while (true) ;
            }

            Bios.EnableA20();

            _gdt.KernelCodeSegment.segmentLength = 0xffff;
            _gdt.KernelCodeSegment.flags1 = 0x9A;
            _gdt.KernelCodeSegment.flags2 = 0xCF;

            _gdt.KernelDataSegment.segmentLength = 0xffff;
            _gdt.KernelDataSegment.flags1 = 0x92;
            _gdt.KernelDataSegment.flags2 = 0xCF;

            Bios.EnterProtectedMode(ref _gdt);
            BiosUtilities.Write("Failed to enter protected mode");

            while (true) ;
        }

        private static void AdvanceSectors(BiosUtilities.Partition partition, ushort sectors, byte heads, byte sectorsPerTrack)
        {
            ushort startSector = partition.StartingSector;
            ushort startHead = partition.StartingHead;
            ushort startCylinder = partition.StartingCylinder;

            startSector += sectors;

            while (startSector > sectorsPerTrack)
            {
                startSector -= sectorsPerTrack;
                startHead++;
            }

            while (startHead >= heads)
            {
                startHead -= heads;
                startCylinder++;
            }

            // sector occupies the lower 6 bits, with the top 2 bits of cylinder occupying the top 2 bits of the sector
            partition.StartingSector = (byte)((startSector & 0x3f) | ((startCylinder >> 2) & 0xC0));
            partition.StartingHead = (byte)startHead;
            partition.EndingCylinder = (byte)startCylinder;
        }

        private static void ShowDiskFailure()
        {
            BiosUtilities.Write("Failed to load kernel from disk");

            while (true) ;
        }

        public static ushort DetectMemory(ushort address, int maxEntries)
        {
            ushort entries = 0;

            do
            {
                if (Bios.DetectMemory((ushort)(address + entries * 24 + 2), ref _smap_ret) == 0xff) break;
                entries++;
            } while ((_smap_ret.contId1 != 0 || _smap_ret.contId2 != 0) && entries < maxEntries);

            CPU.WriteMemory(address, entries);

            return entries;
        }
    }
}
