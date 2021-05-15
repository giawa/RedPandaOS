using System;

namespace PELoader
{
    public class EventLayout
    {
        [Flags]
        public enum EventAttributes
        {
            SpecialName = 0x0200,
            RTSpecialName = 0x0400
        }

        public EventAttributes eventFlags;
        private uint name;
        public uint eventType;

        public string Name { get; private set; }

        public EventLayout(CLIMetadata metadata, ref int offset)
        {
            eventFlags = (EventAttributes)BitConverter.ToUInt16(metadata.Table.Heap);
            offset += 2;

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
            uint tableSize = metadata.TypeDefOrRefCount;
            uint maxTableSize = (1 << 14);

            if (tableSize >= maxTableSize)
            {
                eventType = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                eventType = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            switch (firstByte & 0x03)
            {
                case 0x00: eventType = 0x02000000 | (eventType >> 2); break;
                case 0x01: eventType = 0x01000000 | (eventType >> 2); break;
                case 0x02: eventType = 0x1B000000 | (eventType >> 2); break;
                default: throw new Exception("Invalid table");
            }

            Name = metadata.GetString(name);
        }
    }
}
