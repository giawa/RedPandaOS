using System;
using System.Collections.Generic;
using System.Text;

namespace PELoader
{
    [Flags]
    public enum SigFlags : byte
    {
        HASTHIS = 0x20,
        EXPLICITTHIS = 0x40,
        DEFAULT = 0x00,
        VARARG = 0x50,
        GENERIC = 0x10
    }

    public class ElementType
    {
        public enum EType : uint
        {
            End = 0x00,
            Void = 0x01,
            Boolean = 0x02,
            Char = 0x03,
            I1 = 0x04,
            U1 = 0x05,
            I2 = 0x06,
            U2 = 0x07,
            I4 = 0x08,
            U4 = 0x09,
            I8 = 0x0A,
            U8 = 0x0B,
            R4 = 0x0C,
            R8 = 0x0D,
            String = 0x0E,
            Ptr = 0x0f,
            ByRef = 0x10,
            ValueType = 0x11,
            Class = 0x12,
            Var = 0x13,
            Array = 0x14,
            GenericInst = 0x15,
            TypedByRef = 0x16,
            IntPtr = 0x18,
            UIntPtr = 0x19,
            MethodSignature = 0x1B,
            Object = 0x1C,
            SzArray = 0x1D,
            MVar = 0x1E,
            RequiredModifier = 0x1F,
            OptionalModifier = 0x20,
            Internal = 0x21,
            Modifier = 0x40,
            Sentinel = 0x41,
            Pinned = 0x45,
            Type = 0x50,
            CustomAttribute = 0x51,
            Reserved = 0x52,
            Field = 0x53,
            Property = 0x54,
            Enum = 0x55,

            // custom types
            ByRefValueType = 0x1011,
            ByRefPtr = 0x0f11,

            JmpTable = 0x5252
        }

        public ElementType NestedType;
        public EType Type;
        public uint Token;

        public ElementType(EType type)
        {
            Type = type;
        }

        public ElementType(EType type, uint token)
        {
            Type = type;
            Token = token;
        }

        public ElementType(ElementType type)
        {
            Type = type.Type;
            Token = type.Token;
        }

        public ElementType(uint[] data, ref uint i)
        {
            Init(data, ref i);
        }

        public ElementType(uint[] data)
        {
            uint i = 0;
            Init(data, ref i);
        }

        private void Init(uint[] data, ref uint i)
        {
            Type = (EType)data[i++];

            if (Type == EType.Ptr || Type == EType.ByRef)
            {
                Token = data[i++];

                if (Token == (int)EType.ValueType)
                {
                    Type = (EType)((int)Type << 8) | EType.ValueType;
                    Token = CLIMetadata.TypeDefOrRefOrSpecEncoded(data[i++]);
                }
            }
            else if (Type == EType.ValueType || Type == EType.Class)
            {
                Token = CLIMetadata.TypeDefOrRefOrSpecEncoded(data[i++]);
            }
            else if (Type == EType.Var)
            {
                Token = data[i++];
            }
            else if (Type == EType.MVar)
            {
                Token = data[i++];
            }
            else if (Type == EType.MethodSignature)
            {
                throw new Exception("Unhandled");
            }
            else if (Type == EType.Array)
            {
                i += 2;  // type, rank
                uint boundsCount = data[i++];
                i += boundsCount;
                uint loCount = data[i++];
                i += loCount;
            }
            else if (Type == EType.GenericInst)
            {
                var type = new ElementType(data, ref i);
                uint typeArgCount = data[i++];
                i += typeArgCount;
            }
            else if (Type == EType.RequiredModifier || Type == EType.OptionalModifier)
            {
                Token = data[i++];
            }
            else if (Type == EType.SzArray)
            {
                NestedType = new ElementType(data, ref i);
            }
        }

        public override string ToString()
        {
            if (Token == 0) return Type.ToString();
            else return $"{Type} {Token.ToString("X")}";
        }

        public bool Is32BitCapable(CLIMetadata metadata)
        {
            if (Type == EType.ValueType)
            {
                var token = Token;
                while ((token & 0xff000000) == 0x02000000)
                {
                    var typeDef = metadata.TypeDefs[(int)(Token & 0x00ffffff) - 1];
                    if ((typeDef.typeDefOrRef & 0xff000000) == 0x02000000) token = typeDef.typeDefOrRef;
                    else
                    {
                        var typeRef = metadata.TypeRefs[(int)(typeDef.typeDefOrRef & 0x00ffffff) - 1];
                        if (typeRef.Name == "Enum" && typeRef.Namespace == "System") return true;   // enums can work like ints
                        else break;
                    }
                }
            }
            return (Type >= EType.Char && Type <= EType.U4);
        }

        public bool IsPointer()
        {
            var lowerByte = (EType)((uint)Type & 0xff);
            return (lowerByte == EType.ValueType || lowerByte == EType.Class || lowerByte == EType.Ptr || lowerByte == EType.Object || lowerByte == EType.ByRef);
        }
    }

