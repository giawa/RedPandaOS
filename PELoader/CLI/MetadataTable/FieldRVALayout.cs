using System;

namespace PELoader
{
    public class FieldRVALayout
    {
        public uint RVA;
        private uint field;

        public FieldLayout Field { get; private set; }

        public FieldRVALayout(CLIMetadata metadata, ref int offset)
        {
            RVA = BitConverter.ToUInt32(metadata.Table.Heap, offset);
            offset += 4;

            if (metadata.TableSizes[MetadataTable.Field] >= ushort.MaxValue)
            {
                field = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                field = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            Field = metadata.Fields[(int)field - 1];
        }
    }
}
