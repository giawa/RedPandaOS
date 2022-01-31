using System;
using Runtime.Collections;

namespace Kernel.IO
{
    public class FAT32
    {
        private DiskWithPartition _partition;

        public FAT32(DiskWithPartition partition)
        {
            _partition = partition;

            Initialize(partition);
        }

        private void Panic(string s)
        {
            throw new System.Exception(s);
        }

        private byte[] EnsureFilesystem(DiskWithPartition partition)
        {
            var firstSector = partition.Disk.ReadSector(partition.Partition.RelativeSector);

            if (!HasName(firstSector, 0x52, "FAT32 ", 6) || partition.Partition.PartitionType != 0x0B)
            {
                Panic("Unexpected file system type");
            }

            return firstSector;
        }

        private byte[] LoadLba(uint lba)
        {
            return _partition.Disk.ReadSector(lba);
        }

        private bool HasName(byte[] data, ushort offset, string name, ushort length)
        {
            for (ushort i = 0; i < length; i++)
            {
                var cpuByte = data[offset + i];
                if (cpuByte != name[i]) return false;
            }
            return true;
        }

        private void CopyFile(ushort sectorsPerCluster, uint clusterBeginLba, uint fileCluster, uint fileSectorCount)
        {
            ushort sectorInCluster = 0;

            while (fileSectorCount > 0)
            {
                var lba = (clusterBeginLba + (fileCluster - 2) * sectorsPerCluster + sectorInCluster);
                var data = LoadLba(lba);

                // todo - actually do something with this data?

                fileSectorCount--;
                if (fileSectorCount == 0) break;

                // advance to the next sector
                if (sectorInCluster < sectorsPerCluster - 1) sectorInCluster++;
                else
                {
                    fileCluster = GetNextCluster(fileCluster);

                    sectorInCluster = 0;
                }
            }
        }

        private uint GetNextCluster(uint currentCluster)
        {
            return currentCluster + 1;
        }

        private byte _sectorsPerCluster, _numberOfFats;
        private ushort _reservedSectors;
        private uint _sectorsPerFat, _rootDirectory;
        private uint _fatBeginLba, _clusterBeginLba;

        private void Initialize(DiskWithPartition partition)
        {
            // make sure this is a fat32 file system
            var firstSector = EnsureFilesystem(partition);

            // EnsureFilesystem has already loaded the first sector of the file system, so just use that
            _sectorsPerCluster = firstSector[0x0D];
            _reservedSectors = BitConverter.ToUInt16(firstSector, 0x0E);
            _numberOfFats = firstSector[0x10];
            _sectorsPerFat = BitConverter.ToUInt32(firstSector, 0x24);
            _rootDirectory = BitConverter.ToUInt32(firstSector, 0x2C);

            // load the root directory into 0x9000-0x9200
            _fatBeginLba = partition.Partition.RelativeSector + _reservedSectors;
            _clusterBeginLba = _fatBeginLba + _numberOfFats * _sectorsPerFat + (_rootDirectory - 2) * _sectorsPerCluster;
        }

        private int GetFilenameSize(byte[] data, int offset)
        {
            int size = 0;
            var type = data[offset + 0x0B];

            if ((type & 0x10) == 0x10)
            {
                for (int i = 0; i < 11; i++)
                {
                    if (data[offset + i] == 0x20) break;
                    size++;
                }
            }
            else
            {
                size++; // for period
                for (int i = 0; i < 8; i++)
                {
                    if (data[offset + i] == 0x20) break;
                    size++;
                }
                for (int i = 0; i < 3; i++)
                {
                    if (data[offset + i + 8] == 0x20) break;
                    size++;
                }
            }

            return size;
        }

        private void ExploreDirectory(uint lba, Directory dir)
        {
            var sectorData = LoadLba(lba);

            for (int i = 0; i < 16; i++)
            {
                var offset = (i << 5);
                if (sectorData[offset] == 0) continue;

                var type = sectorData[offset + 0x0B];
                if ((type & 0x10) == 0x10)
                {
                    // this is a subdirectory
                    char[] name = new char[GetFilenameSize(sectorData, offset)];
                    for (int j = 0; j < name.Length; j++) name[j] = (char)sectorData[offset + j];

                    uint dirCluster = BitConverter.ToUInt16(sectorData, offset + 0x1A);
                    dirCluster |= (uint)BitConverter.ToUInt16(sectorData, offset + 0x14) << 16;

                    var newDirectory = new Directory(new string(name), dir);
                    dir.Directories.Add(newDirectory);
                    newDirectory.OnOpen = OnExplore;
                    newDirectory.FilesystemInformation = dirCluster;
                }
                else if (type == 0x0F)
                {
                    // this is a VFAT long name
                }
                else if (type != 0x08)
                {
                    // this is a file
                    char[] name = new char[GetFilenameSize(sectorData, offset)];
                    int curPos = 0;
                    for (int j = 0; j < 8; j++, curPos++)
                    {
                        if (sectorData[offset + j] == 0x20) break;
                        name[j] = (char)sectorData[offset + j];
                    }
                    name[curPos++] = '.';
                    for (int j = 0; j < 3; j++)
                    {
                        if (sectorData[offset + j + 8] == 0x20) break;
                        name[curPos++] = (char)sectorData[offset + j + 8];
                    }

                    var newFile = new File(new string(name), dir);
                    dir.Contents.Add(newFile);
                }
            }
        }

        public void OnExploreRoot(Directory dir)
        {
            if (dir.Opened) return;

            ExploreDirectory(_clusterBeginLba, dir);

            dir.Opened = true;
        }

        public void OnExplore(Directory dir)
        {
            if (dir.Opened) return;

            var lba = _fatBeginLba + _numberOfFats * _sectorsPerFat + (dir.FilesystemInformation - 2) * _sectorsPerCluster;
            ExploreDirectory(lba, dir);

            dir.Opened = true;
        }
    }
}