using System;

namespace PELoader
{
    public class FieldLayout
    {
        public ushort flags;
        public uint name;
        public uint signature;

        public TypeDefLayout Parent;

        public FieldLayout(CLIMetadata metadata, ref int offset)
        {
            flags = BitConverter.ToUInt16(metadata.Table.Heap, offset);
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
                signature = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                signature = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            _name = metadata.GetString(name);
        }

        private string _name;

        public override string ToString()
        {
            return _name;
        }
    }
}
