using System.Runtime.InteropServices;

namespace PELoader
{
    [StructLayout(LayoutKind.Explicit)]
    public struct ImageFileHeader
    {
        [FieldOffset(0)]
        public ushort machine;
        [FieldOffset(0)]
        public COFFMachineType MachineType;

        [FieldOffset(2)]
        public ushort numberOfSections;

        [FieldOffset(4)]
        public uint timeDateStamp;

        [FieldOffset(8)]
        public uint pointerToSymbolTable;

        [FieldOffset(12)]
        public uint numberOfSumbolTable;

        [FieldOffset(16)]
        public ushort sizeOfOptionalHeader;

        [FieldOffset(18)]
        public ushort characteristics;
        [FieldOffset(18)]
        public COFFCharacteristic Characteristics;
    }
}
