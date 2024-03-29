﻿using System;

namespace PELoader
{
    public class TypeRefLayout
    {
        public uint resolutionScope;
        public uint typeName;
        public uint typeNamespace;

        public uint ResolutionScope { get; private set; }

        public string Name { get; private set; }
        public string Namespace { get; private set; }

        public TypeRefLayout(CLIMetadata metadata, ref int offset)
        {
            byte firstByte = metadata.Table.Heap[offset];

            uint tableSize = metadata.ResolutionScopeCount;
            uint maxTableSize = (1 << 14);

            if (tableSize >= maxTableSize)
            {
                resolutionScope = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                resolutionScope = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            switch (firstByte & 0x03)
            {
                case 0x00: ResolutionScope = resolutionScope >> 2; break;
                case 0x01: ResolutionScope = 0x1A000000 | (resolutionScope >> 2); break;
                case 0x02: ResolutionScope = 0x23000000 | (resolutionScope >> 2); break;
                case 0x03: ResolutionScope = 0x01000000 | (resolutionScope >> 2); break;
            }

            if (metadata.WideStrings)
            {
                typeName = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
                typeNamespace = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                typeName = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
                typeNamespace = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            Name = metadata.GetString(typeName);
            Namespace = metadata.GetString(typeNamespace);
        }

        public string FullName
        {
            get { return $"{Namespace}.{Name}"; }
        }

        public override string ToString()
        {
            return $"{Namespace}.{Name}";
        }
    }
}
