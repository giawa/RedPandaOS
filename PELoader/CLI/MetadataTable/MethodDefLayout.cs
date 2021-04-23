using System;

namespace PELoader
{
    public class MethodDefLayout
    {
        public uint rva;
        public ushort implFlags;
        public ushort flags;
        private uint name;
        public uint signature;
        public uint paramList;

        public TypeDefLayout Parent;
        public MethodRefSig MethodSignature;

        public MethodDefLayout(CLIMetadata metadata, ref int offset)
        {
            rva = BitConverter.ToUInt32(metadata.Table.Heap, offset);
            offset += 4;

            implFlags = BitConverter.ToUInt16(metadata.Table.Heap, offset);
            offset += 2;

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

            if (metadata.TableSizes[0x08] > ushort.MaxValue)    // param table
            {
                paramList = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                paramList = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            Name = metadata.GetString(name);
            MethodSignature = new MethodRefSig(metadata, signature);
        }

        public string Name { get; private set; }

        public override string ToString()
        {
            return $"{Parent.Name}.{Name}";
        }
    }
}
