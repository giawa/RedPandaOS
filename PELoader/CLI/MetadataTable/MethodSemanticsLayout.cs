using System;

namespace PELoader
{
    [Flags]
    public enum MethodSemanticsAttributes : ushort
    {
        Setter = 0x0001,
        Getter = 0x0002,
        Other = 0x0004,
        AddOn = 0x0008,
        RemoveOn = 0x0010,
        Fire = 0x0020
    }

    public class MethodSemanticsLayout
    {
        public MethodSemanticsAttributes flags;
        private uint method;
        public MethodDefLayout Method;
        public uint association;

        public MethodSemanticsLayout(CLIMetadata metadata, ref int offset)
        {
            flags = (MethodSemanticsAttributes)BitConverter.ToUInt16(metadata.Table.Heap, offset);
            offset += 2;

            if (metadata.TableSizes[MetadataTable.MethodDef] > ushort.MaxValue)
            {
                method = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                method = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            Method = metadata.MethodDefs[(int)method - 1];

            byte firstByte = metadata.Table.Heap[offset];

            uint tableSize = 0;
            uint maxTableSize = (1 << 15);

            switch (firstByte & 0x01)
            {
                case 0x00: tableSize = metadata.TableSizes[MetadataTable.Event]; break;
                case 0x01: tableSize = metadata.TableSizes[MetadataTable.Property]; break;
            }

            if (tableSize >= maxTableSize)
            {
                association = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                association = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            switch (firstByte & 0x01)
            {
                case 0x00: association = 0x14000000 | (association >> 1); break;
                case 0x01: association = 0x17000000 | (association >> 1); break;
            }
        }
    }
}
