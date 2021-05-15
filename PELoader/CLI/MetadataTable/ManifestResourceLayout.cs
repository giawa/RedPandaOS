using System;

namespace PELoader
{
    public class ManifestResourceLayout
    {
        [Flags]
        public enum ManifestResourceAttributes
        {
        }

        public uint offset;
        public ManifestResourceAttributes flags;
        private uint name;
        public uint implementation;

        public string Name { get; private set; }

        public ManifestResourceLayout(CLIMetadata metadata, ref int offset)
        {
            this.offset = BitConverter.ToUInt32(metadata.Table.Heap, offset);
            offset += 4;

            flags = (ManifestResourceAttributes)BitConverter.ToUInt32(metadata.Table.Heap, offset);
            offset += 4;

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
                default: throw new Exception("Invalid table");
            }

            Name = metadata.GetString(name);
        }
    }
}
