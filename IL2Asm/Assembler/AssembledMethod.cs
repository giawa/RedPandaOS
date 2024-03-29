﻿using PELoader;
using System;
using System.Collections.Generic;

namespace IL2Asm
{
    public class AssembledMethod : BaseTypes.IAssembledMethod
    {
        public MethodHeader Method { get; private set; }
        public MethodSpecLayout MethodSpec { get; private set; }
        public CLIMetadata Metadata { get; private set; }
        public GenericInstSig GenericInstSig { get; private set; }
        public int MethodCounter { get; set; }
        public bool HasStackFrame { get; set; } = false;
        public string HeapAllocatorMethod { get; set; }
        public string ThrowExceptionMethod { get; set; }
        public string AsmName { get; set; }

        public List<string> Assembly { get; private set; } = new List<string>();

        public AssembledMethod(CLIMetadata metadata, MethodHeader method, MethodSpecLayout methodSpec = null, GenericInstSig genericSig = null)
        {
            Method = method;
            Metadata = metadata;
            MethodSpec = methodSpec;
            GenericInstSig = genericSig;
            AsmName = method.MethodDef.ToAsmString(genericSig);
        }

        public AssembledMethod(CLIMetadata metadata, string name)
        {
            Metadata = metadata;
            AsmName = name;
        }

        public void AddAsm(string asm)
        {
            Assembly.Add(asm);
        }

        public string ToAsmString()
        {
            if (Method == null || Method.MethodDef == null) return "Unknown Method";

            var asmName = Method.MethodDef.ToAsmString();

            if (asmName.Contains("MVar"))
            {
                if (MethodSpec.MemberSignature.Types.Length != 1)
                    throw new Exception("Multiple generics are not yet supported");

                asmName = asmName.Replace("MVar", MethodSpec.MemberSignature.TypeNames[0]);
            }

            return asmName;
        }

        public string ToAsmString(GenericInstSig genericSig)
        {
            if (Method == null || Method.MethodDef == null) return "Unknown Method";

            var asmName = Method.MethodDef.ToAsmString(genericSig);

            if (asmName.Contains("MVar") && MethodSpec != null)
            {
                if (MethodSpec.MemberSignature.Types.Length != 1)
                    throw new Exception("Multiple generics are not yet supported");

                asmName = asmName.Replace("MVar", MethodSpec.MemberSignature.TypeNames[0]);
            }

            return asmName;
        }

        public override string ToString()
        {
            return Method?.MethodDef?.ToAsmString() ?? "Unknown Method";
        }

        private static int _uniqueLabelCounter = 0;

        public string GetUniqueLabel(string name)
        {
            return $"{name.ToUpperInvariant()}_{_uniqueLabelCounter++}";
        }
    }
}
