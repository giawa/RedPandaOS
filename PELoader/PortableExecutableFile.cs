using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PELoader
{
    public class PortableExecutableFile
    {
        public string Name { get; private set; }
        public string Filename { get; private set; }

        private COFFHeader _header;
        private COFFStandardFields _standardFields;
        private ImageOptionalHeader32 _windowsFieldsPe32;
        private ImageOptionalHeader64 _windowsFieldsPe32Plus;
        private ImageSectionHeader[] _sections;
        private uint _peOffset;
        private ImageDataDirectory[] _directories;

        public CLIMetadata Metadata { get; private set; }

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
            var fileInfo = new FileInfo(path);
            Name = fileInfo.Name;
            if (Name.EndsWith(".dll") || Name.EndsWith(".exe")) Name = Name.Substring(0, Name.Length - 4);
            Filename = fileInfo.FullName.Replace("\\", "/");

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

                var optionalHeader = reader.ReadBytes(_header.header.sizeOfOptionalHeader);
                // https://docs.microsoft.com/en-us/windows/win32/debug/pe-format#characteristics
                _standardFields = optionalHeader.ToStruct<COFFStandardFields>(0, Marshal.SizeOf<COFFStandardFields>());

                if (Type == PEType.PE32)
                {
                    _windowsFieldsPe32 = optionalHeader.ToStruct<ImageOptionalHeader32>(28, Marshal.SizeOf<ImageOptionalHeader32>());
                    _directories = new ImageDataDirectory[_windowsFieldsPe32.numberOfRvaAndSizes];
                    int offset = 28 + Marshal.SizeOf<ImageOptionalHeader32>();
                    for (int i = 0; i < _directories.Length; i++)
                        _directories[i] = optionalHeader.ToStruct<ImageDataDirectory>(offset + Marshal.SizeOf<ImageDataDirectory>() * i, Marshal.SizeOf<ImageDataDirectory>());
                }
                else if (Type == PEType.PE32Plus)
                {
                    _windowsFieldsPe32Plus = optionalHeader.ToStruct<ImageOptionalHeader64>(24, Marshal.SizeOf<ImageOptionalHeader64>());
                    _directories = new ImageDataDirectory[_windowsFieldsPe32Plus.numberOfRvaAndSizes];
                    int offset = 24 + Marshal.SizeOf<ImageOptionalHeader32>();
                    for (int i = 0; i < _directories.Length; i++)
                        _directories[i] = optionalHeader.ToStruct<ImageDataDirectory>(offset + Marshal.SizeOf<ImageDataDirectory>() * i, Marshal.SizeOf<ImageDataDirectory>());
                }

                _sections = new ImageSectionHeader[_header.header.numberOfSections];
                for (int i = 0; i < _sections.Length; i++)
                    _sections[i] = reader.ReadBytes(Marshal.SizeOf<ImageSectionHeader>()).ToStruct<ImageSectionHeader>();

                // read all the data into the virtual memory sections so that we can access it
                foreach (var section in _sections)
                {
                    reader.BaseStream.Seek(section.pointerToRawData, SeekOrigin.Begin);
                    _memory.Sections.Add(new VirtualMemory.Section(section, reader.ReadBytes((int)section.sizeOfRawData)));
                }

                // the first interesting table is the import table, which is _directories[1]
                //var importTable = _memory.GetBytes(_directories[1].virtualAddress, Marshal.SizeOf<ImportTable>()).ToStruct<ImportTable>();

                Metadata = new CLIMetadata(this, _directories[14]);

                // verify we got a dll entry pointt
                /*StringBuilder dllName = new StringBuilder();
                for (int i = 0; i < 256; i++)
                {
                    byte temp = _memory.GetByte((uint)(importTable.name + i));
                    if (temp == 0) break;
                    dllName.Append((char)temp);
                }*/

                //Console.WriteLine(_header.MachineType);
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

    /*[StructLayout(LayoutKind.Explicit, Size = 40)]
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
    }*/
}
