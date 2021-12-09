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
                if ((compressed[i] & 0x80) == 0)
                {
                    uncompressed.Add(compressed[i]);
                }
                else if ((compressed[i] & 0xC0) == 0x80)
                {
                    uint temp = compressed[i];
                    temp = (temp << 8) | compressed[i + 1];
                    temp &= 0x3fff;
                    uncompressed.Add(temp);
                    i++;
                }
                else
                {
                    uint temp = compressed[i];
                    temp = (temp << 8) | compressed[i + 1];
                    temp = (temp << 8) | compressed[i + 2];
                    temp = (temp << 8) | compressed[i + 3];
                    temp &= 0x1fffffff;
                    uncompressed.Add(temp);
                    i += 3;
                }
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
                else if ((blob & 0xC0) == 0x80)
                {
                    int size = ((blob & 0x3f) << 8) + US.Heap[addr++];
                    return US.Heap.AsSpan(addr, size - 1).ToArray();
                }
                else if ((blob & 0xE0) == 0xC0)
                {
                    int size = ((blob & 0x1f) << 24) | (US.Heap[addr] << 16) | (US.Heap[addr + 1] << 8) | US.Heap[addr + 2];
                    return US.Heap.AsSpan(addr + 3, size - 1).ToArray();
                }
                else
                {
                    throw new Exception("Blob had an unsupported size.  See II.24.2.4");
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
                    TableSizes[j] = BitConverter.ToUInt32(Table.Heap, offset);
                    offset += 4;
                }
            }

            GetIndexCounts();

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
                    else if (bit == MetadataTable.TypeSpec)
                    {
                        _typeSpecs.Add(new TypeSpecLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.PropertyMap)
                    {
                        _propertyMaps.Add(new PropertyMapLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.Property)
                    {
                        _properties.Add(new PropertyLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.MethodSemantics)
                    {
                        _methodSemantics.Add(new MethodSemanticsLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.DeclSecurity)
                    {
                        _declSecurities.Add(new DeclSecurityLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.ExportedType)
                    {
                        _exportedTypes.Add(new ExportedTypeLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.FieldMarshal)
                    {
                        _fieldMarshals.Add(new FieldMarshalLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.ClassLayout)
                    {
                        _classLayouts.Add(new ClassLayoutLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.FieldLayout)
                    {
                        _fieldLayouts.Add(new FieldLayoutLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.EventMap)
                    {
                        _eventMaps.Add(new EventMapLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.Event)
                    {
                        _events.Add(new EventLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.MethodImpl)
                    {
                        _methodImpls.Add(new MethodImplLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.ModuleRef)
                    {
                        _moduleRefs.Add(new ModuleRefLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.ImplMap)
                    {
                        _implMaps.Add(new ImplMapLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.FieldRVA)
                    {
                        _fieldRVAs.Add(new FieldRVALayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.ManifestResource)
                    {
                        _manifestResources.Add(new ManifestResourceLayout(this, ref offset));
                    }
                    else if (bit == MetadataTable.GenericParamConstraint)
                    {
                        _genericParamConstraints.Add(new GenericParamConstraintLayout(this, ref offset));
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

            for (int i = 0; i < _typeDefs.Count; i++) _typeDefs[i].FindFieldsAndMethods(this);

            // similarly, for MethodDefs find the param list end
            for (int i = 0; i < _methodDefs.Count - 1; i++)
            {
                _methodDefs[i].endOfParamList = _methodDefs[i + 1].paramList;
            }

            for (int i = 0; i < _methodDefs.Count; i++) _methodDefs[i].FindParams(_params);

            for (int i = 0; i < _memberRefs.Count; i++) _memberRefs[i].FindParentType(this);

            for (int i = 0; i < _customAttributes.Count; i++)
            {
                var attribute = _customAttributes[i];
                if ((attribute.parent & 0xff000000) == 0x04000000) _fields[(int)(attribute.parent & 0x00ffffff) - 1].Attributes.Add(attribute);
                else if ((attribute.parent & 0xff000000) == 0x06000000) _methodDefs[(int)(attribute.parent & 0x00ffffff) - 1].Attributes.Add(attribute);
            }
        }

        private void GetIndexCounts()
        {
            TypeDefOrRefCount = GetIndexCount(MetadataTable.TypeDef, MetadataTable.TypeRef, MetadataTable.TypeSpec);
            HasConstantCount = GetIndexCount(MetadataTable.Field, MetadataTable.Param, MetadataTable.Property);
            HasCustomAttributeCount = GetIndexCount(MetadataTable.MethodDef, MetadataTable.Field, MetadataTable.TypeRef,
                MetadataTable.TypeDef, MetadataTable.Param, MetadataTable.InterfaceImpl, MetadataTable.MemberRef,
                MetadataTable.Module, /*MetadataTable.Permission,*/ MetadataTable.Property, MetadataTable.Event,
                MetadataTable.StandAloneSig, MetadataTable.ModuleRef, MetadataTable.TypeSpec, MetadataTable.TypeSpec,
                MetadataTable.Assembly, MetadataTable.AssemblyRef, MetadataTable.File, MetadataTable.ExportedType,
                MetadataTable.ManifestResource, MetadataTable.GenericParam, MetadataTable.GenericParamConstraint,
                MetadataTable.MethodSpec);
            HasFieldMarshallAttributeCount = GetIndexCount(MetadataTable.Field, MetadataTable.Param);
            HasDeclSecurityAttributeCount = GetIndexCount(MetadataTable.TypeDef, MetadataTable.MethodDef, MetadataTable.Assembly);
            MemberRefParentCount = GetIndexCount(MetadataTable.TypeDef, MetadataTable.TypeRef, MetadataTable.ModuleRef, MetadataTable.MethodDef, MetadataTable.TypeSpec);
            HasSemanticsCount = GetIndexCount(MetadataTable.Event, MetadataTable.Property);
            MethodDefOrRefCount = GetIndexCount(MetadataTable.MethodDef, MetadataTable.MemberRef);
            MemberForwardedCount = GetIndexCount(MetadataTable.Field, MetadataTable.MethodDef);
            ImplementationCount = GetIndexCount(MetadataTable.File, MetadataTable.AssemblyRef, MetadataTable.ExportedType);
            CustomAttributeTypeCount = GetIndexCount(MetadataTable.MethodDef, MetadataTable.MemberRef);
            ResolutionScopeCount = GetIndexCount(MetadataTable.Module, MetadataTable.ModuleRef, MetadataTable.AssemblyRef, MetadataTable.TypeRef);
            TypeOrMethodDefCount = GetIndexCount(MetadataTable.TypeDef, MetadataTable.MethodDef);
        }

        private uint GetIndexCount(params byte[] tables)
        {
            uint count = 0;
            foreach (var table in tables) count = Math.Max(count, TableSizes[table]);
            return count;
        }

        internal uint TypeDefOrRefCount { get; private set; }
        internal uint HasConstantCount { get; private set; }
        internal uint HasCustomAttributeCount { get; private set; }
        internal uint HasFieldMarshallAttributeCount { get; private set; }
        internal uint HasDeclSecurityAttributeCount { get; private set; }
        internal uint MemberRefParentCount { get; private set; }
        internal uint HasSemanticsCount { get; private set; }
        internal uint MethodDefOrRefCount { get; private set; }
        internal uint MemberForwardedCount { get; private set; }
        internal uint ImplementationCount { get; private set; }
        internal uint CustomAttributeTypeCount { get; private set; }
        internal uint ResolutionScopeCount { get; private set; }
        internal uint TypeOrMethodDefCount { get; private set; }

        private List<ModuleLayout> _modules = new List<ModuleLayout>();
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
        private List<PropertyMapLayout> _propertyMaps = new List<PropertyMapLayout>();
        private List<PropertyLayout> _properties = new List<PropertyLayout>();
        private List<MethodSemanticsLayout> _methodSemantics = new List<MethodSemanticsLayout>();
        private List<DeclSecurityLayout> _declSecurities = new List<DeclSecurityLayout>();
        private List<ExportedTypeLayout> _exportedTypes = new List<ExportedTypeLayout>();
        private List<FieldMarshalLayout> _fieldMarshals = new List<FieldMarshalLayout>();
        private List<ClassLayoutLayout> _classLayouts = new List<ClassLayoutLayout>();
        private List<FieldLayoutLayout> _fieldLayouts = new List<FieldLayoutLayout>();
        private List<EventMapLayout> _eventMaps = new List<EventMapLayout>();
        private List<EventLayout> _events = new List<EventLayout>();
        private List<MethodImplLayout> _methodImpls = new List<MethodImplLayout>();
        private List<ImplMapLayout> _implMaps = new List<ImplMapLayout>();
        private List<FieldRVALayout> _fieldRVAs = new List<FieldRVALayout>();
        private List<ManifestResourceLayout> _manifestResources = new List<ManifestResourceLayout>();
        private List<GenericParamConstraintLayout> _genericParamConstraints = new List<GenericParamConstraintLayout>();

        public List<TypeDefLayout> TypeDefs { get { return _typeDefs; } }
        public List<TypeRefLayout> TypeRefs { get { return _typeRefs; } }
        public List<ModuleLayout> Modules { get { return _modules; } }
        public List<MethodDefLayout> MethodDefs { get { return _methodDefs; } }
        public List<TypeSpecLayout> TypeSpecs { get { return _typeSpecs; } }
        public List<MemberRefLayout> MemberRefs { get { return _memberRefs; } }
        public List<StandAloneSigLayout> StandAloneSigs { get { return _standAloneSigs; } }
        public List<FieldLayout> Fields { get { return _fields; } }
        public List<AssemblyRefLayout> AssemblyRefs { get { return _assemblyRefs; } }
        public List<MethodSpecLayout> MethodSpecs { get { return _methodSpecs; } }
        public List<ExportedTypeLayout> ExportedTypes { get { return _exportedTypes; } }
        public List<ModuleRefLayout> ModuleRefs { get { return _moduleRefs; } }
        public List<GenericParamLayout> GenericParams { get { return _genericParams; } }
        public List<PropertyLayout> Properties { get { return _properties; } }

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
