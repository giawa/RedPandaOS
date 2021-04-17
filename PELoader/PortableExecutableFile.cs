using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PELoader
{
    public class Section
    {
        public SectionHeader Header;

        public byte[] Data;

        public Section(SectionHeader header, byte[] data)
        {
            Header = header;

            if (data.Length < header.virtualSize)
            {
                Data = new byte[header.virtualSize];
                Buffer.BlockCopy(data, 0, Data, 0, data.Length);
            }
            else
            {
                Data = data;
            }
        }
    }

    public class VirtualMemory
    {
        public List<Section> Sections = new List<Section>();

        public byte GetByte(uint location)
        {
            foreach (var section in Sections)
            {
                if (section.Header.virtualAddress <= location && section.Header.virtualAddress + section.Header.virtualSize > location)
                {
                    return section.Data[location - section.Header.virtualAddress];
                }
            }

            throw new Exception("Invalid access");
        }

        public byte[] GetBytes(uint location, int length)
        {
            foreach (var section in Sections)
            {
                if (section.Header.virtualAddress <= location && section.Header.virtualAddress + section.Header.virtualSize > location)
                {
                    byte[] data = new byte[length];
                    for (int i = 0; i < length; i++)
                        data[i] = section.Data[location - section.Header.virtualAddress + i];
                    return data;
                }
            }

            throw new Exception("Invalid access");
        }
    }

    public class PortableExecutableFile
    {
        private COFFHeader _header;
        private COFFStandardFields _standardFields;
        private COFFWindowsPE32Fields _windowsFieldsPe32;
        private COFFWindowsPE32PlusFields _windowsFieldsPe32Plus;
        private SectionHeader[] _sections;
        private uint _peOffset;
        private ImageDataDirectory[] _directories;
        private StreamHeader[] _streamHeaders;
        private MetadataLayout _metadataLayout;

        private VirtualMemory _memory = new VirtualMemory();

        public VirtualMemory Memory { get { return _memory; } }

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
                    _peOffset = reader.ReadUInt32();
                    reader.BaseStream.Position = _peOffset;
                }
                else reader.BaseStream.Position = 0;

                _header = reader.ReadBytes(Marshal.SizeOf<COFFHeader>()).ToStruct<COFFHeader>();

                var optionalHeader = reader.ReadBytes(_header.sizeOfOptionalHeader);
                // TODO:  this will always read 28 bytes, but if PE32+ then it should only read 24
                // https://docs.microsoft.com/en-us/windows/win32/debug/pe-format#characteristics
                // From doc: "PE32 contains this additional field, which is absent in PE32+, following BaseOfCode."
                _standardFields = optionalHeader.ToStruct<COFFStandardFields>(0, Marshal.SizeOf<COFFStandardFields>());

                if (Type == PEType.PE32)
                {
                    _windowsFieldsPe32 = optionalHeader.ToStruct<COFFWindowsPE32Fields>(28, Marshal.SizeOf<COFFWindowsPE32Fields>());
                    _directories = new ImageDataDirectory[_windowsFieldsPe32.numberOfRvaAndSizes];
                    int offset = 28 + Marshal.SizeOf<COFFWindowsPE32Fields>();
                    for (int i = 0; i < _directories.Length; i++)
                        _directories[i] = optionalHeader.ToStruct<ImageDataDirectory>(offset + Marshal.SizeOf<ImageDataDirectory>() * i, Marshal.SizeOf<ImageDataDirectory>());
                }
                else if (Type == PEType.PE32Plus)
                {
                    _windowsFieldsPe32Plus = optionalHeader.ToStruct<COFFWindowsPE32PlusFields>(24, Marshal.SizeOf<COFFWindowsPE32PlusFields>());
                    _directories = new ImageDataDirectory[_windowsFieldsPe32Plus.numberOfRvaAndSizes];
                    int offset = 28 + Marshal.SizeOf<COFFWindowsPE32Fields>();
                    for (int i = 0; i < _directories.Length; i++)
                        _directories[i] = optionalHeader.ToStruct<ImageDataDirectory>(offset + Marshal.SizeOf<ImageDataDirectory>() * i, Marshal.SizeOf<ImageDataDirectory>());
                }

                _sections = new SectionHeader[_header.numberOfSections];
                for (int i = 0; i < _sections.Length; i++)
                    _sections[i] = reader.ReadBytes(Marshal.SizeOf<SectionHeader>()).ToStruct<SectionHeader>();

                // read all the data into the virtual memory sections so that we can access it
                foreach (var section in _sections)
                {
                    reader.BaseStream.Seek(section.pointerToRawData, SeekOrigin.Begin);
                    _memory.Sections.Add(new Section(section, reader.ReadBytes((int)section.sizeOfRawData)));
                }

                // the first interesting table is the import table, which is _directories[1]
                var importTable = _memory.GetBytes(_directories[1].virtualAddress, Marshal.SizeOf<ImportTable>()).ToStruct<ImportTable>();
                
                // verify we got a dll entry pointt
                /*StringBuilder dllName = new StringBuilder();
                for (int i = 0; i < 256; i++)
                {
                    byte temp = _memory.GetByte((uint)(importTable.name + i));
                    if (temp == 0) break;
                    dllName.Append((char)temp);
                }*/

                // read in the CLI header, _directories[14]
                var cliHeader = _memory.GetBytes(_directories[14].virtualAddress, Marshal.SizeOf<CLIHeader>()).ToStruct<CLIHeader>();

                // read in the metadata root
                uint metadataRva = (uint)(cliHeader.metadata & 0xffffffff);
                int metadataLength = (int)(cliHeader.metadata >> 32);
                byte[] metadata = _memory.GetBytes(metadataRva, metadataLength);
                var metadataRoot = metadata.ToStruct<MetadataRoot>(0, Marshal.SizeOf<MetadataRoot>());

                // verify the version looks appropriate
                /*StringBuilder version = new StringBuilder();
                for (int i = 0; i < metadataRoot.versionLength; i++)
                {
                    if (metadata[16 + i] == 0) break;
                    version.Append((char)metadata[16 + i]);
                }*/

                ushort metadataStreams = BitConverter.ToUInt16(metadata, 18 + (int)metadataRoot.versionLength);
                uint metadataOffset = 20 + metadataRoot.versionLength;
                _streamHeaders = new StreamHeader[metadataStreams];

                for (int i = 0; i < _streamHeaders.Length; i++)
                {
                    _streamHeaders[i] = new StreamHeader(metadata, ref metadataOffset);
                    _streamHeaders[i].ReadHeap(_memory, metadataRva);

                    if (_streamHeaders[i].Name == "#~")
                    {
                        _metadataLayout = _streamHeaders[i].Heap.ToStruct<MetadataLayout>(0, Marshal.SizeOf<MetadataLayout>());
                    }
                }

                /*uint rvaOffset = 0x2050;
                var tempMethod = new MethodHeader(_memory, ref rvaOffset);*/

                //Console.WriteLine(_header.MachineType);
            }
        }
    }

    public class MethodHeader
    {
        public uint CodeSize;
        public byte[] Code;

        public ushort Flags;
        public ushort MaxStack;
        public uint LocalVarSigTok;

        public MethodHeader(VirtualMemory memory, ref uint offset)
        {
            byte type = memory.GetByte(offset);

            if ((type & 0x03) == 0x02)
            {
                // tiny method header
                CodeSize = (uint)(type >> 2);
                offset++;
            }
            else
            {
                // fat method header
                var fatHeader = memory.GetBytes(offset, 12);
                offset += 12;

                Flags = (ushort)(BitConverter.ToUInt16(fatHeader, 0) & 0x0fff);
                MaxStack = BitConverter.ToUInt16(fatHeader, 2);
                CodeSize = BitConverter.ToUInt32(fatHeader, 4);
                LocalVarSigTok = BitConverter.ToUInt32(fatHeader, 8);

                if ((type & 0x08) != 0)
                {
                    throw new Exception("More sections follows after this header (II.25.4.5)");
                }
            }

            Code = memory.GetBytes(offset, (int)CodeSize);
            offset += CodeSize;
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

    public class StreamHeader
    {
        public uint Offset;
        public uint Size;
        public string Name;

        public byte[] Heap;

        public StreamHeader(byte[] data, ref uint offset)
        {
            Offset = BitConverter.ToUInt32(data, (int)offset);
            Size = BitConverter.ToUInt32(data, (int)offset + 4);

            offset += 8;

            StringBuilder name = new StringBuilder();
            while (offset < data.Length && data[offset] != 0)
                name.Append((char)data[offset++]);
            Name = name.ToString();

            // move to the next 4-byte boundary
            offset += (4 - (offset % 4));
        }

        public void ReadHeap(VirtualMemory memory, uint metadataOffset)
        {
            Heap = memory.GetBytes(metadataOffset + Offset, (int)Size);

            if (Name == "#Strings")
            {
                for (int i = 0; i < Heap.Length; i++)
                {
                    if (Heap[i] != 0) Console.Write((char)Heap[i]);
                    else Console.WriteLine();
                }
            }
        }

        public override string ToString()
        {
            return $"{Name} heap contains {Size} bytes.";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MetadataLayout
    {
        public uint reserved;
        public byte majorVersion;
        public byte minorVersion;
        public byte heapSizes;
        public byte reserved2;
        public ulong valid;
        public ulong sorted;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MetadataRoot
    {
        public uint signature;
        public ushort majorVersion;
        public ushort minorVersion;
        public uint reserved;
        public uint versionLength;
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct ImportTable
    {
        [FieldOffset(0)]
        public uint importLookupTable;

        [FieldOffset(4)]
        public uint dateTimeStamp;

        [FieldOffset(8)]
        public uint forwarderChain;

        [FieldOffset(12)]
        public uint name;

        [FieldOffset(16)]
        public uint importAddressTable;

        [FieldOffset(20)]
        public uint zero;

        [FieldOffset(24)]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] zeros;
    }

    public struct CLIHeader
    {
        public uint cb;
        public ushort majorRuntimeVersion;
        public ushort minorRuntimeVersion;
        public ulong metadata;
        public uint flags;
        public uint entryPointToken;
        public ulong resources;
        public ulong strongNameSignature;
        public ulong codeManagerTable;
        public ulong vTableFixups;
        public ulong exportAddressTableJumps;
        public ulong managedNativeHeader;
    }

    [Flags]
    public enum CLIRuntimeFlags : uint
    {
        COMIMAGE_FLAGS_ILONLY = 0x00000001,
        COMIMAGE_FLAGS_32BITREQUIRED = 0x00000002,
        COMIMAGE_FLAGS_STRONGNAMESIGNED = 0x00000008,
        COMIMAGE_FLAGS_NATIVE_ENTRYPOINT = 0x00000010,
        COMIMAGE_FLAGS_TRACKDEBUGDATA = 0x00010000
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ImageDataDirectory
    {
        public uint virtualAddress;
        public uint size;
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

    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct COFFWindowsPE32Fields
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
        public uint numberOfRvaAndSizes;
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
