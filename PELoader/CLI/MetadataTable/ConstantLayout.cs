using System;

namespace PELoader
{
    class ConstantLayout
    {
        public ElementType.EType type;
        public uint parent;
        public uint value;

        public ConstantLayout(CLIMetadata metadata, ref int offset)
        {
            type = (ElementType.EType)metadata.Table.Heap[offset];
            offset += 2;    // padded with one byte

            byte firstByte = metadata.Table.Heap[offset];

            uint tableSize = 0;
            uint maxTableSize = (1 << 14);

            switch (firstByte & 0x03)
            {
                case 0x00: tableSize = metadata.TableSizes[MetadataTable.Field]; break;
                case 0x01: tableSize = metadata.TableSizes[MetadataTable.Param]; break;
                case 0x02: tableSize = metadata.TableSizes[MetadataTable.Property]; break;
                default: throw new Exception("Invalid table");
            }

            if (tableSize >= maxTableSize)
            {
                parent = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                parent = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            if (metadata.WideBlob)
            {
                value = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                value = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }
        }
    }
}
