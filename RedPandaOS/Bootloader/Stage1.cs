using CPUHelper;
using IL2Asm.BaseTypes;

namespace Bootloader
{
    static class Stage1
    {
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
                partitionAddress += 16;
            } while ((partition.Bootable & 0x80) != 0x80 && partitionAddress < 0x7e00);

            // store the disk and bootable partition somewhere the second stage bootloader can get to
            CPU.WriteMemory(0x500, disk);
            CPU.WriteMemory(0x502, (ushort)(partitionAddress - 16));

            if ((partition.Bootable & 0x80) == 0x80)
            {
                // if we found a bootable partition then try to load the stage 2 bootloader
                if (BiosUtilities.LoadDiskWithRetry(partition, 0x0900, 0x0000, disk, 8))
                {
                    CPU.Jump(0x9000);
                }

                BiosUtilities.Write("Disk");
            }
            else BiosUtilities.Write("Partition");

            BiosUtilities.Write(" Fail");

            while (true) ;
        }
    }
}
