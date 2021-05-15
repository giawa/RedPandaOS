using System;

namespace PELoader
{
    public class MethodImplLayout
    {
        private uint _class;
        public uint methodBody;
        public uint methodDeclaration;

        public TypeDefLayout Class;

        public MethodImplLayout(CLIMetadata metadata, ref int offset)
        {
            if (metadata.TableSizes[MetadataTable.TypeDef] >= ushort.MaxValue)
            {
                _class = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                _class = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            byte firstByte = metadata.Table.Heap[offset];
            uint tableSize = metadata.MethodDefOrRefCount;
            uint maxTableSize = (1 << 15);

            if (tableSize >= maxTableSize)
            {
                methodBody = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                methodBody = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            switch (firstByte & 0x01)
            {
                case 0x00: methodBody = 0x06000000 | (methodBody >> 1); break;
                case 0x01: methodBody = 0x0A000000 | (methodBody >> 1); break;
            }

            firstByte = metadata.Table.Heap[offset];
            tableSize = metadata.MethodDefOrRefCount;

            if (tableSize >= maxTableSize)
            {
                methodDeclaration = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                methodDeclaration = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            switch (firstByte & 0x01)
            {
                case 0x00: methodDeclaration = 0x06000000 | (methodDeclaration >> 1); break;
                case 0x01: methodDeclaration = 0x0A000000 | (methodDeclaration >> 1); break;
            }

            Class = metadata.TypeDefs[(int)_class - 1];
        }
    }
}
