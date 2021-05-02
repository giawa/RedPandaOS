using System;

namespace PELoader
{
    public class TypeSpecLayout
    {
        public uint signature;

        public TypeSpecLayout(CLIMetadata metadata, ref int offset)
        {
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
        }
    }
}
