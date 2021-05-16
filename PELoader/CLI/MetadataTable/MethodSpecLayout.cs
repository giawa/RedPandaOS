using System;
using System.Text;

namespace PELoader
{
    public class MethodSpecLayout
    {
        public uint method;
        public uint instantiation;
        public MethodSpecSig MemberSignature;

        public MethodSpecLayout(CLIMetadata metadata, ref int offset)
        {
            uint tableSize = metadata.MethodDefOrRefCount;
            uint maxTableSize = (1 << 15);

            if (tableSize >= maxTableSize)
            {
                method = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                method = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            switch (method & 0x01)
            {
                case 0x00: method = 0x06000000 | (method >> 1); break;
                case 0x01: method = 0x0A000000 | (method >> 1); break;
            }

            if (metadata.WideBlob)
            {
                instantiation = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                instantiation = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            MemberSignature = new MethodSpecSig(metadata, instantiation, this);
        }

        private MethodSpecLayout() { }

        public MethodSpecLayout Clone()
        {
            MethodSpecLayout methodSpec = new MethodSpecLayout();
            methodSpec.method = method;
            methodSpec.instantiation = instantiation;
            methodSpec.MemberSignature = MemberSignature.Clone();

            return methodSpec;
        }

        public class MethodSpecSig  // II.23.2.15
        {
            public ElementType[] Types;
            public string[] TypeNames;

            public MethodSpecSig(CLIMetadata metadata, uint addr, MethodSpecLayout methodSpec)
            {
                var blob = metadata.GetBlob(addr);
                var data = CLIMetadata.DecompressUnsignedSignature(blob, 1);    // first byte is the flags

                if (blob[0] == 0x0a)
                {
                    uint genArgCount = data[0];
                    uint offset = 1;

                    Types = new ElementType[genArgCount];
                    TypeNames = new string[genArgCount];

                    for (int i = 0; i < genArgCount; i++)
                    {
                        Types[i] = new ElementType(data, ref offset);
                        TypeNames[i] = NameFromType(Types[i], metadata);
                    }
                }
                else throw new Exception("Unexpected blob type");
            }

            private string NameFromType(ElementType type, CLIMetadata metadata)
            {
                if ((type.Type != ElementType.EType.End && type.Type <= ElementType.EType.String) ||
                            type.Type == ElementType.EType.IntPtr || type.Type == ElementType.EType.UIntPtr ||
                            type.Type == ElementType.EType.Object)
                {
                    return type.Type.ToString();
                }
                else if ((type.Token & 0xff000000) == 0x02000000)
                {
                    return metadata.TypeDefs[(int)(type.Token & 0x00ffffff) - 1].Name;
                }
                else if ((type.Token & 0xff000000) == 0x01000000)
                {
                    return metadata.TypeRefs[(int)(type.Token & 0x00ffffff) - 1].Name;
                }
                else if (type.Type == ElementType.EType.MVar)
                {
                    //type.Token = methodSpec.method;
                    return "";
                }
                else if (type.Type == ElementType.EType.SzArray)
                {
                    return "@" + NameFromType(type.NestedType, metadata);
                }
                else if (type.Type == ElementType.EType.GenericInst)
                {
                    return NameFromType(type.NestedType, metadata);
                }
                else if (type.Type == ElementType.EType.Var)
                {
                    return "";
                }
                else throw new Exception("No implementation for this table type");
            }

            public string ToAsmString()
            {
                StringBuilder sb = new StringBuilder("<");

                for (int i = 0; i < Types.Length; i++)
                {
                    var type = Types[i];

                    if (!string.IsNullOrEmpty(TypeNames[i])) sb.Append(TypeNames[i]);
                    else sb.Append(type.Type);

                    if (i < Types.Length - 1) sb.Append(",");
                }

                sb.Append(">");
                return sb.ToString();
            }

            private MethodSpecSig() { }

            public MethodSpecSig Clone()
            {
                MethodSpecSig methodSig = new MethodSpecSig();
                methodSig.Types = new ElementType[Types.Length];
                for (int i = 0; i < methodSig.Types.Length; i++)
                    methodSig.Types[i] = new ElementType(Types[i]);
                methodSig.TypeNames = (string[])TypeNames.Clone();
                return methodSig;
            }
        }
    }
}
