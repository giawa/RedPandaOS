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

            LoadKernel(disk, partition);

            EnterProtectedMode();
        }

        private static void EnsureFilesystem(BiosUtilities.Partition partition)
        {
            // fat volume table should be at memory region 0x9000-0x9200
            var FA = CPU.ReadMemShort(0x9000 + 0x52);
            var T3 = CPU.ReadMemShort(0x9000 + 0x54);
            var _2 = CPU.ReadMemShort(0x9000 + 0x56);

            if (FA != 0x4146 || T3 != 0x3354 || _2 != 0x2032 || partition.PartitionType != 0x0B)
            {
                BiosUtilities.Write("Unexpected file system type");
                while (true) ;
            }
        }

        private static void LoadLba(byte disk, byte heads, byte sectorsPerTrack, ushort lba, ushort addr)
        {
            var sector = lba;
            ushort head = 0, cylinder = 0;
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

            if (!BiosUtilities.LoadDiskWithRetry(cylinder, (byte)head, (byte)sector, addr, 0, disk, 1))
            {
                BiosUtilities.Write("LoadDiskWithRetry failed at addr 0x");
                BiosUtilities.WriteHex(addr);
                BiosUtilities.WriteLine();
                ShowDiskFailure();
            }
        }

        private static ushort kernelSectorCount = 0;

        private static ushort FindKernelFile()
        {
            ushort kernelCluster = 0;

            // search for kernel.bin in the /boot directory
            for (short i = 0; i < 16; i++)
            {
                var offset = (ushort)(0x9000 + (i << 5));
                var attr = CPU.ReadMemByte((ushort)(offset + 0x0B));
                if (attr == 0x00 || attr == 0x04)
                {
                    // directory entry
                    var KE = CPU.ReadMemShort(offset);
                    var RN = CPU.ReadMemShort((ushort)(offset + 2));
                    var EL = CPU.ReadMemShort((ushort)(offset + 4));
                    var empty = CPU.ReadMemShort((ushort)(offset + 6));
                    var BI = CPU.ReadMemShort((ushort)(offset + 8));
                    var N = CPU.ReadMemByte((ushort)(offset + 10));

                    // look for directory with name "BOOT" followed by empty padding
                    if (KE == 0x656B && RN == 0x6E72 && EL == 0x6C65 && empty == 0x0000 && BI == 0x6962 && N == 0x6E)
                    {
                        kernelCluster = CPU.ReadMemShort((ushort)(offset + 0x1A));
                        var kernelFileSize1 = CPU.ReadMemShort((ushort)(offset + 0x1C));
                        var kernelFileSize2 = CPU.ReadMemShort((ushort)(offset + 0x1E));
                        BiosUtilities.WriteLine(); BiosUtilities.WriteHex(kernelFileSize1);
                        BiosUtilities.WriteLine(); BiosUtilities.WriteHex(kernelFileSize2);
                        kernelSectorCount = (ushort)((kernelFileSize1 >> 9) | (kernelFileSize2 << 7));
                        if ((kernelFileSize1 & 0x1ff) > 0) kernelSectorCount++;
                        BiosUtilities.WriteLine(); BiosUtilities.WriteHex(kernelSectorCount);
                        BiosUtilities.WriteLine(); BiosUtilities.WriteHex(kernelCluster);
                        break;
                    }
                }

                if (i == 15)
                {
                    BiosUtilities.Write("Could not find kernel.bin");
                    while (true) ;
                }
            }

            return kernelCluster;
        }

        private static void CopyFile(byte disk, byte heads, byte sectorsPerTrack, ushort sectorsPerCluster, ushort clusterBeginLba, ushort fileCluster, ushort fileSectorCount, ushort highAddr)
        {
            ushort sectorInCluster = 0;

            // assume fat is in 0x9000-0x9200 and has all the entries we need

            BiosUtilities.WriteLine();
            BiosUtilities.Write("Starting at cluster ");
            BiosUtilities.WriteHex(fileCluster); BiosUtilities.WriteLine();

            while (fileSectorCount > 0)
            {
                //ushort toCopy = (fileSize > 512 ? (ushort)512 : fileSize);

                var lba = (ushort)(clusterBeginLba + (fileCluster - 2) * sectorsPerCluster + sectorInCluster);
                LoadLba(disk, heads, sectorsPerTrack, lba, highAddr);

                highAddr += 0x20;
                fileSectorCount--;

                // advance to the next sector
                if (sectorInCluster < sectorsPerCluster - 1) sectorInCluster++;
                else
                {
                    ushort index = (ushort)(0x9000 + fileCluster * 4);
                    var clusterLow = CPU.ReadMemShort(index);
                    var clusterHigh = CPU.ReadMemShort((ushort)(index + 2));

                    if (clusterHigh > 0)
                    {
                        BiosUtilities.Write("Next cluster was in the wrong place, or file ended prematurely");
                        BiosUtilities.WriteHex(fileSectorCount);
                        while (true) ;
                    }

                    sectorInCluster = 0;
                    fileCluster = clusterLow;

                    BiosUtilities.Write("Moving to cluster ");
                    BiosUtilities.WriteHex(fileCluster);
                    //BiosUtilities.WriteLine();
                }
            }
        }

        private static ushort FindBootDirectory()
        {
            ushort bootCluster = 0;

            // search for the /boot directory in the root directory
            for (short i = 0; i < 16; i++)
            {
                var offset = (ushort)(0x9000 + (i << 5));
                if (CPU.ReadMemByte((ushort)(offset + 0x0B)) == 0x10)
                {
                    // directory entry
                    var BO = CPU.ReadMemShort(offset);
                    var OT = CPU.ReadMemShort((ushort)(offset + 2));
                    var empty = CPU.ReadMemShort((ushort)(offset + 6));

                    // look for directory with name "BOOT" followed by empty padding
                    if (BO == 0x6F62 && OT == 0x746F && empty == 0x0000)
                    {
                        bootCluster = CPU.ReadMemShort((ushort)(offset + 0x1A));
                        break;
                    }
                }

                if (i == 15)
                {
                    BiosUtilities.Write("Could not find /BOOT");
                    while (true) ;
                }
            }

            return bootCluster;
        }

        private static void LoadKernel(byte disk, BiosUtilities.Partition partition)
        {
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

            EnsureFilesystem(partition);

            const ushort volumeOffset = 0x9000;
            var sectorsPerCluster = CPU.ReadMemByte(volumeOffset + 0x0D);
            var reservedSectors = CPU.ReadMemShort(volumeOffset + 0x0E);
            var numberOfFats = CPU.ReadMemByte(volumeOffset + 0x10);
            var sectorsPerFat1 = CPU.ReadMemShort(volumeOffset + 0x24);
            var sectorsPerFat2 = CPU.ReadMemShort(volumeOffset + 0x26);
            if (sectorsPerFat2 > 0 || sectorsPerFat1 > 32768 - reservedSectors - partition.RelativeSector1)
            {
                BiosUtilities.Write("Invalid FAT32 partition - too many sectors per FAT");
                while (true) ;
            }
            var rootDirectory1 = CPU.ReadMemShort(volumeOffset + 0x2C);
            var rootDirectory2 = CPU.ReadMemShort(volumeOffset + 0x2E);
            if (rootDirectory2 > 0)
            {
                BiosUtilities.Write("Invalid FAT32 partition - invalid root directory");
                while (true) ;
            }

            // load the root directory into 0x9000-0x9200
            ushort fatBeginLba = (ushort)(partition.RelativeSector1 + reservedSectors);
            ushort clusterBeginLba = (ushort)(fatBeginLba + numberOfFats * sectorsPerFat1 + (rootDirectory1 - 2) * sectorsPerCluster);

            LoadLba(disk, heads, sectorsPerTrack, clusterBeginLba, 0x0900);

            var bootCluster = FindBootDirectory();

            bootCluster = (ushort)(clusterBeginLba + (bootCluster - 2) * sectorsPerCluster);
            LoadLba(disk, heads, sectorsPerTrack, bootCluster, 0x0900);

            var kernelCluster = FindKernelFile();

            // copy fat into 0x9000-0x9200
            LoadLba(disk, heads, sectorsPerTrack, fatBeginLba, 0x0900);

            // now copy the kernel into 0xA000 memory
            CopyFile(disk, heads, sectorsPerTrack, sectorsPerCluster, clusterBeginLba, kernelCluster, kernelSectorCount, 0x0A00);
        }

        private static void EnterProtectedMode()
        {
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
