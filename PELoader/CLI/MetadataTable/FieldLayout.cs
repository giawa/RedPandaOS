using System;
using System.Collections.Generic;
using System.Text;

namespace PELoader
{
    public class FieldLayout
    {
        public ushort flags;
        public uint name;
        public uint signature;

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
        }

        public string GetName(CLIMetadata metadata)
        {
            return metadata.GetString(name);
        }
    }
}
