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

        protected byte[] LoadLba(uint lba)
        {
            return _partition.Disk.ReadSector(lba);
        }

        protected void WriteLba(uint lba, byte[] data)
        {
            _partition.Disk.WriteSector(lba, data);
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

                    uint fileCluster = BitConverter.ToUInt16(sectorData, offset + 0x1A);
                    fileCluster |= (uint)BitConverter.ToUInt16(sectorData, offset + 0x14) << 16;

                    var newFile = new File(new string(name), dir);
                    newFile.OnOpen = OnOpen;
                    newFile.FileSystem = this;
                    dir.Contents.Add(newFile);
                    newFile.FilesystemInformation = fileCluster;
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

        public void OnOpen(File file)
        {
            SectorHandler handler = new SectorHandler(this, _fatBeginLba, _clusterBeginLba, _sectorsPerFat, _sectorsPerCluster);
            handler.SetCluster(file.FilesystemInformation);

            _files.Add(file.FilesystemInformation);
            _handlers.Add(handler);

            Logging.WriteLine(LogLevel.Warning, "SetCluster {0:X}", file.FilesystemInformation);
        }

        public byte[] GetFirstSector(File file)
        {
            SectorHandler handler = null;
            for (int i = 0; i < _files.Count; i++)
            {
                if (_files[i] == file.FilesystemInformation)
                    handler = _handlers[i];
            }

            if (handler == null) throw new Exception("No handler existed for this file, use OnOpen first");

            handler.SetCluster(file.FilesystemInformation);

            return handler.GetCurrentSector();
        }

        public byte[] OnReadSector(File file)
        {
            SectorHandler handler = null;
            for (int i = 0; i < _files.Count; i++)
            {
                if (_files[i] == file.FilesystemInformation)
                    handler = _handlers[i];
            }

            if (handler == null) throw new Exception("No handler existed for this file, use OnOpen first");

            if (handler.AdvanceRead()) return handler.GetCurrentSector();
            else return null;
        }

        private List<uint> _files = new List<uint>();
        private List<SectorHandler> _handlers = new List<SectorHandler>();

        public class SectorHandler
        {
            private FAT32 _fat32;

            public byte[] GetCurrentSector()
            {
                return _fat32.LoadLba(_clusterBeginLba + (ClusterId - 2) * _sectorsPerCluster + _sectorInCluster);
            }

            public uint ClusterId { get; private set; }

            private byte _sectorInCluster;

            private uint _fatBeginLba, _clusterBeginLba;
            private uint _sectorsPerFat;
            private byte _sectorsPerCluster;

            public SectorHandler(FAT32 fat32, uint fatBeginLba, uint clusterBeginLba, uint sectorsPerFat, uint sectorsPerCluster)
            {
                _fat32 = fat32;
                _fatBeginLba = fatBeginLba;
                _clusterBeginLba = clusterBeginLba;
                _sectorsPerFat = sectorsPerFat;
                _sectorsPerCluster = (byte)sectorsPerCluster;
            }

            public void SetCluster(uint cluster)
            {
                ClusterId = cluster;
                _sectorInCluster = 0;
            }

            public bool AdvanceRead()
            {
                if (_sectorInCluster < _sectorsPerCluster - 1) _sectorInCluster++;
                else
                {
                    // find which fat sector this cluster is in
                    var offset = (ClusterId * 4) / 512 + _fatBeginLba;
                    var fatSector = _fat32.LoadLba(offset);
                    var nextCluster = BitConverter.ToUInt32(fatSector, (int)(ClusterId % 128) * 4);
                    nextCluster &= 0x0FFFFFFF;

                    if (nextCluster >= 0x0FFFFFF8) return false; // no more clusters
                    else if (nextCluster >= 2 && nextCluster <= 0xFFFFFEF)
                    {
                        ClusterId = nextCluster;
                        _sectorInCluster = 0;
                    }
                    else throw new Exception("Unsupported cluster type");
                }

                return true;
            }

            public uint ReserveEmptyCluster()
            {
                uint empty = 0;
                for (uint i = 0; i < _sectorsPerFat && empty == 0; i++)
                {
                    var fatSector = _fat32.LoadLba(i + _fatBeginLba);
                    for (uint j = 0; j < fatSector.Length / 4; j++)
                    {
                        var nextCluster = BitConverter.ToUInt32(fatSector, (int)j * 4);
                        if (nextCluster == 0)
                        {
                            // mark this cluster as used
                            fatSector[j * 4 + 0] = 0xff;
                            fatSector[j * 4 + 1] = 0xff;
                            fatSector[j * 4 + 2] = 0xff;
                            fatSector[j * 4 + 3] = 0x0f;
                            empty = i * 512 / 4 + j;
                            break;
                        }
                    }
                    if (empty != 0) break;
                }
                return empty;
            }

            public bool AdvanceWrite()
            {
                if (AdvanceRead()) return true;

                // this means we hit the end of the cluster chain, so we need to find an empty cluster
                var empty = ReserveEmptyCluster();

                // update the fat table entry of the previous cluster to point to the new one
                uint offset = (ClusterId * 4) / 512 + _fatBeginLba;
                var fatSector = _fat32.LoadLba(offset);
                int byteOffset = (int)(ClusterId % 128) * 4;
                fatSector[byteOffset + 0] = (byte)empty;
                fatSector[byteOffset + 1] = (byte)(empty >> 8);
                fatSector[byteOffset + 2] = (byte)(empty >> 16);
                fatSector[byteOffset + 3] = (byte)(empty >> 24);

                return AdvanceRead();
            }
        }
    }
}