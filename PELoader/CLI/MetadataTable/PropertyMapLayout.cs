using System;

namespace PELoader
{
    public class PropertyMapLayout
    {
        public uint parent;
        public uint propertyList;

        public PropertyMapLayout(CLIMetadata metadata, ref int offset)
        {
            if (metadata.TableSizes[MetadataTable.TypeDef] > ushort.MaxValue)
            {
                parent = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                parent = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            if (metadata.TableSizes[MetadataTable.Property] > ushort.MaxValue)
            {
                propertyList = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                propertyList = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            metadata.TypeDefs[(int)parent - 1].propertyList = propertyList;
        }
    }
}
