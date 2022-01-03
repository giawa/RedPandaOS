using System.Collections.Generic;

namespace IL2Asm.Optimizer
{
    public class RemoveDuplicateInstructions
    {
        private static List<string> instructions = new List<string>(new string[] { "mov", "and", "or", "xor", "cmp" });

        public static void ProcessAssembly(List<string> assembly)
        {
            for (int i = 0; i < assembly.Count - 1; i++)
            {
                var l1 = assembly[i].Trim();
                if (!l1.Contains(' ') || l1.StartsWith(";") || l1.EndsWith(":")) continue;
                if (!instructions.Contains(l1.Split()[0])) continue;

                int temp = i + 1;
                if (CompareAsm(l1, NextLine(assembly, ref temp)))
                {
                    assembly[i] = ";" + assembly[i].Substring(1);
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