    public class MethodRefSig
    {
        public SigFlags Flags { get; private set; }
        public uint ParamCount { get; private set; }
        public uint GenParamCount { get; private set; }
        public ElementType RetType { get; private set; }
        public ElementType[] Params { get; private set; }

        public MethodRefSig(CLIMetadata metadata, uint addr)
        {
            var blob = metadata.GetBlob(addr);
            var data = CLIMetadata.DecompressUnsignedSignature(blob, 1);    // first byte is the flags

            if (blob[0] == 0x06)
            {
                // this is just a field type?
                uint i = 0;
                RetType = new ElementType(data, ref i);
            }
            else if (blob[0] == 0x05)
            {
                throw new Exception("No support for VARARG (II.22.25) (intentional)");
            }
            else
            {
                Flags = (SigFlags)blob[0];

                uint i = 0;
                if ((Flags & SigFlags.GENERIC) != 0)
                    GenParamCount = data[i++];

                ParamCount = data[i++];
                
                RetType = new ElementType(data, ref i);

                if (ParamCount > 0)
                {
                    Params = new ElementType[ParamCount];
                    for (uint p = 0; p < ParamCount; p++)
                    {
                        Params[p] = new ElementType(data, ref i);
                    }
                }
            }
        }
    }

    public class MemberRefLayout
    {
        private uint classIndex;
        public uint nameAddr;
        public uint signature;
        public uint Parent { get; private set; }

        public string Name { get; private set; }
        public MethodRefSig MemberSignature { get; private set; }

        public MemberRefLayout(CLIMetadata metadata, ref int offset)
        {
            byte firstByte = metadata.Table.Heap[offset];

            uint tableSize = 0;
            uint maxTableSize = (1 << 13);

            switch (firstByte & 0x07)
            {
                case 0x00: tableSize = metadata.TableSizes[MetadataTable.TypeDef]; break;
                case 0x01: tableSize = metadata.TableSizes[MetadataTable.TypeRef]; break;
                case 0x02: tableSize = metadata.TableSizes[MetadataTable.ModuleRef]; break;
                case 0x03: tableSize = metadata.TableSizes[MetadataTable.MethodDef]; break;
                case 0x04: tableSize = metadata.TableSizes[MetadataTable.TypeSpec]; break;
                default: throw new Exception("Invalid table");
            }

            if (tableSize >= maxTableSize)
            {
                classIndex = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                classIndex = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            Parent = GetParentToken();

            if (metadata.WideStrings)
            {
                nameAddr = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                nameAddr = BitConverter.ToUInt16(metadata.Table.Heap, offset);
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

            Name = metadata.GetString(nameAddr);
            MemberSignature = new MethodRefSig(metadata, signature);
        }

        private string _parent = null;

        public uint GetParentToken()
        {
            uint addr = (classIndex >> 3);
            switch (classIndex & 0x07)
            {
                case 0x00: addr |= 0x02000000U; break;
                case 0x01: addr |= 0x01000000U; break;
                case 0x02: addr |= 0x0A000000U; break;
                case 0x03: addr |= 0x06000000U; break;
                case 0x04: addr |= 0x1B000000U; break;
            }
            return addr;
        }

        public void FindParentType(CLIMetadata metadata)
        {
            switch (classIndex & 0x07)
            {
                case 0x00: _parent = metadata.TypeDefs[(int)(classIndex >> 3) - 1].ToString(); break;
                case 0x01: _parent = metadata.TypeRefs[(int)(classIndex >> 3) - 1].ToString(); break;
                case 0x02: _parent = metadata.ModuleRefs[(int)(classIndex >> 3) - 1].ToString(); break;
                case 0x03: _parent = metadata.MethodDefs[(int)(classIndex >> 3) - 1].ToString(); break;
                case 0x04: _parent = metadata.TypeSpecs[(int)(classIndex >> 3) - 1].ToString(); break;
            }
        }

        public override string ToString()
        {
            if (_parent == null) return $"???.{Name}";
            else return $"{_parent}.{Name}";
        }

        public string ToAsmString()
        {
            StringBuilder sb = new StringBuilder(ToString());//.Replace(".", "_"));
            sb.Append($"_{MemberSignature.RetType.Type}");

            for (int i = 0; i < MemberSignature.ParamCount; i++)
            {
                if (MemberSignature.Params[i].Type != ElementType.EType.End)
                    sb.Append($"_{MemberSignature.Params[i].Type}");
            }

            return sb.ToString();
        }

        public string ToPrettyString()
        {
            StringBuilder sb = new StringBuilder();

            if (MemberSignature != null) sb.Append(MemberSignature.RetType.Type + " ");

            if (string.IsNullOrEmpty(_parent)) sb.Append("???");
            else sb.Append(_parent);
            sb.Append(".");

            sb.Append(Name);

            if (MemberSignature != null)
            {
                sb.Append("(");

                for (int i = 0; MemberSignature.Params != null && i < MemberSignature.Params.Length; i++)
                {
                    sb.Append(MemberSignature.Params[i].Type.ToString());
                    if (i < MemberSignature.Params.Length - 1) sb.Append(", ");
                }

                sb.Append(")");
            }

            return sb.ToString();
        }
    }
}
