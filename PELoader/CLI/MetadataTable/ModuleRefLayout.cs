using System;
using System.Collections.Generic;
using System.Text;

namespace PELoader
{
    public class ModuleRefLayout
    {
        private uint name;

        public string Name { get; private set; }

        public ModuleRefLayout(CLIMetadata metadata, ref int offset)
        {
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
    }
}
