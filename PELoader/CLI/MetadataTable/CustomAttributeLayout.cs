using System;

namespace PELoader
{
    public class CustomAttributeLayout
    {
        public uint parent;
        public uint type;
        public uint value;

        public CustomAttributeLayout(CLIMetadata metadata, ref int offset)
        {
            uint tableSize = metadata.HasCustomAttributeCount;
            uint maxTableSize = (1 << 11);

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

            tableSize = metadata.CustomAttributeTypeCount;
            maxTableSize = (1 << 13);

            if (tableSize >= maxTableSize)
            {
                type = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                type = BitConverter.ToUInt16(metadata.Table.Heap, offset);
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
