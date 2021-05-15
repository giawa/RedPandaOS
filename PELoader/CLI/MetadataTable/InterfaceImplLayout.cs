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
            uint tableSize = metadata.TypeDefOrRefCount;
            uint maxTableSize = (1 << 14);

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

            switch (firstByte & 0x03)
            {
                case 0x00: interfaceIndex = 0x02000000 | (interfaceIndex >> 2); break;
                case 0x01: interfaceIndex = 0x01000000 | (interfaceIndex >> 2); break;
                case 0x02: interfaceIndex = 0x1B000000 | (interfaceIndex >> 2); break;
                default: throw new Exception("Invalid table");
            }
        }
    }
}
