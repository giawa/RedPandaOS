using System;
using System.Collections.Generic;
using System.Text;

namespace PELoader
{
    public class GenericParamLayout
    {
        public ushort number;
        public ushort flags;
        public uint owner;
        public uint name;

        public string Name { get; private set; }

        public GenericParamLayout(CLIMetadata metadata, ref int offset)
        {
            number = BitConverter.ToUInt16(metadata.Table.Heap, offset);
            offset += 2;

            flags = BitConverter.ToUInt16(metadata.Table.Heap, offset);
            offset += 2;

            byte firstByte = metadata.Table.Heap[offset];

            uint tableSize = 0;
            uint maxTableSize = (1 << 15);

            switch (firstByte & 0x01)
            {
                case 0x00: tableSize = metadata.TableSizes[MetadataTable.TypeDef]; break;
                case 0x01: tableSize = metadata.TableSizes[MetadataTable.MethodDef]; break;
            }

            if (tableSize >= maxTableSize)
            {
                owner = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                owner = BitConverter.ToUInt16(metadata.Table.Heap, offset);
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

            Name = metadata.GetString(name);
        }
    }
}
