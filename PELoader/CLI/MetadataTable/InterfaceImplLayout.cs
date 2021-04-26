using System;

namespace PELoader
{
    public class InterfaceImplLayout
    {
        public uint classIndex;
        public uint interfaceIndex;

        public InterfaceImplLayout(CLIMetadata metadata, ref int offset)
        {
            if (metadata.TypeDefs.Count > ushort.MaxValue)
            {
                classIndex = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;    // padded with one byte
            }
            else
            {
                classIndex = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;    // padded with one byte
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
                interfaceIndex = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                interfaceIndex = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }
        }
    }
}
