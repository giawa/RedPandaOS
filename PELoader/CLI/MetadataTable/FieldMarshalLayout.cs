using System;

namespace PELoader
{
    public class FieldMarshalLayout
    {
        public uint parent;
        public uint nativeType;

        public FieldMarshalLayout(CLIMetadata metadata, ref int offset)
        {
            byte firstByte = metadata.Table.Heap[offset];

            uint tableSize = metadata.HasFieldMarshallAttributeCount;
            uint maxTableSize = (1 << 15);

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
                case 0x00: parent = 0x04000000 | (parent >> 1); break;
                case 0x01: parent = 0x08000000 | (parent >> 1); break;
            }

            if (metadata.WideBlob)
            {
                nativeType = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                nativeType = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }
        }
    }
}
