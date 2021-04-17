using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace PELoader
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ImageOptionalHeader64
    {
        public ulong imageBase;
        public uint sectionAlignment;
        public uint fileAlignment;
        public ushort majorOperationSystemVersion;
        public ushort minorOperationSystemVersion;
        public ushort majorImageVersion;
        public ushort minorImageVersion;
        public ushort majorSubsystemVersion;
        public ushort minorSubsystemVersion;
        public uint win32VersionValue;
        public uint sizeOfImage;
        public uint sizeOfHeaders;
        public uint checkSum;
        public ushort subsystem;
        public ushort dllCharacteristics;
        public ulong sizeOfStackReserve;
        public ulong sizeOfStackCommit;
        public ulong sizeOfHeapReserve;
        public ulong sizeOfHeapCommit;
        public uint loaderFlags;
        public uint numberOfRvaAndSizes;
    }
}
