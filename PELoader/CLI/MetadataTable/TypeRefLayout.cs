using System;

namespace PELoader
{
    public class TypeRefLayout
    {
        public uint resolutionScope;
        public uint typeName;
        public uint typeNamespace;

        public TypeRefLayout(CLIMetadata metadata, ref int offset)
        {
            byte firstByte = metadata.Table.Heap[offset];

            uint tableSize = 0;
            uint maxTableSize = (1 << 14);

            switch (firstByte & 0x03)
            {
                case 0x00: tableSize = metadata.TableSizes[MetadataTable.Module]; break;
                case 0x01: tableSize = metadata.TableSizes[MetadataTable.ModuleRef]; break;
                case 0x02: tableSize = metadata.TableSizes[MetadataTable.AssemblyRef]; break;
                case 0x03: tableSize = metadata.TableSizes[MetadataTable.TypeRef]; break;
                default: throw new Exception("Invalid table");
            }

            if (tableSize >= maxTableSize)
            {
                resolutionScope = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                resolutionScope = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

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
        }

        public string GetName(CLIMetadata metadata)
        {
            return metadata.GetString(typeName);
        }
    }
}
