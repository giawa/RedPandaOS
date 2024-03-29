﻿#define MBR

using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;

namespace BuildTools
{
    public static class DiskMaker
    {
        public class PartitionInfo
        {
            public bool Bootable;

            public byte[] FirstSectorCHS = new byte[3];

            public byte PartitionType;

            public byte[] LastSectorCHS = new byte[3];

            public uint FirstSectorLBA;

            public uint NumberOfSectors;

            public byte[] GetBytes()
            {
                byte[] bytes = new byte[16];

                if (Bootable) bytes[0] = 0x80;
                bytes[4] = PartitionType;
                BitConverter.GetBytes(FirstSectorLBA).CopyTo(bytes, 8);
                BitConverter.GetBytes(NumberOfSectors).CopyTo(bytes, 12);

                return bytes;
            }
        }

        public class DiskInfo
        {
            public List<PartitionInfo> Partitions = new List<PartitionInfo>();
        }

        public static void MakeBootableDisk(DiskInfo info, string filename, string stage1, string stage2)
        {
            // do a couple of quick checks
            if (info.Partitions.Count == 0 || info.Partitions.Count > 4) throw new Exception("Invalid partitions");

            // find the bootable partition
            var bootablePartition = info.Partitions.Where(p => p.Bootable).SingleOrDefault();
            if (bootablePartition == null || bootablePartition.PartitionType != 0x0B) throw new Exception("Not bootable FAT32 partition was available");

            // set up the first stage bootloader
            var stage1Bytes = File.ReadAllBytes(stage1);
            if (stage1Bytes.Length > 440) throw new Exception("Stage 1 bootloader is too large");

            Console.WriteLine($"Stage 1 is using {stage1Bytes.Length / 440.0 * 100}% of available space.");

            byte[] bootSector = new byte[512];
            stage1Bytes.CopyTo(bootSector, 0);
            Encoding.ASCII.GetBytes("RPOS").CopyTo(bootSector, 440);

            uint lastDiskSector = 0;

            for (int i = 0; i < info.Partitions.Count; i++)
            {
                info.Partitions[i].GetBytes().CopyTo(bootSector, 446 + i * 16);

                lastDiskSector = Math.Max(lastDiskSector, info.Partitions[i].FirstSectorLBA + info.Partitions[i].NumberOfSectors - 1);
            }

            bootSector[510] = 0x55;
            bootSector[511] = 0xAA;

            byte sectorsPerCluster = 2;
            ushort reservedSectors = 8;
            FAT32Writer filesystem = new FAT32Writer(sectorsPerCluster, reservedSectors, bootablePartition.NumberOfSectors / sectorsPerCluster / 128, 2, bootablePartition.NumberOfSectors);
            //filesystem.StoreKernel(kernel);
            filesystem.Build();
            filesystem.Flush();

            // store the stage2 bootloader on to the reserved sectors of the fat32 file system
            var stage2Bytes = File.ReadAllBytes(stage2);
            if (stage2Bytes.Length > 512 * 7) throw new Exception("Stage 2 bootloader is too large");

            Console.WriteLine($"Stage 2 is using {stage2Bytes.Length / (512.0 * 7) * 100}% of available space.");
            //var pmBytes = File.ReadAllBytes(kernel).Length;
            //Console.WriteLine($"Kernel is using {pmBytes / 512 + 1} sectors ({(pmBytes / 352256.0) * 100}%).");

            int toCopy = stage2Bytes.Length;
            int offset = 0;

            while (toCopy > 0)
            {
                int copy = Math.Min(512, toCopy);
                Array.Copy(stage2Bytes, offset, filesystem.Sectors[1 + offset / 512], 0, copy);
                toCopy -= copy;
                offset += copy;
            }

            File.Delete(filename);

            using (BinaryWriter writer = new BinaryWriter(File.Open(filename, FileMode.OpenOrCreate)))
            {
                List<byte[]> sectors = new List<byte[]>();

#if MBR
                sectors.Add(bootSector);
#endif

                while (lastDiskSector-- > 0) sectors.Add(new byte[512]);

                // put the fat32 file system on to the bootable partition
                for (int i = 0; i < filesystem.Sectors.Count; i++)
                {
#if MBR
                    sectors[(int)bootablePartition.FirstSectorLBA + i] = filesystem.Sectors[i];
#else
                    sectors[i] = filesystem.Sectors[i];
#endif
                }

#if !MBR
                File.ReadAllBytes("fatstage1.bin").CopyTo(sectors[0], 0x60);
                // drop a partition entry at offset 0x1E0
                var partition = new byte[] { 0x80, 0x00, 0x00, 0x00, 0x0B, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x3E, 0x0B, 0x00, 0x00 };
                partition.CopyTo(sectors[0], 0x1E0);
#endif

                foreach (var sector in sectors) writer.Write(sector);
            }
        }
    }
}
