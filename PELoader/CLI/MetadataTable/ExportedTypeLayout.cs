using System;

namespace PELoader
{
    public class ExportedTypeLayout
    {
        private uint flags;
        public uint typeDefId;
        private uint typeName;
        private uint typeNamespace;
        public uint implementation;

        public string TypeName { get; private set; }
        public string TypeNamespace { get; private set; }

        public ExportedTypeLayout(CLIMetadata metadata, ref int offset)
        {
            flags = BitConverter.ToUInt32(metadata.Table.Heap, offset);
            offset += 4;

            typeDefId = BitConverter.ToUInt32(metadata.Table.Heap, offset);
            offset += 4;

            if (metadata.WideStrings)
            {
                typeName = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;

                typeNamespace = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                typeName = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;

                typeNamespace = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            byte firstByte = metadata.Table.Heap[offset];

            uint tableSize = metadata.ImplementationCount;
            uint maxTableSize = (1 << 14);

            if (tableSize >= maxTableSize)
            {
                implementation = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                implementation = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            switch (firstByte & 0x03)
            {
                case 0x00: implementation = 0x26000000 | (implementation >> 2); break;
                case 0x01: implementation = 0x23000000 | (implementation >> 2); break;
                case 0x02: implementation = 0x27000000 | (implementation >> 2); break;
            }

            TypeName = metadata.GetString(typeName);
            TypeNamespace = metadata.GetString(typeNamespace);
        }
    }
}
