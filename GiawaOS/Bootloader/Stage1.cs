using CPUHelper;

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

            if (LoadDiskWithRetry(0x0000, 0x9000, disk, 24))
            {
                CPU.Jump(0x9000);
            }
            else
            {
                BiosUtilities.Write("Failed to read from disk 0x");
                BiosUtilities.WriteHex(disk);
            }

            while (true) ;
        }

        private static bool LoadDiskWithRetry(ushort highAddr, ushort lowAddr, byte disk, byte sectors)
        {
            int retry = 0;
            ushort sectorsRead;

            do
            {
                sectorsRead = Bios.LoadDisk(highAddr, lowAddr, disk, sectors);
            } while (sectorsRead != sectors && retry++ < 3);

            return sectorsRead == sectors;
        }
    }
}
