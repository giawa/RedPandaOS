using Runtime.Collections;
using System;
using Kernel.Devices;

namespace Kernel.IO
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
        public uint RelativeSector;
        public uint TotalSectors;
    }

    public class DiskWithPartition
    {
        public Partition Partition;
        public Disk Disk;

        public DiskWithPartition(Disk disk, Partition partition)
        {
            Disk = disk;
            Partition = partition;
        }
    }

    public class Disk
    {
        private PATA.Device _pataDisk;
        private List<Partition> _partitions;

        public enum DiskType
        {
            Unformatted,
            Unsupported,
            FAT32,
            exFAT,
        }

        public static void AddDevice(PATA.Device disk)
        {
            new Disk(disk);
        }

        private Disk(PATA.Device disk)
        {
            _pataDisk = disk;
            _partitions = new List<Partition>();

            _buffer = new uint[512 / 4];    // full sector, 512 bytes

            // check for an MBR
            var sector0 = ReadSector(0);

            Memory.SplitBumpHeap.Instance.PrintSpace();

            if (sector0[511] == 0xAA && sector0[510] == 0x55)
            {
                // looks for an MBR
                uint offset = 0x01BE;
                Partition partition;
                do
                {
                    // offset + 8 due to the first 8 bytes being the array type and size
                    partition = Memory.Utilities.PtrToObject<Partition>(Memory.Utilities.ObjectToPtr(sector0) + offset + 8);
                    if (partition.PartitionType != 0)
                    {
                        var clone = Memory.Utilities.Clone(partition, 16);
                        _partitions.Add(clone);

                        if (clone.PartitionType == 0x0b || clone.PartitionType == 0x0c)
                            AttachHardDisk(clone);
                    }
                    offset += 16;
                } while (offset < 0x0200);
            }

            Array.Clear(_buffer, 0, 512 / 4);

            Logging.WriteLine(LogLevel.Warning, "Found {0} partitions", (uint)_partitions.Count);
            for (int i = 0; i < _partitions.Count; i++)
                Logging.WriteLine(LogLevel.Warning, "Found partition {0:X} offset {1:X} size {2:X}", _partitions[i].PartitionType, _partitions[i].RelativeSector, _partitions[i].TotalSectors);
        }

        private uint[] _buffer;

        public byte[] ReadSector(uint lba)
        {
            byte result = PATA.Access(0, 0, lba, 1, 0, _buffer);

            return Memory.Utilities.UnsafeCast<byte[]>(_buffer);
        }

        private static char diskletter = 'a';

        private void AttachHardDisk(Partition partition)
        {
            var root = IO.Filesystem.Root;
            IO.Directory devices = null;
            for (int i = 0; i < root.Directories.Count; i++)
                if (root.Directories[i].Name == "dev") devices = root.Directories[i];

            if (devices == null) throw new Exception("/dev did not exist");

            IO.Directory harddisk = new Directory("hd" + diskletter, devices);
            devices.Directories.Add(harddisk);

            if (partition.PartitionType == 0x0b || partition.PartitionType == 0x0c)
            {
                DiskWithPartition partitionWithDisk = new DiskWithPartition(this, partition);
                FAT32 fileSystem = new FAT32(partitionWithDisk);
                harddisk.OnOpen = fileSystem.OnExploreRoot;
            }

            if (diskletter == 'a')
                Exceptions.ReadSymbols(0);

            diskletter++;
        }
    }
}
