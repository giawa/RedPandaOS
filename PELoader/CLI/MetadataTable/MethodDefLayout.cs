using System;
using System.Collections.Generic;
using System.Text;

namespace PELoader
{
    [Flags]
    public enum MethodImplAttributes : ushort
    {
        CodeTypeMask = 0x0003,
        IL = 0x0000,
        Native = 0x0001,
        OPTIL = 0x0002,
        Runtime = 0x0003,

        ManagedMask = 0x0004,
        Unmanaged = 0x0004,
        Managed = 0x0000,

        ForwardRef = 0x0010,
        PreserveSig = 0x0080,
        InternalCall = 0x1000,
        Synchronized = 0x0020,
        NoInlining = 0x0008,
        MaxMethodImplVal = 0xffff,
        NoOptimization = 0x0040
    }

    [Flags]
    public enum MethodAttributes : ushort
    {
        MemberAccessMask = 0x0007,
        CompilerControlled = 0x0000,
        Private = 0x0001,
        FamANDAssem = 0x0002,
        Assem = 0x0003,
        Family = 0x0004,
        FamORAssem = 0x0005,
        Public = 0x0006,

        Static = 0x0010,
        Final = 0x0020,
        Virtual = 0x0040,
        HideBySig = 0x0080,
        
        VtableLayoutMask = 0x0100,
        ReuseSlot = 0x0000,
        NewSlot = 0x0100,

        Strict = 0x0200,
        Abstract = 0x0400,
        SpecialName = 0x0800,

        PInvokeImpl = 0x2000,
        UnmanagedExport = 0x0008,

        RTSpecialName = 0x1000,
        HasSecurity = 0x4000,
        RequireSecObject = 0x8000
    }

    public class MethodDefLayout : ICommonMethodInfo
    {
        public uint RVA { get; private set; }
        private uint name;
        private uint signature;
        internal uint paramList, endOfParamList;

        public string Name { get; private set; }

        public MethodImplAttributes ImplFlags { get; private set; }
        public MethodImplAttributes Flags { get; private set; }

        public TypeDefLayout Parent { get; internal set; }
        public MethodRefSig Signature { get; private set; }

        public List<ParamLayout> Params = new List<ParamLayout>();
        public List<CustomAttributeLayout> Attributes = new List<CustomAttributeLayout>();

        public string ParentName { get { return Parent.Name; } }

        public MethodDefLayout(CLIMetadata metadata, ref int offset)
        {
            RVA = BitConverter.ToUInt32(metadata.Table.Heap, offset);
            offset += 4;

            ImplFlags = (MethodImplAttributes)BitConverter.ToUInt16(metadata.Table.Heap, offset);
            offset += 2;

            Flags = (MethodImplAttributes)BitConverter.ToUInt16(metadata.Table.Heap, offset);
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

            if (metadata.TableSizes[MetadataTable.Param] > ushort.MaxValue)
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
            Signature = new MethodRefSig(metadata, signature);
        }

        public void FindParams(List<ParamLayout> paramLayouts)
        {
            if (endOfParamList == 0) endOfParamList = (uint)(paramLayouts.Count + 1);

            for (uint i = paramList; i < endOfParamList; i++)
            {
                Params.Add(paramLayouts[(int)i - 1]);
                paramLayouts[(int)i - 1].Parent = this;
            }
        }

        public override string ToString()
        {
            if (Parent != null) return $"{Parent.FullName}.{Name}";
            else return $"???.{Name}";
        }

        public string ToAsmString(GenericInstSig genericSig)
        {
            var asm = ToAsmString();

            if (asm.Contains("`1") && genericSig != null)
            {
                //if (genericSig == null) throw new Exception("Name looked generic but no generic sig was found");
                string genericName = genericSig.Params[0].Type.ToString();
                if (genericSig.Params[0].Type == ElementType.EType.ValueType)
                    genericName += genericSig.Params[0].Token.ToString();
                asm = asm.Replace("`1", $"_{genericName}");
            }
            else if (asm.Contains("`1"))
            {
                asm = asm.Replace("`1", $"_Var");
            }

            return asm;
        }

        public string ToAsmString()
        {
            StringBuilder sb = new StringBuilder(ToString().Replace(".", "_"));
            if (Signature.RetType.Type == ElementType.EType.SzArray)
            {
                sb.Append($"_@{Signature.RetType.NestedType}");
            }
            else sb.Append($"_{Signature.RetType.Type}");

            for (int i = 0; i < Signature.ParamCount; i++)
            {
                if (Signature.Params[i].Type != ElementType.EType.End)
                    sb.Append($"_{Signature.Params[i].Type}");
            }

            return sb.ToString();
        }

        public bool IsEquivalent(MemberRefLayout memberRef, TypeSpecLayout typeSpec)
        {
            if (typeSpec == null || memberRef == null) return false;
            if (Parent.Token != typeSpec.Parent || Name != memberRef.Name) return false;
            return memberRef.Signature.IsEquivalent(Signature);
        }

        private MethodDefLayout() { }

        public MethodDefLayout Clone(CLIMetadata metadata)
        {
            MethodDefLayout methodDef = new MethodDefLayout();

            methodDef.RVA = this.RVA;
            methodDef.name = this.name;
            methodDef.signature = this.signature;
            methodDef.paramList = this.paramList;
            methodDef.endOfParamList = this.endOfParamList;
            methodDef.Name = this.Name;
            methodDef.ImplFlags = this.ImplFlags;
            methodDef.Flags = this.Flags;
            methodDef.Parent = this.Parent;

            methodDef.Params = new List<ParamLayout>(this.Params);
            methodDef.Attributes = new List<CustomAttributeLayout>(this.Attributes);

            methodDef.Signature = new MethodRefSig(metadata, signature);

            return methodDef;
        }
    }
}
