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

        public static uint TypeDefOrRefOrSpecEncoded(uint compressed)
        {
            var table = compressed & 0x03;

            switch (table)
            {
                case 0x00: return MetadataTable.TypeDef << 24 | (compressed >> 2);
                case 0x01: return MetadataTable.TypeRef << 24 | (compressed >> 2);
                case 0x02: return MetadataTable.TypeSpec << 24 | (compressed >> 2);
                default: throw new Exception("Unexpected table type");
            }
        }

        public static uint[] DecompressUnsignedSignature(byte[] compressed, int startPosition)
        {
            List<uint> uncompressed = new List<uint>();

            for (int i = startPosition; i < compressed.Length; i++)
            {
                if (i < compressed.Length - 3 && (compressed[i + 3] & 0xE0) == 0xC0)
                {
                    uncompressed.Add(BitConverter.ToUInt32(compressed, i) & 0x1fffffff);
                    i += 3;
                }
                else if (i < compressed.Length - 1 && (compressed[i + 1] & 0xC0) == 0x80)
                {
                    uncompressed.Add((uint)BitConverter.ToUInt16(compressed, i) & 0x3fff);
                    i++;
                }
                else uncompressed.Add(compressed[i]);
            }

            return uncompressed.ToArray();
        }

        public byte[] GetBlob(uint addr)
        {
            byte blob = Blob.Heap[addr++];

            if ((blob & 0x80) == 0)
            {
                var bytes = Blob.Heap.AsSpan((int)addr, blob);
                return bytes.ToArray();
            }
            else
            {
                throw new Exception("No support yet for longer blobs.  See II.24.2.4");
            }
        }

        public byte[] GetMetadata(uint metadataToken)
        {
            if ((metadataToken & 0xff000000) == 0x70000000U)
            {
                int addr = (int)(metadataToken & 0x00ffffff);
                byte blob = US.Heap[addr++];

                if ((blob & 0x80) == 0)
                {
                    return US.Heap.AsSpan(addr, blob - 1).ToArray();
                }
                else
                {
                    throw new Exception("No support yet for longer blobs.  See II.24.2.4");
                }
            }
            else
            {
                throw new Exception("No support yet for getting metadata from the TypeDef table.  See III.1.9");
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
                        _moduleRefs.Add(new ModuleRefLayout(this, ref offset));
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
                    else if (bit == MetadataTable.Constant)
                    {
                        _constants.Add(new ConstantLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.NestedClass)
                    {
                        _nestedClasses.Add(new NestedClassLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.InterfaceImpl)
                    {
                        _interfaceImpls.Add(new InterfaceImplLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.MethodSpec)
                    {
                        _methodSpecs.Add(new MethodSpecLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.GenericParam)
                    {
                        _genericParams.Add(new GenericParamLayout(this, ref offset));
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

            for (int i = 0; i < _memberRefs.Count; i++) _memberRefs[i].FindParentType(this);
        }

        private List<ModuleRefLayout> _moduleRefs = new List<ModuleRefLayout>();
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
        private List<TypeSpecLayout> _typeSpecs = new List<TypeSpecLayout>();
        private List<ConstantLayout> _constants = new List<ConstantLayout>();
        private List<NestedClassLayout> _nestedClasses = new List<NestedClassLayout>();
        private List<InterfaceImplLayout> _interfaceImpls = new List<InterfaceImplLayout>();
        private List<MethodSpecLayout> _methodSpecs = new List<MethodSpecLayout>();
        private List<GenericParamLayout> _genericParams = new List<GenericParamLayout>();

        public List<TypeDefLayout> TypeDefs { get { return _typeDefs; } }
        public List<TypeRefLayout> TypeRefs { get { return _typeRefs; } }
        public List<ModuleRefLayout> ModuleRefs { get { return _moduleRefs; } }
        public List<MethodDefLayout> MethodDefs { get { return _methodDefs; } }
        public List<TypeSpecLayout> TypeSpecs { get { return _typeSpecs; } }
        public List<MemberRefLayout> MemberRefs { get { return _memberRefs; } }
        public List<StandAloneSigLayout> StandAloneSigs { get { return _standAloneSigs; } }
        public List<FieldLayout> Fields { get { return _fields; } }
        public List<AssemblyRefLayout> AssemblyRefs { get { return _assemblyRefs; } }
        public List<MethodSpecLayout> MethodSpecs { get { return _methodSpecs; } }

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
