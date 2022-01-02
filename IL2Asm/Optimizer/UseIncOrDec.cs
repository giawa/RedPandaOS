using System.Collections.Generic;

namespace IL2Asm.Optimizer
{
    public class UseIncOrDec
    {
        public static void ProcessAssembly(List<string> assembly)
        {
            for (int i = 0; i < assembly.Count - 1; i++)
            {
                var l1 = assembly[i].Trim();

                if (l1 == "push 1")
                {
                    string type = "inc";

                    int j = i + 1;
                    if (!CompareAsm(NextLine(assembly, ref j), "mov ebx, [esp]")) continue;
                    if (!CompareAsm(NextLine(assembly, ref j), "mov eax, [esp + 4]")) continue;
                    //if (CompareAsm(NextLine(assembly, ref j), "add eax, ebx")) type = "inc";
                    var instruction = NextLine(assembly, ref j);
                    if (instruction == "add eax, ebx") type = "inc";
                    else if (instruction == "sub eax, ebx") type = "dec";
                    else continue;
                    if (!CompareAsm(NextLine(assembly, ref j), "pop ebx")) continue;
                    if (!CompareAsm(NextLine(assembly, ref j), "mov [esp], eax")) continue;

                    for (int k = i; k < j - 3; k++) assembly.RemoveAt(i);
                    assembly[i] = "    pop eax";
                    assembly[i + 1] = $"    {type} eax";
                    assembly[i + 2] = "    push eax";
                }
            }
        }

        private static string NextLine(List<string> assembly, ref int j)
        {
            while (j < assembly.Count && assembly[j].Trim().StartsWith(";")) j++;
            return (j >= assembly.Count ? null : assembly[j++].Trim());
        }

        private static bool CompareAsm(string asm, string comparison)
        {
            if (string.IsNullOrEmpty(asm)) return false;
            return asm == comparison;
        }
    }
}
