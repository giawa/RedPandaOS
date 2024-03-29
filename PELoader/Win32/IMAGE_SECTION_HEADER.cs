﻿using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PELoader
{
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 40)]
    public struct ImageSectionHeader
    {
        [FieldOffset(0)]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] name;

        [FieldOffset(8)]
        public uint virtualSize;

        [FieldOffset(12)]
        public uint virtualAddress;

        [FieldOffset(16)]
        public uint sizeOfRawData;

        [FieldOffset(20)]
        public uint pointerToRawData;

        [FieldOffset(24)]
        public uint pointerToRelocations;

        [FieldOffset(28)]
        public uint pointerToLinenumbers;

        [FieldOffset(32)]
        public ushort numberOfRelocations;

        [FieldOffset(34)]
        public ushort numberOfLinenumbers;

        [FieldOffset(36)]
        public uint characteristics;
        [FieldOffset(36)]
        public SectionCharacteristic Characteristics;

        public string Name
        {
            get
            {
                StringBuilder temp = new StringBuilder();
                for (int i = 0; i < name.Length && name[i] != 0; i++) temp.Append((char)name[i]);
                return temp.ToString();
            }
        }
    }

    [Flags]
    public enum SectionCharacteristic : uint
    {
        IMAGE_SCN_TYPE_NO_PAD = 0x00000008,
        IMAGE_SCN_CNT_CODE = 0x00000020,
        IMAGE_SCN_CNT_INITIALIZED_DATA = 0x00000040,
        IMAGE_SCN_CNT_UNINITIALIZED_DATA = 0x00000080,
        IMAGE_SCN_LNK_OTHER = 0x00000100,
        IMAGE_SCN_LNK_INFO = 0x00000200,
        IMAGE_SCN_LNK_REMOVE = 0x00000800,
        IMAGE_SCN_LNK_COMDAT = 0x00001000,
        IMAGE_SCN_GPREL = 0x00008000,
        IMAGE_SCN_ALIGN_1BYTES = 0x00100000,
        IMAGE_SCN_ALIGN_2BYTES = 0x00200000,
        IMAGE_SCN_ALIGN_4BYTES = 0x00300000,
        IMAGE_SCN_ALIGN_8BYTES = 0x0400000,
        IMAGE_SCN_ALIGN_16BYTES = 0x00500000,
        IMAGE_SCN_ALIGN_32BYTES = 0x00600000,
        IMAGE_SCN_ALIGN_64BYTES = 0x00700000,
        IMAGE_SCN_ALIGN_128BYTES = 0x00800000,
        IMAGE_SCN_ALIGN_256BYTES = 0x00900000,
        IMAGE_SCN_ALIGN_512BYTES = 0x00A00000,
        IMAGE_SCN_ALIGN_1024BYTES = 0x00B00000,
        IMAGE_SCN_ALIGN_2048BYTES = 0x00C00000,
        IMAGE_SCN_ALIGN_4096BYTES = 0x00D00000,
        IMAGE_SCN_ALIGN_8192BYTES = 0x00E00000,
        IMAGE_SCN_LNK_NRELOC_OVFL = 0x01000000,
        IMAGE_SCN_MEM_DISCARDABLE = 0x02000000,
        IMAGE_SCN_MEM_NOT_CACHED = 0x04000000,
        IMAGE_SCN_MEM_NOT_PAGED = 0x08000000,
        IMAGE_SCN_MEM_SHARED = 0x10000000,
        IMAGE_SCN_MEM_EXECUTE = 0x20000000,
        IMAGE_SCN_MEM_READ = 0x40000000,
        IMAGE_SCN_MEM_WRITE = 0x80000000
    }
}
