using System;
using System.Collections.Generic;

namespace PELoader
{
    public class FieldLayout
    {
        public FieldLayoutFlags flags;
        public uint name;
        public uint signature;

        public TypeDefLayout Parent;

        public List<CustomAttributeLayout> Attributes = new List<CustomAttributeLayout>();

        [Flags]
        public enum FieldLayoutFlags : ushort
        {
            Private = 0x0001,
            Static = 0x0010,
            InitOnly = 0x0020,
            Literal = 0x0040,
            NotSerialized = 0x0080,
            SpecialName = 0x0200,
            PinvokeImpl = 0x2000,
            RTSpecialName = 0x400,
            HasFieldMarshal = 0x1000,
            HasDefault = 0x8000,
            HasFieldRVA = 0x0100
        }

        public FieldLayout(CLIMetadata metadata, ref int offset)
        {
            flags = (FieldLayoutFlags)BitConverter.ToUInt16(metadata.Table.Heap, offset);
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

            Name = metadata.GetString(name);

            var blob = metadata.GetBlob(signature);
            if (blob[0] == 0x06)
            {
                var uncompressed = CLIMetadata.DecompressUnsignedSignature(blob, 1);    // first byte is 0x06 for fields
                Type = new ElementType(uncompressed);
            }
            else
            {
                throw new Exception();
            }
        }

        public ElementType Type { get; private set; }

        public string Name { get; private set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
