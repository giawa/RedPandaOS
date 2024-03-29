﻿using System;

namespace PELoader
{
    public class NestedClassLayout
    {
        private uint nestedClass;
        private uint enclosingClass;

        public TypeDefLayout NestedClass { get; private set; }
        public TypeDefLayout EnclosingClass { get; private set; }

        public NestedClassLayout(CLIMetadata metadata, ref int offset)
        {
            if (metadata.TypeDefs.Count > ushort.MaxValue)
            {
                nestedClass = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;

                enclosingClass = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                nestedClass = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;

                enclosingClass = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            NestedClass = metadata.TypeDefs[(int)nestedClass - 1];
            EnclosingClass = metadata.TypeDefs[(int)enclosingClass - 1];
        }
    }
}
