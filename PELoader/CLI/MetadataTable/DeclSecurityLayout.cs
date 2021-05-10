using System;
using System.Collections.Generic;
using System.Text;

namespace PELoader
{
    class DeclSecurityLayout
    {
        public ushort action;
        public uint parent;
        public uint permissionSet;

        public DeclSecurityLayout(CLIMetadata metadata, ref int offset)
        {
            action = BitConverter.ToUInt16(metadata.Table.Heap, offset);
            offset += 2;

            byte firstByte = metadata.Table.Heap[offset];

            uint tableSize = 0;
            uint maxTableSize = (1 << 14);

            switch (firstByte & 0x03)
            {
                case 0x00: tableSize = metadata.TableSizes[MetadataTable.TypeDef]; break;
                case 0x01: tableSize = metadata.TableSizes[MetadataTable.MethodDef]; break;
                case 0x02: tableSize = metadata.TableSizes[MetadataTable.Assembly]; break;
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

            switch (firstByte & 0x03)
            {
                case 0x00: parent = 0x02000000 | (parent >> 2); break;
                case 0x01: parent = 0x06000000 | (parent >> 2); break;
                case 0x02: parent = 0x20000000 | (parent >> 2); break;
                default: throw new Exception("Invalid table");
            }

            if (metadata.WideBlob)
            {
                permissionSet = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                permissionSet = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }
        }
    }
}
