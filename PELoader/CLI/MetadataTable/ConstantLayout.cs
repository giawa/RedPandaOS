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

            uint tableSize = metadata.HasConstantCount;
            uint maxTableSize = (1 << 14);

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
