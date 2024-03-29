﻿using System;
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
                NestedType = new ElementType(data, ref i);
                uint typeArgCount = data[i++];
                var genericTypes = new ElementType[typeArgCount];
                for (int j = 0; j < typeArgCount; j++)
                    genericTypes[j] = new ElementType(data, ref i);
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
                        else if (typeRef.Name == "ValueType" && typeDef.Fields.Count == 1) return typeDef.Fields[0].Type.Is32BitCapable(metadata);
                        else break;
                    }
                }
            }
            return (Type >= EType.Boolean && Type <= EType.U4) || IsPointer() || Type == EType.SzArray;
        }

        public bool IsPointer()
        {
            if (Type == EType.ByRefValueType || Type == EType.ByRefPtr) return true;
            var lowerByte = (EType)((uint)Type & 0xff);
            return (lowerByte == EType.ByRefValueType || lowerByte == EType.Class || lowerByte == EType.Ptr ||
                lowerByte == EType.Object || lowerByte == EType.ByRef || lowerByte == EType.GenericInst || lowerByte == EType.String);
        }

        public static bool operator ==(ElementType e1, ElementType e2)
        {
            if (object.ReferenceEquals(e1, null) || object.ReferenceEquals(e2, null)) return false;

            if (e1.Type != e2.Type) return false;
            if (e1.Token != e2.Token) return false;
            if (e1.Type == EType.GenericInst && e2.Type == EType.GenericInst) return true;  // TODO: This is a total hack for now
            if (!object.ReferenceEquals(e1.NestedType, null)) return e1.NestedType == e2.NestedType || e1.NestedType.Type == EType.MVar || e2.NestedType.Type == EType.MVar;

            return true;
        }

        public static bool operator !=(ElementType e1, ElementType e2)
        {
            return !(e1 == e2);
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class MethodRefSig
    {
        public SigFlags Flags { get; private set; }
        public uint ParamCount { get; private set; }
        public uint GenParamCount { get; private set; }
        public ElementType RetType { get; private set; }
        public ElementType[] Params { get; private set; }

        public MethodRefSig(byte[] data, int offset)
        {
            ParseSignature(data, offset);
        }

        public MethodRefSig(CLIMetadata metadata, uint addr)
        {
            var blob = metadata.GetBlob(addr);

            ParseSignature(blob, 0);
        }

        private void ParseSignature(byte[] blob, int offset)
        {
            var data = CLIMetadata.DecompressUnsignedSignature(blob, offset + 1);    // first byte is the flags

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

        public bool IsEquivalent(MethodRefSig other)
        {
            if (other.Flags != Flags) return false;
            if (other.GenParamCount != GenParamCount) return false;
            if (other.ParamCount != ParamCount) return false;
            if (other.RetType != RetType) return false;
            for (int i = 0; i < ParamCount; i++)
                if (other.Params[i] != Params[i]) return false;
            return true;
        }

        public void Override(GenericInstSig sig)
        {
            if (sig.Params.Length == 0) return;

            if (RetType.Type == ElementType.EType.Var || RetType.Type == ElementType.EType.MVar)
            {
                RetType = sig.Params[0];
                return;
            }

            for (int i = 0; i < ParamCount; i++)
            {
                if (Params[i].Type == ElementType.EType.Var || Params[i].Type == ElementType.EType.MVar)
                {
                    Params[i] = sig.Params[0];
                    return;
                }
            }
        }
    }

    public interface ICommonMethodInfo
    {
        string Name { get; }
        string ToAsmString();
        MethodRefSig Signature { get; }
        string ParentName { get; }
    }

    public class MemberRefLayout : ICommonMethodInfo
    {
        private uint classIndex;
        public uint nameAddr;
        public uint signature;
        public uint Parent { get; private set; }
        public string ParentName { get { return _parent; } }

        public string Name { get; private set; }
        public MethodRefSig Signature { get; private set; }

        public MemberRefLayout(CLIMetadata metadata, ref int offset)
        {
            uint tableSize = metadata.MemberRefParentCount;
            uint maxTableSize = (1 << 13);

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
            Signature = new MethodRefSig(metadata, signature);
        }

        private string _parent = null;

        private uint GetParentToken()
        {
            uint addr = (classIndex >> 3);
            switch (classIndex & 0x07)
            {
                case 0x00: addr |= 0x02000000U; break;
                case 0x01: addr |= 0x01000000U; break;
                case 0x02: addr |= 0x0A000000U; break;
                case 0x03: addr |= 0x06000000U; break;
                case 0x04: addr |= 0x1B000000U; break;
                default: throw new Exception("Unsupported classIndex");
            }
            return addr;
        }

        public void FindParentType(CLIMetadata metadata)
        {
            uint addr = GetParentToken();

            switch (addr & 0xff000000)
            {
                case 0x02000000: _parent = metadata.TypeDefs[(int)(addr & 0x00ffffff) - 1].ToString(); break;
                case 0x01000000: _parent = metadata.TypeRefs[(int)(addr & 0x00ffffff) - 1].ToString(); break;
                case 0x00000000: _parent = metadata.Modules[(int)(addr & 0x00ffffff) - 1].ToString(); break;
                case 0x06000000: _parent = metadata.MethodDefs[(int)(addr & 0x00ffffff) - 1].ToString(); break;
                case 0x1B000000: _parent = metadata.TypeSpecs[(int)(addr & 0x00ffffff) - 1].ToString(); break;
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
            sb.Append($"_{Signature.RetType.Type}");

            for (int i = 0; i < Signature.ParamCount; i++)
            {
                if (Signature.Params[i].Type != ElementType.EType.End)
                    sb.Append($"_{Signature.Params[i].Type}");
            }

            return sb.ToString();
        }

        public string ToPrettyString()
        {
            StringBuilder sb = new StringBuilder();

            if (Signature != null) sb.Append(Signature.RetType.Type + " ");

            if (string.IsNullOrEmpty(_parent)) sb.Append("???");
            else sb.Append(_parent);
            sb.Append(".");

            sb.Append(Name);

            if (Signature != null)
            {
                sb.Append("(");

                for (int i = 0; Signature.Params != null && i < Signature.Params.Length; i++)
                {
                    sb.Append(Signature.Params[i].Type.ToString());
                    if (i < Signature.Params.Length - 1) sb.Append(", ");
                }

                sb.Append(")");
            }

            return sb.ToString();
        }

        private MemberRefLayout(CLIMetadata metadata, MemberRefLayout original)
        {
            _parent = original._parent;
            classIndex = original.classIndex;
            nameAddr = original.nameAddr;
            signature = original.signature;
            Parent = original.Parent;
            Name = original.Name;
            Signature = new MethodRefSig(metadata, signature);
        }

        public MemberRefLayout Clone(CLIMetadata metadata)
        {
            return new MemberRefLayout(metadata, this);
        }
    }
}
