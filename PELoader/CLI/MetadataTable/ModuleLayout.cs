using System;

namespace PELoader
{
    public class ModuleLayout
    {
        public ushort generation;
        public uint name;
        public ushort mvid;
        public ushort encId;
        public ushort encBaseId;

        public ModuleLayout(CLIMetadata metadata, ref int offset)
        {
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

            mvid = BitConverter.ToUInt16(metadata.Table.Heap, offset);
            offset += 2;

            // last 4 bytes are reserved, so skip them as well
            offset += 4;
        }

        public string GetName(CLIMetadata metadata)
        {
            return metadata.GetString(name);
        }
    }
}
