using System;

namespace PELoader
{
    public class MemberRefLayout
    {
        public uint classIndex;
        public uint name;
        public uint signature;

        public MemberRefLayout(CLIMetadata metadata, ref int offset)
        {
            byte firstByte = metadata.Table.Heap[offset];

            uint tableSize = 0;
            uint maxTableSize = (1 << 13);

            switch (firstByte & 0x07)
            {
                case 0x00: tableSize = metadata.TableSizes[MetadataTable.TypeDef]; break;
                case 0x01: tableSize = metadata.TableSizes[MetadataTable.TypeRef]; break;
                case 0x02: tableSize = metadata.TableSizes[MetadataTable.ModuleRef]; break;
                case 0x03: tableSize = metadata.TableSizes[MetadataTable.MethodDef]; break;
                case 0x04: tableSize = metadata.TableSizes[MetadataTable.TypeSpec]; break;
                default: throw new Exception("Invalid table");
            }

            if (tableSize >= maxTableSize)
            {
                classIndex = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                classIndex = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            if (metadata.WideStrings)
            {
                name = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                name = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            if (metadata.WideBlob)
            {
                signature = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                signature = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }
        }

        public string GetName(CLIMetadata metadata)
        {
            return metadata.GetString(name);
        }
    }
}
