using System;

namespace PELoader
{
    public class ParamLayout
    {
        public ushort flags;
        public ushort sequence;
        public uint name;

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
        }

        public string GetName(CLIMetadata metadata)
        {
            return metadata.GetString(name);
        }
    }
}
