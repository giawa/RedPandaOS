using System;
using System.Collections.Generic;

namespace PELoader
{
    public class TypeDefLayout
    {
        public uint typeAttributes;
        public uint typeName;
        public uint typeNamespace;
        public uint typeDefOrRef;
        public uint fieldList;
        public uint methodList;

        public uint endOfFieldList;
        public uint endOfMethodList;

        public List<FieldLayout> Fields = new List<FieldLayout>();
        public List<MethodDefLayout> Methods = new List<MethodDefLayout>();

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

            uint tableSize = 0;
            uint maxTableSize = (1 << 14);

            switch (firstByte & 0x03)
            {
                case 0x00: tableSize = metadata.TableSizes[MetadataTable.TypeDef]; break;
                case 0x01: tableSize = metadata.TableSizes[MetadataTable.TypeRef]; break;
                case 0x02: tableSize = metadata.TableSizes[MetadataTable.TypeSpec]; break;
                default: throw new Exception("Invalid table");
            }

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

            if (metadata.TableSizes[0x04] > ushort.MaxValue)
            {
                fieldList = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                fieldList = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            if (metadata.TableSizes[0x06] > ushort.MaxValue)
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

        public void FindFieldsAndMethods(List<FieldLayout> fields, List<MethodDefLayout> methodDefs)
        {
            if (endOfFieldList == 0) endOfFieldList = (uint)(fields.Count + 1);
            if (endOfMethodList == 0) endOfMethodList = (uint)(methodDefs.Count + 1);

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
        }

        public string Name { get; private set; }

        public string Namespace { get; private set; }

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
