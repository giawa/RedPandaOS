using System;

namespace PELoader
{
    [Flags]
    public enum PropertyAttributes : ushort
    {
        SpecialName = 0x0200,
        RTSpecialName = 0x0400,
        HasDefault = 0x1000,
        Unused = 0xE9FF
    }

    public class PropertyLayout
    {
        public PropertyAttributes flags;
        public uint type;
        private uint name;

        public string Name { get; private set; }

        public TypeDefLayout Parent;

        public PropertyLayout(CLIMetadata metadata, ref int offset)
        {
            flags = (PropertyAttributes)BitConverter.ToUInt16(metadata.Table.Heap, offset);
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

            if (metadata.WideBlob)
            {
                type = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                type = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            Name = metadata.GetString(name);
        }
    }
}
