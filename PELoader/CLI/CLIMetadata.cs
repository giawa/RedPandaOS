using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace PELoader
{
    public class CLIMetadata
    {
        private StreamHeader[] _streamHeaders;
        private MetadataLayout _metadataLayout;

        public StreamHeader Strings { get; private set; }
        public StreamHeader US { get; private set; }
        public StreamHeader Blob { get; private set; }
        public StreamHeader Table { get; private set; }

        public bool WideStrings { get; private set; }
        public bool WideGuid { get; private set; }
        public bool WideBlob { get; private set; }

        public string GetString(uint addr)
        {
            StringBuilder sb = new StringBuilder();

            while (addr < Strings.Heap.Length)
            {
                if (Strings.Heap[addr] == 0) break;
                else sb.Append((char)Strings.Heap[addr++]);
            }

            return sb.ToString();
        }

        public string GetBlob(uint addr)
        {
            byte blob = Blob.Heap[addr++];

            if ((blob & 0x80) == 0)
            {
                var bytes = Blob.Heap.AsSpan((int)addr, blob - 1);
                return Encoding.Unicode.GetString(bytes);
            }
            else
            {
                throw new Exception("No support yet for longer blobs.  See II.24.2.4");
            }
        }

        public CLIMetadata(PortableExecutableFile peFile, ImageDataDirectory cliImageDataDirectory)
        {
            // read in the CLI header, _directories[IMAGE_DIRECTORY_ENTRY_COM_DESCRIPTOR]
            var cliHeader = peFile.Memory.GetBytes(cliImageDataDirectory.virtualAddress, Marshal.SizeOf<ImageCOR20Header>()).ToStruct<ImageCOR20Header>();

            // read in the metadata root
            byte[] metadata = peFile.Memory.GetBytes(cliHeader.metadata.virtualAddress, (int)cliHeader.metadata.size);
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
                _streamHeaders[i].ReadHeap(peFile.Memory, cliHeader.metadata.virtualAddress);

                if (_streamHeaders[i].Name == "#~")
                {
                    _metadataLayout = _streamHeaders[i].Heap.ToStruct<MetadataLayout>(0, Marshal.SizeOf<MetadataLayout>());

                    WideStrings = (_metadataLayout.heapSizes & 0x01) != 0;
                    WideGuid = (_metadataLayout.heapSizes & 0x02) != 0;
                    WideBlob = (_metadataLayout.heapSizes & 0x04) != 0;

                    Table = _streamHeaders[i];
                }
                else if (_streamHeaders[i].Name == "#Strings")
                {
                    Strings = _streamHeaders[i];
                }
                else if (_streamHeaders[i].Name == "#US")
                {
                    US = _streamHeaders[i];
                }
                else if (_streamHeaders[i].Name == "#Blob")
                {
                    Blob = _streamHeaders[i];
                }
            }

            int rowCount = 0;
            int offset = Marshal.SizeOf<MetadataLayout>();
            for (int j = 0; j < 63; j++)
            {
                if ((_metadataLayout.valid & (1UL << j)) != 0)
                {
                    rowCount++;
                    TableSizes[j] = BitConverter.ToUInt32(Table.Heap, offset);// Marshal.SizeOf<MetadataLayout>() + 4 * j);
                    offset += 4;
                }
            }

            for (int bit = 0; bit < TableSizes.Length; bit++)
            {
                if (TableSizes[bit] == 0) continue;

                for (int row = 0; row < TableSizes[bit]; row++)
                {
                    if (bit == MetadataTable.Module)
                    {
                        _modules.Add(new ModuleLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.TypeRef)
                    {
                        _typeRefs.Add(new TypeRefLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.TypeDef)
                    {
                        _typeDefs.Add(new TypeDefLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.MethodDef)
                    {
                        _methodDefs.Add(new MethodDefLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.Param)
                    {
                        _params.Add(new ParamLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.MemberRef)
                    {
                        _memberRefs.Add(new MemberRefLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.CustomAttribute)
                    {
                        _customAttributes.Add(new CustomAttributeLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.StandAloneSig)
                    {
                        _standAloneSigs.Add(new StandAloneSigLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.Assembly)
                    {
                        _assemblies.Add(new AssemblyLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.AssemblyRef)
                    {
                        _assemblyRefs.Add(new AssemblyRefLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.Field)
                    {
                        _fields.Add(new FieldLayout(this, ref offset));
                    }
                    else
                    {
                        throw new Exception("Unknown bit index");
                    }
                }
            }

            // figure out when the fields/methods for each type ends (which is where the next typedef fields/methods start)
            // from II.22.37: The run continues to the smaller of:
            //     1. the last row of the Field table
            //     2. the next run of Fields, found by inspecting the FieldList of the next row in this TypeDef table
            for (int i = 0; i < _typeDefs.Count - 1; i++)
            {
                _typeDefs[i].endOfFieldList = _typeDefs[i + 1].fieldList;
                _typeDefs[i].endOfMethodList = _typeDefs[i + 1].methodList;
            }

            for (int i = 0; i < _typeDefs.Count; i++) _typeDefs[i].FindFieldsAndMethods(_fields, _methodDefs);
        }

        private List<ModuleLayout> _modules = new List<ModuleLayout>();
        private List<TypeRefLayout> _typeRefs = new List<TypeRefLayout>();
        private List<TypeDefLayout> _typeDefs = new List<TypeDefLayout>();
        private List<MethodDefLayout> _methodDefs = new List<MethodDefLayout>();
        private List<ParamLayout> _params = new List<ParamLayout>();
        private List<MemberRefLayout> _memberRefs = new List<MemberRefLayout>();
        private List<CustomAttributeLayout> _customAttributes = new List<CustomAttributeLayout>();
        private List<StandAloneSigLayout> _standAloneSigs = new List<StandAloneSigLayout>();
        private List<AssemblyLayout> _assemblies = new List<AssemblyLayout>();
        private List<AssemblyRefLayout> _assemblyRefs = new List<AssemblyRefLayout>();
        private List<FieldLayout> _fields = new List<FieldLayout>();

        public List<TypeDefLayout> TypeDefs {  get { return _typeDefs; } }

        public uint[] TableSizes = new uint[64];
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
}
