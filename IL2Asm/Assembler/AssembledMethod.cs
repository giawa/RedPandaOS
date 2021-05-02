using PELoader;
using System.Collections.Generic;

namespace IL2Asm
{
    public class AssembledMethod
    {
        public MethodHeader Method { get; private set; }
        public CLIMetadata Metadata { get; private set; }

        public List<string> Assembly { get; private set; }

        public AssembledMethod(CLIMetadata metadata, MethodHeader method)
        {
            Method = method;
            Metadata = metadata;

            Assembly = new List<string>();
        }

        public void AddAsm(string asm)
        {
            Assembly.Add(asm);
        }

        public override string ToString()
        {
            return Method?.MethodDef?.ToAsmString() ?? "Unknown Method";
        }
    }
}
