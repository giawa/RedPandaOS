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

            if (LoadDiskWithRetry(0, 0, 2, 0x0900, 0x0000, disk, 8) &&    // stage2 of the boot loader 4kiB at 0x09000
                LoadDiskWithRetry(0, 0, 10, 0x0A00, 0x0000, disk, 128) && // kernel part 1 64kiB at 0x0A000
                LoadDiskWithRetry(0, 2, 12, 0x1A00, 0x0000, disk, 48))    // kernel part 2 24kiB at 0x1A000
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

        private static bool LoadDiskWithRetry(ushort cylinder, byte head, byte sector, ushort highAddr, ushort lowAddr, byte disk, byte sectors)
        {
            int retry = 0;
            ushort sectorsRead;

            sector = (byte)((sector & 0x3f) | ((cylinder >> 2) & 0xC0));

            do
            {
                sectorsRead = Bios.LoadDisk((byte)cylinder, head, sector, highAddr, lowAddr, disk, sectors);
                if (sectorsRead != sectors) Bios.ResetDisk();
            } while (sectorsRead != sectors && retry++ < 5);

            return sectorsRead == sectors;
        }
    }
}
