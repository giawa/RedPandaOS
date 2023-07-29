using Runtime.Collections;
using System;
using Kernel.Devices;
using Runtime.Memory;

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

            //_buffer = new uint[512 / 4];    // full sector, 512 bytes
            _buffers = new List<CachedBuffer>(4);
            //for (int i = 0; i < 4; i++) _buffers.Add(new CachedBuffer());

            // check for an MBR
            var sector0 = ReadSector(0, 1);

            Memory.SplitBumpHeap.Instance.PrintSpace();

            if (sector0[511] == 0xAA && sector0[510] == 0x55)
            {
                // looks for an MBR
                uint offset = 0x01BE;
                Partition partition;
                do
                {
                    // offset + 8 due to the first 8 bytes being the array type and size
                    partition = Utilities.PtrToObject<Partition>(Utilities.ObjectToPtr(sector0) + offset + 8);
                    if (partition.PartitionType != 0)
                    {
                        var clone = Memory.Fast.Clone(partition, 16);
                        _partitions.Add(clone);

                        if (clone.PartitionType == 0x0b || clone.PartitionType == 0x0c)
                            AttachHardDisk(clone);
                    }
                    offset += 16;
                } while (offset < 0x0200);
            }

            //Array.Clear(_buffer, 0, 512 / 4);

            Logging.WriteLine(LogLevel.Warning, "Found {0} partitions", (uint)_partitions.Count);
            for (int i = 0; i < _partitions.Count; i++)
                Logging.WriteLine(LogLevel.Warning, "Found partition {0:X} offset {1:X} size {2:X}", _partitions[i].PartitionType, _partitions[i].RelativeSector, _partitions[i].TotalSectors);
        }

        //private uint[] _buffer;
        private List<CachedBuffer> _buffers;

        public class CachedBuffer
        {
            public uint AccessSinceUsed;
            public uint[] Buffer;
            public uint Lba;
            public uint Sectors;

            public CachedBuffer(uint sectorsPerBuffer)
            {
                AccessSinceUsed = 0;
                Buffer = new uint[128 * sectorsPerBuffer];
                Lba = uint.MaxValue;
                Sectors = sectorsPerBuffer;
            }
        }

        public void WriteSector(uint lba, byte[] data)
        {
            // TODO:  Keep in _buffers and mark as dirty.  Only flush to disk when falling out of _buffers
            byte result = PATA.Access(1, 0, lba, 1, 0, Utilities.UnsafeCast<uint[]>(data));
        }

        public byte ReadSector(uint lba, ushort sectorsPerCluster, byte[] buffer)
        {
            //return PATA.Access(0, 0, lba, (byte)sectorsPerCluster, 0, Memory.Utilities.UnsafeCast<uint[]>(buffer));
            return PATA.AccessWithDMASimple(0, lba, (byte)sectorsPerCluster, Utilities.UnsafeCast<uint[]>(buffer));
        }

        public byte Read(uint[] lbas, PATA.PRDT[] dmas)
        {
            return 0;
        }

        public byte[] ReadSector(uint lba, ushort sectorsPerCluster)
        {
            CachedBuffer buffer = null;
            bool hasCache = false;

            for (int i = 0; i < _buffers.Count; i++)
            {
                if (_buffers[i].Sectors == sectorsPerCluster)
                {
                    hasCache = true;
                    if (_buffers[i].Lba == lba && _buffers[i].Sectors == sectorsPerCluster)
                    {
                        buffer = _buffers[i];
                        buffer.AccessSinceUsed = 0;
                    }
                    else _buffers[i].AccessSinceUsed++;
                }
                //if (_buffers[i].Sectors == sectorsPerCluster) buffer = _buffers[i];
            }

            if (!hasCache)
            {
                for (int i = 0; i < 4; i++)
                {
                    _buffers.Add(new CachedBuffer(sectorsPerCluster));
                }
            }
            /*if (buffer == null)
            {
                buffer = new CachedBuffer(sectorsPerCluster);
                _buffers.Add(buffer);
            }*/

            if (buffer == null)
            {
                // find the worst buffer
                uint worst = 0;

                for (int i = 0; i < _buffers.Count; i++)
                {
                    if (_buffers[i].Sectors == sectorsPerCluster && _buffers[i].AccessSinceUsed >= worst)
                    {
                        buffer = _buffers[i];
                        worst = buffer.AccessSinceUsed;
                    }
                }

                //byte result = PATA.Access(0, 0, lba, (byte)sectorsPerCluster, 0, buffer.Buffer);
                byte result = PATA.AccessWithDMASimple(0, lba, (byte)sectorsPerCluster, buffer.Buffer);
                buffer.AccessSinceUsed = 0;
                buffer.Lba = lba;
            }

            return Utilities.UnsafeCast<byte[]>(buffer.Buffer);
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
            devices.AddDirectory(harddisk);

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
