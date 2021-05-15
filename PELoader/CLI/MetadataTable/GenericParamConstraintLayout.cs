using System;

namespace PELoader
{
    public class GenericParamConstraintLayout
    {
        private uint owner;
        public uint constraint;

        public GenericParamLayout Owner;

        public GenericParamConstraintLayout(CLIMetadata metadata, ref int offset)
        {
            if (metadata.TableSizes[MetadataTable.GenericParam] >= ushort.MaxValue)
            {
                owner = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                owner = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            uint tableSize = metadata.TypeDefOrRefCount;
            uint maxTableSize = (1 << 14);

            if (tableSize >= maxTableSize)
            {
                constraint = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                constraint = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            switch (constraint & 0x03)
            {
                case 0x00: constraint = 0x02000000 | (constraint >> 2); break;
                case 0x01: constraint = 0x01000000 | (constraint >> 2); break;
                case 0x02: constraint = 0x1B000000 | (constraint >> 2); break;
            }

            Owner = metadata.GenericParams[(int)owner - 1];
        }
    }
}
