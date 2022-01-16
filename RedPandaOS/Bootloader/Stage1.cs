using CPUHelper;
using IL2Asm.BaseTypes;

namespace Bootloader
{
    static class Stage1
    {
        private static ushort cylinder, head, sector, retry;
        //private static ushort partitionAddress = 0x7C00 + 0x01BE;

        [BootSector]
        [RealMode(0x7C00)]
        public static void Start()
        {
            // BIOS stores the disk in dl, the lowest 8 bits of dx
            byte disk = (byte)CPU.ReadDX();

            // load the MBR and try to find a bootable partition
            ushort partitionAddress = 0x7C00 + 0x01BE;
            BiosUtilities.Partition partition;
            do
            {
                partition = Kernel.Memory.Utilities.PtrToObject<BiosUtilities.Partition>(partitionAddress);
                if (partition.Bootable == 0x80) break;
                partitionAddress += 16;
            } while (partitionAddress < 0x7e00);

            // store the disk and bootable partition somewhere the second stage bootloader can get to
            CPU.WriteMemory(0x500, disk);
            CPU.WriteMemory(0x502, partitionAddress);

            // get the disk geometry
            partitionAddress = Bios.GetGeometry(disk);
            ushort heads = (ushort)((partitionAddress >> 8) + 1);
            ushort sectorsPerTrack = (ushort)(partitionAddress & 0x3f);

            // convert LBA value to CHS
            //int cylinder = 0, head = 0, sector = partition.RelativeSector1;
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

            // make sure the partition is bootable and FAT32 before trying to load it
            if (partition.Bootable == 0x80 && partition.PartitionType == 0x0B)
            {
                sector = (ushort)(sector | (ushort)((cylinder >> 2) & 0xC0));

                do
                {
                    partitionAddress = Bios.LoadDisk(cylinder, head, sector, 0x0900, disk, 8);
                    if (partitionAddress != 8) Bios.ResetDisk();
                    else CPU.Jump(0x9200);    // first sector is FAT32 volume, so jump into second sector (+512)
                } while (retry++ < 5);

                BiosUtilities.Write("Disk");
            }
            else BiosUtilities.Write("Partition");

            BiosUtilities.Write(" Fail");

            while (true) ;
        }
    }
}
