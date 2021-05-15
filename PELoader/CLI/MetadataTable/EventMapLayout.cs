using System;

namespace PELoader
{
    public class EventMapLayout
    {
        private uint parent;
        public uint eventList;

        public TypeDefLayout Parent { get; private set; }

        public EventMapLayout(CLIMetadata metadata, ref int offset)
        {
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

            if (metadata.TableSizes[MetadataTable.Event] >= ushort.MaxValue)
            {
                eventList = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                eventList = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            Parent = metadata.TypeDefs[(int)parent - 1];
        }
    }
}
