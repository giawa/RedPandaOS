using PELoader;
using System;
using System.Collections.Generic;

namespace IL2Asm
{
    public class AssembledMethod
    {
        public MethodHeader Method { get; private set; }
        public MethodSpecLayout MethodSpec { get; private set; }
        public CLIMetadata Metadata { get; private set; }

        public List<string> Assembly { get; private set; }

        public AssembledMethod(CLIMetadata metadata, MethodHeader method, MethodSpecLayout methodSpec)
        {
            Method = method;
            Metadata = metadata;
            MethodSpec = methodSpec;

            Assembly = new List<string>();
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

        public override string ToString()
        {
            return Method?.MethodDef?.ToAsmString() ?? "Unknown Method";
        }
    }
}
