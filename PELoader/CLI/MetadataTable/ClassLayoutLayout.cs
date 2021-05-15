using System;

namespace PELoader
{
    public class ClassLayoutLayout
    {
        public ushort packingSize;
        public uint classSize;
        private uint parent;

        public TypeDefLayout Parent { get; private set; }

        public ClassLayoutLayout(CLIMetadata metadata, ref int offset)
        {
            packingSize = BitConverter.ToUInt16(metadata.Table.Heap, offset);
            offset += 2;

            classSize = BitConverter.ToUInt32(metadata.Table.Heap, offset);
            offset += 4;

            if (metadata.TableSizes[MetadataTable.TypeDef] >= ushort.MaxValue)
            {
                parent = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                parent = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            Parent = metadata.TypeDefs[(int)parent - 1];
        }
    }
}
