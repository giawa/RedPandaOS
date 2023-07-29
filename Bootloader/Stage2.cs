using CPUHelper;
using IL2Asm.BaseTypes;
using System;

namespace Bootloader
{
    public static class Stage2
    {
        private static CPU.GDT _gdt;
        private static CPU.TSSEntry _tss;
        private static Bios.SMAP_ret _smap_ret;

        [RealMode(0x9000)]
        public static void Start()
        {
            // the stage 1 kernel stored some information for us to load in once we get to stage 2
            byte disk = (byte)CPU.ReadMemShort(0x500);  // the disk as reported by the bios
            BiosUtilities.Partition partition = Runtime.Memory.Utilities.PtrToObject<BiosUtilities.Partition>(CPU.ReadMemShort(0x502));

            CPU.WriteMemory(0x500, (ushort)Runtime.Memory.Utilities.StructToPtr(ref _gdt));
            CPU.WriteMemory(0x502, (ushort)Runtime.Memory.Utilities.StructToPtr(ref _tss));
            CPU.WriteMemory(0x504, 0);
            if (DetectMemory(0x504, 10) == 0)
            {
                Panic("Failed to get memory map");
            }

            Bios.EnableA20();

            LoadKernel(disk, partition);

            EnterProtectedMode();
        }

        private static void EnsureFilesystem(BiosUtilities.Partition partition)
        {
            // fat volume table should be at memory region 0x9000-0x9200
            if (!HasName(0x9052, "FAT32 ", 6) || partition.PartitionType != 0x0B)
            {
                Panic("Unexpected file system type");
            }
        }

        private class DAP
        {
            public byte Size;
            public byte Unused;
            public ushort NumSectors;
            public ushort Addr0;
            public ushort Addr1;
            public ushort Lba0;
            public ushort Lba1;
            public ushort Lba2;
            public ushort Lba3;
        }

        private static void LoadLba(byte disk, byte heads, byte sectorsPerTrack, ushort lba, ushort addr)
        {
#if USE_DAP
            DAP dap = Kernel.Memory.Utilities.PtrToObject<DAP>(0x7c00);
            dap.Size = 16;
            dap.Unused = 0;
            dap.NumSectors = 1;
            dap.Addr0 = 0;
            dap.Addr1 = addr;
            dap.Lba0 = lba;
            dap.Lba1 = 0;
            dap.Lba2 = 0;
            dap.Lba3 = 0;

            if (!BiosUtilities.LoadDiskWithRetry(0x7c00, disk))
            {
                BiosUtilities.Write("LoadDiskWithRetry failed at addr 0x");
                BiosUtilities.WriteHex(addr);
                Panic(null);
            }
#else
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
                Panic(null);
            }
#endif
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
                if ((attr & 0xF8) == 0) // make sure this is a file
                {
                    if (HasName(offset, "KERNEL  BIN", 11))
                    {
                        kernelCluster = CPU.ReadMemShort((ushort)(offset + 0x1A));
                        var kernelFileSize1 = CPU.ReadMemShort((ushort)(offset + 0x1C));
                        var kernelFileSize2 = CPU.ReadMemShort((ushort)(offset + 0x1E));
                        kernelSectorCount = (ushort)((kernelFileSize1 >> 9) | (kernelFileSize2 << 7));
                        if ((kernelFileSize1 & 0x1ff) > 0) kernelSectorCount++;
                        break;
                    }
                }

                if (i == 15)
                {
                    Panic("Missing kernel.bin");
                }
            }

