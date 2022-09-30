using System;

namespace PELoader
{
    public class TypeSpecLayout
    {
        public uint signature;
        private CLIMetadata metadata;

        public MethodRefSig MethodSignature { get; private set; }

        public ElementType Type { get; private set; }

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

            var blob = metadata.GetBlob(signature);

            Type = new ElementType((ElementType.EType)blob[0]);

            switch (blob[0])
            {
                case 0x0f:  // PTR
                    break;
                case 0x1b:  // FNPTR
                    break;
                case 0x14:  // ARRAY
                    break;
                case 0x1D:  // SZARRAY
                    break;
                case 0x15:  // GENERICINST
                    var classOrValueType = blob[1];
                    var decompressed = CLIMetadata.DecompressUnsignedSignature(blob, 2);
                    Parent = CLIMetadata.TypeDefOrRefOrSpecEncoded(decompressed[0]);
                    GenericSig = new GenericInstSig(decompressed, 1);
                    break;
            }

            this.metadata = metadata;
        }

        public GenericInstSig GenericSig { get; private set; }

        public uint Parent { get; private set; }

        public override string ToString()
        {
            if (Parent == 0) return "????";

            switch (Parent & 0xff000000)
            {
                case 0x02000000: return metadata.TypeDefs[(int)(Parent & 0x00ffffff) - 1].FullName;
                case 0x01000000: return metadata.TypeRefs[(int)(Parent & 0x00ffffff) - 1].FullName;
                case 0x1B000000: return metadata.TypeSpecs[(int)(Parent & 0x00ffffff) - 1].ToString();
                default: return "????";
            }
        }
    }

    public class GenericInstSig
    {
        public SigFlags Flags { get; private set; }
        public uint ParamCount { get; private set; }
        public uint GenParamCount { get; private set; }
        public ElementType RetType { get; private set; }
        public ElementType[] Params { get; private set; }

        public GenericInstSig(MethodSpecLayout.MethodSpecSig sig)
        {
            ParamCount = (uint)sig.Types.Length;
            Params = sig.Types;
        }

        public GenericInstSig(uint[] data, uint offset)
        {
            ParseSignature(data, offset);
        }

        private void ParseSignature(uint[] data, uint offset)
        {
            ParamCount = data[offset++];

            if (ParamCount > 0)
            {
                Params = new ElementType[ParamCount];
                for (uint p = 0; p < ParamCount; p++)
                {
                    Params[p] = new ElementType(data, ref offset);
                }
            }
        }
    }
}
