using System;

namespace PELoader
{
    public class ParamLayout
    {
        public ushort flags;
        public ushort sequence;
        private uint name;

        public string Name { get; private set; }

        public MethodDefLayout Parent;

        public ParamLayout(CLIMetadata metadata, ref int offset)
        {
            flags = BitConverter.ToUInt16(metadata.Table.Heap, offset);
            offset += 2;

            sequence = BitConverter.ToUInt16(metadata.Table.Heap, offset);
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

            Name = metadata.GetString(name);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
