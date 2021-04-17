using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PELoader
{
    public class PortableExecutableFile
    {
        private COFFHeader _header;
        private COFFStandardFields _standardFields;
        private COFFWindowsPE32Fields _windowsFieldsPe32;
        private COFFWindowsPE32PlusFields _windowsFieldsPe32Plus;
        private SectionHeader[] _sections;

        public PEType Type
        {
            get
            {
                switch (_standardFields.magic)
                {
                    case 0x10B: return PEType.PE32;
                    case 0x20B: return PEType.PE32Plus;
                    case 0x107: return PEType.ROMImage;
                }
                return PEType.Invalid;
            }
        }

        public PortableExecutableFile(string path)
        {
            // try to process the output of 'TestIL'
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                var dosSignature = reader.ReadUInt16();

                if (dosSignature == 0x5A4D)
                {
                    // this file contains a DOS header, so offset to the start of the PE header
                    reader.BaseStream.Position = 0x3C;
                    var peHeader = reader.ReadUInt32();
                    reader.BaseStream.Position = peHeader;
                }
                else reader.BaseStream.Position = 0;

                _header = reader.ReadBytes(Marshal.SizeOf<COFFHeader>()).ToStruct<COFFHeader>();

                var optionalHeader = reader.ReadBytes(_header.sizeOfOptionalHeader);
                // TODO:  this will always read 28 bytes, but if PE32+ then it should only read 24
                // https://docs.microsoft.com/en-us/windows/win32/debug/pe-format#characteristics
                // From doc: "PE32 contains this additional field, which is absent in PE32+, following BaseOfCode."
                _standardFields = optionalHeader.ToStruct<COFFStandardFields>(0, Marshal.SizeOf<COFFStandardFields>());

                if (Type == PEType.PE32) _windowsFieldsPe32 = optionalHeader.ToStruct<COFFWindowsPE32Fields>(28, Marshal.SizeOf<COFFWindowsPE32Fields>());
                else if (Type == PEType.PE32Plus) _windowsFieldsPe32Plus = optionalHeader.ToStruct<COFFWindowsPE32PlusFields>(24, Marshal.SizeOf<COFFWindowsPE32PlusFields>());

                _sections = new SectionHeader[_header.numberOfSections];
                for (int i = 0; i < _sections.Length; i++)
                    _sections[i] = reader.ReadBytes(Marshal.SizeOf<SectionHeader>()).ToStruct<SectionHeader>();

                Console.WriteLine(_header.MachineType);
            }
        }
    }

    public enum PEType
    {
        Invalid,
        PE32,
        PE32Plus,
        ROMImage
    }

    public static class Utilities
    {
        public static T ToStruct<T>(this byte[] bytes) where T : struct
        {
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);

            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }

        public static T ToStruct<T>(this byte[] bytes, int offset, int size) where T : struct
        {
            return MemoryMarshal.Cast<byte, T>(bytes.AsSpan(offset, size))[0];
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct COFFHeader
    {
        [FieldOffset(0)]
        public uint signature;

        [FieldOffset(4)]
        public ushort machine;
        [FieldOffset(4)]
        public COFFMachineType MachineType;

        [FieldOffset(6)]
        public ushort numberOfSections;

        [FieldOffset(8)]
        public uint timeDateStamp;

        [FieldOffset(12)]
        public uint pointerToSymbolTable;

        [FieldOffset(16)]
        public uint numberOfSumbolTable;

        [FieldOffset(20)]
        public ushort sizeOfOptionalHeader;

        [FieldOffset(22)]
        public ushort characteristics;
        [FieldOffset(22)]
        public COFFCharacteristic Characteristics;
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

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct COFFWindowsPE32Fields
    {
        public uint imageBase;
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
        public uint sizeOfStackReserve;
        public uint sizeOfStackCommit;
        public uint sizeOfHeapReserve;
        public uint sizeOfHeapCommit;
        public uint loaderFlags;
        public uint numerOfRvaAndSizes;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct COFFWindowsPE32PlusFields
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
        public uint numerOfRvaAndSizes;
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

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 40)]
    public struct SectionHeader
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