            return kernelCluster;
        }

        private static bool HasName(ushort offset, string name, ushort length)
        {
            for (ushort i = 0; i < length; i++)
            {
                var cpuByte = CPU.ReadMemByte((ushort)(offset + i));
                if (cpuByte != name[i]) return false;
            }
            return true;
        }

        private static void CopyFile(byte disk, byte heads, byte sectorsPerTrack, ushort sectorsPerCluster, ushort clusterBeginLba, ushort fileCluster, ushort fileSectorCount, ushort highAddr)
        {
            ushort sectorInCluster = 0;

            // assume fat is in 0x9000-0x9200 and has all the entries we need

            while (fileSectorCount > 0)
            {
                //ushort toCopy = (fileSize > 512 ? (ushort)512 : fileSize);

                var lba = (ushort)(clusterBeginLba + (fileCluster - 2) * sectorsPerCluster + sectorInCluster);
                LoadLba(disk, heads, sectorsPerTrack, lba, highAddr);

                highAddr += 0x20;
                fileSectorCount--;
                if (fileSectorCount == 0) break;

                // advance to the next sector
                if (sectorInCluster < sectorsPerCluster - 1) sectorInCluster++;
                else
                {
                    ushort index = (ushort)(0x9000 + fileCluster * 4);
                    var clusterLow = CPU.ReadMemShort(index);
                    var clusterHigh = CPU.ReadMemShort((ushort)(index + 2));

                    if (clusterHigh > 0)
                    {
                        Panic("Invalid next cluster");
                    }

                    sectorInCluster = 0;
                    fileCluster = clusterLow;
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
                if (CPU.ReadMemByte((ushort)(offset + 0x0B)) == 0x10)   // make sure this is a subdirectory
                {
                    // look for directory with name "BOOT" followed by empty padding
                    if (HasName(offset, "BOOT       ", 11))
                    {
                        bootCluster = CPU.ReadMemShort((ushort)(offset + 0x1A));
                        break;
                    }
                }

                if (i == 15)
                {
                    Panic("Missing /boot");
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
                BiosUtilities.Write("Bad geometry - using floppy defaults ");
                //sectorsPerTrack = 18;
                //heads = 2;
                sectorsPerTrack = 63;
                heads = 255;
            }

            // make sure this is a fat32 file system
            EnsureFilesystem(partition);

            const ushort volumeOffset = 0x9000;
            var sectorsPerCluster = CPU.ReadMemByte(volumeOffset + 0x0D);
            var reservedSectors = CPU.ReadMemShort(volumeOffset + 0x0E);
            var numberOfFats = CPU.ReadMemByte(volumeOffset + 0x10);
            var sectorsPerFat1 = CPU.ReadMemShort(volumeOffset + 0x24);
            var sectorsPerFat2 = CPU.ReadMemShort(volumeOffset + 0x26);
            if (sectorsPerFat2 > 0 || sectorsPerFat1 > 32768 - reservedSectors - partition.RelativeSector1)
            {
                Panic("FAT32: Too many sectors per FAT");
            }
            var rootDirectory1 = CPU.ReadMemShort(volumeOffset + 0x2C);
            var rootDirectory2 = CPU.ReadMemShort(volumeOffset + 0x2E);
            if (rootDirectory2 > 0)
            {
                Panic("FAT32: Invalid root directory");
            }

            // load the root directory into 0x9000-0x9200
            ushort fatBeginLba = (ushort)(partition.RelativeSector1 + reservedSectors);
            ushort clusterBeginLba = (ushort)(fatBeginLba + numberOfFats * sectorsPerFat1 + (rootDirectory1 - 2) * sectorsPerCluster);
            LoadLba(disk, heads, sectorsPerTrack, clusterBeginLba, 0x0900);

            var bootCluster = FindBootDirectory();

            // load the boot directory into 0x9000-0x9200
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
            _gdt.KernelCodeSegment.segmentLength = 0xffff;
            _gdt.KernelCodeSegment.flags1 = 0x9A;   // ReadWrite | Code | CodeDataSegment | Ring0 | Present
            _gdt.KernelCodeSegment.flags2 = 0xCF;   // LimitHigh | Big | Gran

            _gdt.KernelDataSegment.segmentLength = 0xffff;
            _gdt.KernelDataSegment.flags1 = 0x92;   // ReadWrite | CodeDataSegment | Ring0 | Present
            _gdt.KernelDataSegment.flags2 = 0xCF;   // LimitHigh | Big | Gran

            _gdt.UserCodeSegment.segmentLength = 0xffff;
            _gdt.UserCodeSegment.flags1 = (byte)(CPU.GDTFlags1.Ring3 | CPU.GDTFlags1.ReadWrite | CPU.GDTFlags1.Code | CPU.GDTFlags1.CodeDataSegment | CPU.GDTFlags1.Present);
            _gdt.UserCodeSegment.flags2 = (byte)(CPU.GDTFlags2.LimitHigh | CPU.GDTFlags2.Available | CPU.GDTFlags2.Big | CPU.GDTFlags2.Gran);

            _gdt.UserDataSegment.segmentLength = 0xffff;
            _gdt.UserDataSegment.flags1 = (byte)(CPU.GDTFlags1.Ring3 | CPU.GDTFlags1.ReadWrite | CPU.GDTFlags1.CodeDataSegment | CPU.GDTFlags1.Present);
            _gdt.UserDataSegment.flags2 = (byte)(CPU.GDTFlags2.LimitHigh | CPU.GDTFlags2.Available | CPU.GDTFlags2.Big | CPU.GDTFlags2.Gran);

            _gdt.TaskStateSegment.segmentLength = 104;  // Marshal.SizeOf<TSSEntry>()
            _gdt.TaskStateSegment.segmentBase1 = (ushort)Runtime.Memory.Utilities.StructToPtr(ref _tss);
            _gdt.TaskStateSegment.flags1 = 0x89;    // Accessed | Code | Present
            _gdt.TaskStateSegment.flags2 = 0x00;

            Bios.EnterProtectedMode(ref _gdt);

            // code should never get here since EnterProtectedMode jumps to 0xA000
            Panic("Failed to enter protected mode");
        }

        private static void Panic(string s)
        {
            if (s != null) BiosUtilities.Write(s);

            while (true) ;
        }

        public static ushort DetectMemory(ushort address, int maxEntries)
        {
            ushort entries = 0;

            do
            {
                if (Bios.DetectMemory((ushort)(address + entries * 24 + 4), ref _smap_ret) == 0xff) break;
                entries++;
            } while ((_smap_ret.contId1 != 0 || _smap_ret.contId2 != 0) && entries < maxEntries);

            CPU.WriteMemory(address, entries);

            return entries;
        }
    }
}
