using System;

namespace PELoader
{
    public class FieldLayoutLayout
    {
        public uint offset;
        private uint field;

        public FieldLayout Field { get; private set; }

        public FieldLayoutLayout(CLIMetadata metadata, ref int offset)
        {
            this.offset = BitConverter.ToUInt32(metadata.Table.Heap, offset);
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
