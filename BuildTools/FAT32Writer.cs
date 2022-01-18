using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BuildTools
{
    public class FAT32Writer
    {
        private List<byte[]> _sectors = new List<byte[]>();

        private SectorHandler _handler;

        private byte _sectorsPerCluster;
        private ushort _reservedSectors;
        private uint _sectorsPerFat, _rootDirectory;

        public List<byte[]> Sectors { get { return _sectors; } }

        public FAT32Writer(byte sectorsPerCluster, ushort reservedSectors, uint sectorsPerFat, uint rootDirectory, uint diskSizeInSectors)
        {
            _sectorsPerCluster = sectorsPerCluster;
            _reservedSectors = reservedSectors;
            _sectorsPerFat = sectorsPerFat;
            _rootDirectory = rootDirectory;

            // assume always 2 FAT tables
            _handler = new SectorHandler(_sectors, reservedSectors, (int)(reservedSectors + 2 * sectorsPerFat), sectorsPerFat, sectorsPerCluster);

            // initialize the disk
            for (uint i = 0; i < diskSizeInSectors; i++) _sectors.Add(new byte[512]);

            // initialize boot sector
            _sectors[0][0] = 0xE9;  // jmp
            _sectors[0][1] = 0x5D;  // to 0x7C60 
            _sectors[0][2] = 0x00;  // (FAT32 boot code)
            //_sectors[0][0] = 0xE9;  // jmp
            //_sectors[0][1] = 0xFD;
            //_sectors[0][2] = 0x01;  // to 0x9200
            Encoding.ASCII.GetBytes("RedPanda").CopyTo(_sectors[0], 3);
            _sectors[0][510] = 0x55;
            _sectors[0][511] = 0xAA;

            // initialize the first part of the fat32 bios parameter block (DOS 2.0 BPB)
            _sectors[0][0x0C] = 0x02;   // 512 byte sectors
            _sectors[0][0x0D] = sectorsPerCluster;
            _sectors[0][0x0E] = (byte)reservedSectors;
            _sectors[0][0x0F] = (byte)(reservedSectors >> 8);
            _sectors[0][0x10] = 2;      // number of fat tables
            _sectors[0][0x15] = 0xF8;   // "fixed disk" media descriptor

            // initialize the next part of the fat32 bios parameter block (DOS 3.31 BPB)
            // skip CHS geometry, as we retrieve this from the BIOS and we also use LBA addressing
            _sectors[0][0x20] = (byte)diskSizeInSectors;
            _sectors[0][0x21] = (byte)(diskSizeInSectors >> 8);
            _sectors[0][0x22] = (byte)(diskSizeInSectors >> 16);
            _sectors[0][0x23] = (byte)(diskSizeInSectors >> 24);

            // initialize the rest of the fat32 bios parameter block, which is actually the fat32 extended BPB
            _sectors[0][0x24] = (byte)sectorsPerFat;
            _sectors[0][0x25] = (byte)(sectorsPerFat >> 8);
            _sectors[0][0x26] = (byte)(sectorsPerFat >> 16);
            _sectors[0][0x27] = (byte)(sectorsPerFat >> 24);
            _sectors[0][0x2C] = (byte)rootDirectory;
            _sectors[0][0x2D] = (byte)(rootDirectory >> 8);
            _sectors[0][0x2E] = (byte)(rootDirectory >> 16);
            _sectors[0][0x2F] = (byte)(rootDirectory >> 24);
            _sectors[0][0x30] = 1;      // logical sector of FS Information Sector
            _sectors[0][0x32] = 6;      // sector of first of three FAT32 boot sectors
            _sectors[0][0x40] = 0x80;   // drive number, which in qemu is 0x80 for a typical physical hard drive
            _sectors[0][0x42] = 0x29;   // always 0x29
            // use mmdd-hhmm for the serial number
            _sectors[0][0x43] = (byte)DateTime.UtcNow.Month;
            _sectors[0][0x44] = (byte)DateTime.UtcNow.Day;
            _sectors[0][0x45] = (byte)DateTime.UtcNow.Hour;
            _sectors[0][0x46] = (byte)DateTime.UtcNow.Minute;
            var volumeName = Encoding.ASCII.GetBytes("RedPandaVol");
            Array.Copy(volumeName, 0, _sectors[0], 0x47, volumeName.Length);
            var fatSignature = Encoding.ASCII.GetBytes("FAT32   ");
            Array.Copy(fatSignature, 0, _sectors[0], 0x52, fatSignature.Length);

            // initialize the fat table
            var firstFat = _sectors[reservedSectors];
            WriteUInt(firstFat, 0x0ffffff8U, 0);
            WriteUInt(firstFat, 0xffffffffU, 4);
        }

        public void StoreKernel(string filename)
        {
            // TODO:  This is just a hack to get a basic file system working
            // The directory/file classes should provide helpers to insert directory/etc

            // grab the root directory and reserve it
            var shouldBeRoot = _handler.ReserveEmptyCluster();
            var rootSector = _sectors[(int)(_reservedSectors + 2 * _sectorsPerFat + (shouldBeRoot - 2) * _sectorsPerCluster)];
            // then populate the volume label
            Encoding.ASCII.GetBytes("PANDAVOLUME").CopyTo(rootSector, 0);
            rootSector[0x0B] = 0x08;    // volume label

            // create a "boot" directory in the root directory
            var bootCluster = _handler.ReserveEmptyCluster();
            FATDirectory bootDirectory = new FATDirectory("BOOT", bootCluster);
            bootDirectory.DirEntry[0x0C] = 0x08;    // mark as lowercase
            bootDirectory.DirEntry.CopyTo(rootSector, 32);

            // find kernel.bin
            var kernelBytes = File.ReadAllBytes(filename);
            FATFile kernelFile = new FATFile("KERNEL.BIN", _handler, kernelBytes);
            kernelFile.DirEntry[0x0B] = 0x05;       // system and read only flags
            kernelFile.DirEntry[0x0C] = 0x18;    // mark as lowercase
            var bootSector = _sectors[(int)(_reservedSectors + 2 * _sectorsPerFat + (bootCluster - 2) * _sectorsPerCluster)];
            kernelFile.DirEntry.CopyTo(bootSector, 0);
        }

        private void WriteUInt(byte[] sector, uint value, int offset)
        {
            sector[offset + 0] = (byte)value;
            sector[offset + 1] = (byte)(value >> 8);
            sector[offset + 2] = (byte)(value >> 16);
            sector[offset + 3] = (byte)(value >> 24);
        }

        public void Flush()
        {
            // make sure second fat table mirrors the first
            for (int i = 0; i < _sectorsPerFat; i++)
            {
                Array.Copy(_sectors[_reservedSectors], _sectors[(int)(_reservedSectors + _sectorsPerFat)], _sectors[0].Length);
            }
        }

        /*public void Write(string filename)
        {
            using (BinaryWriter output = new BinaryWriter(File.Open(filename, FileMode.OpenOrCreate)))
            {
                // make sure second fat table mirrors the first
                for (int i = 0; i < _sectorsPerFat; i++)
                {
                    Array.Copy(_sectors[_reservedSectors], _sectors[(int)(_reservedSectors + _sectorsPerFat)], _sectors[0].Length);
                }

                foreach (var sector in _sectors) output.Write(sector);
            }
        }*/
    }

    public class FATDirectory
    {
        public List<FATDirectory> Directories = new List<FATDirectory>();
        public List<FATFile> Files = new List<FATFile>();

        public byte[] DirEntry;

        public uint Cluster
        {
            get
            {
                var high = BitConverter.ToUInt16(DirEntry, 0x14);
                var low = BitConverter.ToUInt16(DirEntry, 0x1A);

                return (uint)high << 16 | low;
            }
        }

        public FATDirectory(string name, uint cluster)
        {
            DirEntry = new byte[32];

            if (name.Contains(".") || name.Length > 11) throw new Exception("Invalid directory name");

            name = name.PadRight(11, ' ');

            Encoding.ASCII.GetBytes(name).CopyTo(DirEntry, 0);

            DirEntry[0x0B] = 0x10;

            ushort high = (ushort)(cluster >> 16);
            ushort low = (ushort)(cluster);
            BitConverter.GetBytes(high).CopyTo(DirEntry, 0x14);
            BitConverter.GetBytes(low).CopyTo(DirEntry, 0x1A);
        }

        public FATDirectory(byte[] dirEntry)
        {
            DirEntry = dirEntry;
        }

        public void Read(SectorHandler reader)
        {
            reader.SetCluster(Cluster);

            do
            {
                var dirdata = reader.CurrentSector;

                for (int i = 0; i < 16; i++)
                {
                    if (dirdata[i * 32] == 0) continue;
                    byte[] dirEntry = new byte[32];
                    Array.Copy(dirdata, i * 32, dirEntry, 0, 32);


                    if ((dirEntry[0x0B] & 0x10) == 0x10)
                    {
                        Directories.Add(new FATDirectory(dirEntry));
                    }
                    else if ((dirEntry[0x0B] & 0x0F) != 0x0F)   // extended name
                    {
                        Files.Add(new FATFile()
                        {
                            DirEntry = dirEntry
                        });
                    }
                }
            } while (reader.AdvanceRead());
        }

        public override string ToString()
        {
            return Encoding.ASCII.GetString(DirEntry, 0, 11);
        }
    }

    public class FATFile
    {
        public byte[] DirEntry;

        public uint Size
        {
            get { return BitConverter.ToUInt32(DirEntry, 0x1C); }
        }

        public uint Cluster
        {
            get
            {
                var high = BitConverter.ToUInt16(DirEntry, 0x14);
                var low = BitConverter.ToUInt16(DirEntry, 0x1A);

                return (uint)high << 16 | low;
            }
        }

        public FATFile()
        {

        }

        public FATFile(string name, SectorHandler writer, byte[] data)
        {
            DirEntry = new byte[32];

            string[] split = name.Split('.');
            if (split.Length > 2) throw new Exception("Invalid filename");
            if (split[0].Length > 8) throw new Exception("Invalid filename");
            if (split.Length > 1 && split[1].Length > 3) throw new Exception("Invalid filename");

            split[0] = split[0].PadRight(8, ' ');
            split[1] = split[1].PadRight(3, ' ');

            Encoding.ASCII.GetBytes(split[0]).CopyTo(DirEntry, 0);
            if (split.Length > 1) Encoding.ASCII.GetBytes(split[1]).CopyTo(DirEntry, 8);

            var firstCluster = writer.ReserveEmptyCluster();

            ushort high = (ushort)(firstCluster >> 16);
            ushort low = (ushort)(firstCluster);
            BitConverter.GetBytes(high).CopyTo(DirEntry, 0x14);
            BitConverter.GetBytes(low).CopyTo(DirEntry, 0x1A);

            int toWrite = data.Length;
            int index = 0;
            writer.SetCluster(firstCluster);

            while (toWrite > 0)
            {
                int amount = Math.Min(512, toWrite);
                Array.Copy(data, index, writer.CurrentSector, 0, amount);
                index += amount;
                toWrite -= amount;

                if (toWrite > 0)
                {
                    if (!writer.AdvanceWrite()) throw new Exception("Ran out of space");
                }
            }

            BitConverter.GetBytes(data.Length).CopyTo(DirEntry, 0x1C);
        }

        public byte[] Read(SectorHandler reader)
        {
            byte[] data = new byte[(int)Size];

            uint copied = 0;

            reader.SetCluster(Cluster);

            while (copied < Size)
            {
                var toCopy = Size - copied;
                if (toCopy > 512) toCopy = 512;

                Array.Copy(reader.CurrentSector, 0, data, copied, toCopy);
                copied += toCopy;

                if (copied < Size)
                {
                    if (!reader.AdvanceRead()) throw new Exception("Unexpected end of file");
                }
            }

            return data;
        }

        public override string ToString()
        {
            return Encoding.ASCII.GetString(DirEntry, 0, 11);
        }
    }

    public class SectorHandler
    {
        public byte[] CurrentSector
        {
            set
            {
                _sectors[_clusterBeginLba + (int)(ClusterId - 2) * _sectorsPerCluster + _sectorInCluster] = value;
            }
            get
            {
                return _sectors[_clusterBeginLba + (int)(ClusterId - 2) * _sectorsPerCluster + _sectorInCluster];
            }
        }

        public uint ClusterId { get; private set; }

        private byte _sectorInCluster;

        private List<byte[]> _sectors;
        private int _fatBeginLba, _clusterBeginLba;
        private uint _sectorsPerFat;
        private byte _sectorsPerCluster;

        public SectorHandler(List<byte[]> sectors, int fatBeginLba, int clusterBeginLba, uint sectorsPerFat, byte sectorsPerCluster)
        {
            _sectors = sectors;
            _fatBeginLba = fatBeginLba;
            _clusterBeginLba = clusterBeginLba;
            _sectorsPerFat = sectorsPerFat;
            _sectorsPerCluster = sectorsPerCluster;
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
                int offset = (int)(ClusterId / 512 * 4) + _fatBeginLba;
                var fatSector = _sectors[offset];
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
                var fatSector = _sectors[(int)i + _fatBeginLba];
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
            int offset = (int)(ClusterId / 512 * 4) + _fatBeginLba;
            var fatSector = _sectors[offset];
            int byteOffset = (int)(ClusterId % 128) * 4;
            fatSector[byteOffset + 0] = (byte)empty;
            fatSector[byteOffset + 1] = (byte)(empty >> 8);
            fatSector[byteOffset + 2] = (byte)(empty >> 16);
            fatSector[byteOffset + 3] = (byte)(empty >> 24);

            return AdvanceRead();
        }
    }
}
