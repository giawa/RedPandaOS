using CPUHelper;
using IL2Asm.BaseTypes;

namespace Bootloader
{
    static class FATStage1
    {
        [BootSector]
        [RealMode(0x7C00)]
        public static void Start()
        {
            // BIOS stores the disk in dl, the lowest 8 bits of dx
            byte disk = (byte)CPU.ReadDX();
            ushort retry = 0;

            BiosUtilities.Write("Stage 1");

            // store the disk and bootable partition somewhere the second stage bootloader can get to
            CPU.WriteMemory(0x500, disk);
            CPU.WriteMemory(0x502, 0x7DE0);

            do
            {
                var sectorsRead = Bios.LoadDisk(0, 0, 1, 0x0900, disk, 8);
                if (sectorsRead != 8) Bios.ResetDisk();
                else CPU.Jump(0x9200);    // jump to first (non boot sector) reserved sector of the file system we copied over
            } while (retry++ < 5);

            BiosUtilities.Write("Disk Fail");

            while (true) ;
        }
    }

    static class Stage1
    {
        // saves a few bytes by allocating them statically instead of on the stack
        private static ushort cylinder, head, sector, retry;

        [BootSector]
        [RealMode(0x7C00)]
        public static void Start()
        {
            // BIOS stores the disk in dl, the lowest 8 bits of dx
            byte disk = (byte)CPU.ReadDX();

            // load the MBR and try to find a bootable partition
            ushort commonVariable = 0x7C00 + 0x01BE;
            BiosUtilities.Partition partition;
            do
            {
                partition = Runtime.Memory.Utilities.PtrToObject<BiosUtilities.Partition>(commonVariable);
                if (partition.Bootable == 0x80) break;
                commonVariable += 16;
            } while (commonVariable < 0x7e00);

            // store the disk and bootable partition somewhere the second stage bootloader can get to
            CPU.WriteMemory(0x500, disk);
            CPU.WriteMemory(0x502, commonVariable);

            // get the disk geometry
            commonVariable = Bios.GetGeometry(disk);
            ushort heads = (ushort)((commonVariable >> 8) + 1);
            ushort sectorsPerTrack = (ushort)(commonVariable & 0x3f);

            // convert LBA value to CHS
            sector = partition.RelativeSector1;
            sector++;   // advance because LBA is indexed from 0, but sectors are from 1

            while (sector > sectorsPerTrack)
            {
                sector -= sectorsPerTrack;
                head++;
            }

            while (head >= heads)
            {
                head -= heads;
                cylinder++;
            }

            // make sure the partition is bootable and then load it, assuming a VBR exists at that partition
            if (partition.Bootable == 0x80)
            {
                sector = (ushort)(sector | (ushort)((cylinder >> 2) & 0xC0));

                do
                {
                    commonVariable = Bios.LoadDisk(cylinder, head, sector, 0x0900, disk, 8);
                    if (commonVariable != 8) Bios.ResetDisk();
                    else CPU.Jump(0x9200);    // jump to first (non boot sector) reserved sector of the file system we copied over
                } while (retry++ < 5);

                BiosUtilities.Write("Disk");
            }
            else BiosUtilities.Write("Partition");

            BiosUtilities.Write(" Fail");

            while (true) ;
        }
    }
}
