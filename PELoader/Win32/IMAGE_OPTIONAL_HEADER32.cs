using System;
using System.Runtime.InteropServices;

namespace PELoader
{
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct ImageOptionalHeader32
    {
        [FieldOffset(0)]
        public uint imageBase;

        [FieldOffset(4)]
        public uint sectionAlignment;

        [FieldOffset(8)]
        public uint fileAlignment;

        [FieldOffset(12)]
        public ushort majorOperationSystemVersion;

        [FieldOffset(14)]
        public ushort minorOperationSystemVersion;

        [FieldOffset(16)]
        public ushort majorImageVersion;

        [FieldOffset(18)]
        public ushort minorImageVersion;

        [FieldOffset(20)]
        public ushort majorSubsystemVersion;

        [FieldOffset(22)]
        public ushort minorSubsystemVersion;

        [FieldOffset(24)]
        public uint win32VersionValue;

        [FieldOffset(28)]
        public uint sizeOfImage;

        [FieldOffset(32)]
        public uint sizeOfHeaders;

        [FieldOffset(36)]
        public uint checkSum;

        [FieldOffset(40)]
        public ushort subsystem;

        [FieldOffset(42)]
        public ushort dllCharacteristics;
        [FieldOffset(42)]
        public DLLCharacteristic DLLCharacteristics;

        [FieldOffset(44)]
        public uint sizeOfStackReserve;

        [FieldOffset(48)]
        public uint sizeOfStackCommit;

        [FieldOffset(52)]
        public uint sizeOfHeapReserve;

        [FieldOffset(56)]
        public uint sizeOfHeapCommit;

        [FieldOffset(60)]
        public uint loaderFlags;

        [FieldOffset(64)]
        public uint numberOfRvaAndSizes;
    }

    [Flags]
    public enum DLLCharacteristic : ushort
    {
        IMAGE_DLLCHARACTERISTICS_HIGH_ENTROPY_VA = 0x0020,
        IMAGE_DLLCHARACTERISTICS_DYNAMIC_BASE = 0x0040,
        IMAGE_DLLCHARACTERISTICS_FORCE_INTEGRITY = 0x0080,
        IMAGE_DLLCHARACTERISTICS_NX_COMPAT = 0x0100,
        IMAGE_DLLCHARACTERISTICS_NO_ISOLATION = 0x0200,
        IMAGE_DLLCHARACTERISTICS_NO_SEH = 0x0400,
        IMAGE_DLLCHARACTERISTICS_NO_BIND = 0x0800,
        IMAGE_DLLCHARACTERISTICS_APPCONTAINER = 0x1000,
        IMAGE_DLLCHARACTERISTICS_WDM_DRIVER = 0x2000,
        IMAGE_DLLCHARACTERISTICS_TERMINAL_SERVER_AWARE = 0x8000
    }
}
