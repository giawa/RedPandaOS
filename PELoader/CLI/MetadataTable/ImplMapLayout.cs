using System;

namespace PELoader
{
    public class ImplMapLayout
    {
        [Flags]
        public enum PInvokeAttributes
        {
            NoMangle = 0x0001,

            CharSetMask = 0x0006,
            CharSetNotSpec = 0x0000,
            CharSetAnsi = 0x0002,
            CharSetUnicode = 0x0004,
            CharSetAuto = 0x0006,

            SupportsLastError = 0x0040,

            CallConvMask = 0x0700,
            CallConvPlatformApi = 0x0100,
            CallConvCdecl = 0x0200,
            CallConvStdcall = 0x0300,
            CallConvThiscall = 0x0400,
            CallConvFastcasll = 0x0500
        }

        public PInvokeAttributes mappingFlags;
        public uint memberForwarded;
        private uint importName;
        private uint importScope;

        public string ImportName { get; private set; }

        public ModuleRefLayout ImportScope { get; private set; }

        public ImplMapLayout(CLIMetadata metadata, ref int offset)
        {
            mappingFlags = (PInvokeAttributes)BitConverter.ToUInt16(metadata.Table.Heap);
            offset += 2;

            byte firstByte = metadata.Table.Heap[offset];
            uint tableSize = metadata.MemberForwardedCount;
            uint maxTableSize = (1 << 15);

            if (tableSize >= maxTableSize)
            {
                memberForwarded = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                memberForwarded = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            switch (firstByte & 0x01)
            {
                case 0x00: memberForwarded = 0x04000000 | (memberForwarded >> 1); break;
                case 0x01: memberForwarded = 0x06000000 | (memberForwarded >> 1); break;
            }

            if (metadata.WideStrings)
            {
                importName = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                importName = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            if (metadata.TableSizes[MetadataTable.ModuleRef] >= ushort.MaxValue)
            {
                importScope = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                importScope = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            ImportName = metadata.GetString(importName);
            ImportScope = metadata.ModuleRefs[(int)importScope - 1];
        }
    }
}
