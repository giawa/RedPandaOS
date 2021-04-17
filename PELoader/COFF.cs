using System;
using System.Runtime.InteropServices;

namespace PELoader
{
    [StructLayout(LayoutKind.Explicit)]
    public struct COFFHeader
    {
        [FieldOffset(0)]
        public uint signature;

        [FieldOffset(4)]
        public ImageFileHeader header;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct COFFStandardFields
    {
        [FieldOffset(0)]
        public ushort magic;

        [FieldOffset(2)]
        public byte majorLinkerVersion;

        [FieldOffset(3)]
        public byte minorLinkerVersion;

        [FieldOffset(4)]
        public uint sizeOfCode;

        [FieldOffset(8)]
        public uint sizeOfInitializedData;

        [FieldOffset(12)]
        public uint sizeOfUninitializedData;

        [FieldOffset(16)]
        public uint addressOfEntryPoint;

        [FieldOffset(20)]
        public uint baseOfCode;

        [FieldOffset(24)]
        public uint baseOfData;
    }

    public enum COFFMachineType : ushort
    {
        I386 = 0x14c,
        AMD64 = 0x8664,
        ARM = 0x1c0,
        ARM64 = 0xaa64,
        ARMNT = 0x1c4,
        IA64 = 0x200
    }

    [Flags]
    public enum COFFCharacteristic : ushort
    {
        IMAGE_FILE_RELOCS_STRIPPED = 0x0001,
        IMAGE_FILE_EXECUTABLE_IMAGE = 0x0002,
        IMAGE_FILE_LINE_NUMS_STRIPPED = 0x0004,
        IMAGE_FILE_LOCAL_SYMS_STRIPPED = 0x0008,
        IMAGE_FILE_AGGRESSIVE_WS_TRIM = 0x0010,
        IMAGE_FILE_LARGE_ADDRESS_AWARE = 0x0020,
        RESERVED = 0x0040,
        IMAGE_FILE_BYTES_REVERSED_LO = 0x0080,
        IMAGE_FILE_32BIT_MACHINE = 0x0100,
        IMAGE_FILE_DEBUG_STRIPPED = 0x0200,
        IMAGE_FILE_REMOVABLE_RUN_FROM_SWAP = 0x0400,
        IMAGE_FILE_NET_RUN_FROM_SWAP = 0x0800,
        IMAGE_FILE_SYSTEM = 0x1000,
        IMAGE_FILE_DLL = 0x2000,
        IMAGE_FILE_UP_SYSTEM_ONLY = 0x4000,
        IMAGE_FILE_BYTES_REVERSED_HI = 0x8000
    }
}
