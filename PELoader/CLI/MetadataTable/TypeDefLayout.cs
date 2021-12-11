using System;
using System.Collections.Generic;

namespace PELoader
{
    public class TypeDefLayout
    {
        public uint typeAttributes;
        private uint typeName;
        private uint typeNamespace;
        public uint typeDefOrRef;
        public uint fieldList;
        public uint methodList;
        public uint propertyList;

        public uint endOfFieldList;
        public uint endOfMethodList;
        public uint endOfPropertyList;

        public List<FieldLayout> Fields = new List<FieldLayout>();
        public List<MethodDefLayout> Methods = new List<MethodDefLayout>();
        public List<PropertyLayout> Properties = new List<PropertyLayout>();

        public string Name { get; private set; }

        public string Namespace { get; private set; }

        public TypeRefLayout BaseType { get; private set; }

        public uint TypeDefOrRef { get { return typeDefOrRef; } }

        public uint Token { get; internal set; }

        public TypeDefLayout(CLIMetadata metadata, ref int offset)
        {
            typeAttributes = BitConverter.ToUInt32(metadata.Table.Heap, offset);
            offset += 4;

            if (metadata.WideStrings)
            {
                typeName = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
                typeNamespace = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                typeName = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
                typeNamespace = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            byte firstByte = metadata.Table.Heap[offset];

            uint tableSize = metadata.TypeDefOrRefCount;
            uint maxTableSize = (1 << 14);

            if (tableSize >= maxTableSize)
            {
                typeDefOrRef = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                typeDefOrRef = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            switch (firstByte & 0x03)
            {
                case 0x00: typeDefOrRef = 0x02000000 | (typeDefOrRef >> 2); break;
                case 0x01: typeDefOrRef = 0x01000000 | (typeDefOrRef >> 2); break;
                case 0x02: typeDefOrRef = 0x1B000000 | (typeDefOrRef >> 2); break;
            }

            if (metadata.TableSizes[MetadataTable.Field] > ushort.MaxValue)
            {
                fieldList = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                fieldList = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            if (metadata.TableSizes[MetadataTable.MethodDef] > ushort.MaxValue)
            {
                methodList = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                methodList = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            Name = metadata.GetString(typeName);
            Namespace = metadata.GetString(typeNamespace);
        }

        public void FindFieldsAndMethods(CLIMetadata metadata)
        {
            var fields = metadata.Fields;
            var methodDefs = metadata.MethodDefs;
            var properties = metadata.Properties;

            if (endOfFieldList == 0) endOfFieldList = (uint)(fields.Count + 1);
            if (endOfMethodList == 0) endOfMethodList = (uint)(methodDefs.Count + 1);
            if (endOfPropertyList == 0) endOfPropertyList = (uint)(properties.Count + 1);

            for (uint i = fieldList; i < endOfFieldList; i++)
            {
                Fields.Add(fields[(int)i - 1]);
                fields[(int)i - 1].Parent = this;
            }
            for (uint i = methodList; i < endOfMethodList; i++)
            {
                Methods.Add(methodDefs[(int)i - 1]);
                methodDefs[(int)i - 1].Parent = this;
            }
            if (propertyList != 0)
            {
                for (uint i = propertyList; i < endOfPropertyList; i++)
                {
                    Properties.Add(properties[(int)i - 1]);
                    properties[(int)i - 1].Parent = this;
                }
            }

            if ((typeDefOrRef & 0xff000000) == 0x01000000) BaseType = metadata.TypeRefs[(int)(typeDefOrRef & 0x00ffffff) - 1];
        }

        public string FullName
        {
            get { return $"{Namespace}.{Name}"; }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
