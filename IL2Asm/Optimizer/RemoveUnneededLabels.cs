using System.Collections.Generic;
using System.Linq;

namespace IL2Asm.Optimizer
{
    public static class RemoveUnneededLabels
    {
        private static string[] jumpInstructions = new string[] 
        { 
            "jmp",
            "je", "jz", "jne", "jnz", "jg", "jnle", "jge", "jnl", "jl", "jnge", "jle", "jng",
            "ja", "jnbe", "jae", "jnb", "jb", "jnae", "jbe", "jna",
            "jxcz", "jc", "jnc", "jo", "jno", "jp", "jpe", "jnp", "jpo", "js", "jns"
        };

        private static char[] _split = new char[] { ' ', ';' };

        public static void ProcessAssembly(List<string> assembly)
        {
            List<string> usedLabels = new List<string>();

            foreach (var line in assembly)
            {
                string l = line.Trim();

                if (l.StartsWith("call"))
                {
                    var split = l.Split(_split);
                    usedLabels.Add(split[1]);
                }
                else if (l.StartsWith("j"))
                {
                    var split = l.Split(_split);
                    if (jumpInstructions.Select(i => i == split[0]).Any())
                    {
                        usedLabels.Add(split[1]);
                    }
                }
                else if (l.StartsWith("dd IL"))
                {
                    // jump table
                    var split = l.Split(_split);
                    for (int i = 1; i < split.Length; i++)
                        usedLabels.Add(split[i].Trim(','));
                }
            }

            for (int i = assembly.Count - 1; i >= 0; i--)
            {
                string l = assembly[i];
                if (l.StartsWith("IL_") && l.EndsWith(":"))
                {
                    string label = l.Substring(0, l.Length - 1);
                    if (usedLabels.Contains(label)) continue;
                    else assembly.RemoveAt(i);
                }
            }
        }
    }
}
