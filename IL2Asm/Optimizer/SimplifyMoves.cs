using System.Collections.Generic;
using System.Linq;

namespace IL2Asm.Optimizer
{
    public class SimplifyMoves
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
            for (int i = 0; i < assembly.Count - 1; i++)
            {
                var line = assembly[i].Trim().Split(_split);

                if (line[0] != "mov") continue;

                var dest = line[1].Trim(',');
                var src = line[line.Length - 1].Trim();

                if (dest != "ax" && dest != "bx" && dest != "cx" && dest != "dx" && !dest.StartsWith("[bx")) continue; // deal with simple registers for now
                if (src.EndsWith("]")) continue; // too complicated for now

                bool canSimplify = true;
                int j;

                for (j = i + 1; j < assembly.Count; j++)
                {
                    var l = assembly[j].Trim().Split(_split);

                    // if we hit a jmp instruction before overwriting the src then we can't optimize this,
                    // since it may be used on the other end of the jmp
                    if (l[0].StartsWith("j") && jumpInstructions.Select(i => i == l[0]).Any())
                    {
                        canSimplify = false;
                        break;
                    }
                    if (l[0].EndsWith(":"))
                    {
                        canSimplify = false;
                        break;
                    }
                    if (l[0] == "call" && (src == "dx" || src == "cx"))
                    {
                        canSimplify = false;
                        break;
                    }

                    if (l[0] == "mul" && dest == l[1])
                    {
                        canSimplify = false;
                        break;
                    }
                    if (l[0] == "mul" && dest == "dx")
                    {
                        canSimplify = false;
                        break;
                    }
                    if ((l[0] == "add" || l[0] == "mul" || l[0] == "sub") && dest == "ax")
                    {
                        canSimplify = false;
                        break;
                    }
                    if (l[0] == "pop" && src == l[1])
                    {
                        canSimplify = false;
                        break;
                    }

                    if (l[0] != "mov") continue;

                    var newdest = l[1].Trim(',');
                    if (newdest == src)
                    {
                        canSimplify = false;
                        break;
                    }
                    if (newdest == dest) break;   // found a match
                }

                if (canSimplify)
                {
                    int optimizations = 0;

                    for (int k = i + 1; k <= j; k++)
                    {
                        var l = assembly[k].Trim().Split(_split);

                        if (l.Length > 2 && l[l.Length - 1] == dest)
                        {
                            string fullName = l[1];
                            if (fullName.StartsWith("["))
                                for (int m = 2; m < l.Length && !fullName.EndsWith("],"); m++)
                                    fullName += l[m];
                            //assembly[k] += "; could optimize to " + $"{l[0]} {fullName} {src}";
                            if (l[0] == "mov") assembly[k] = $"    mov word {fullName} {src} ;" + assembly[k];
                            else assembly[k] = $"    {l[0]} {fullName} {src} ;" + assembly[k].TrimStart();
                            optimizations++;
                        }
                        else if (l.Length > 1 && l[l.Length - 1] == dest)
                        {
                            //assembly[k] += "; could optimize to " + $"{l[0]} {src}";
                            assembly[k] = $"    {l[0]} {src}; " + assembly[k].TrimStart();
                            optimizations++;
                        }
                    }

                    //if (optimizations == 0) assembly[i] += "; could be removed as this mov is redundant";
                    //else assembly[i] += "; could remove assuming below optimizations";
                    assembly[i] = ";   " + assembly[i].TrimStart();
                }
            }
        }
    }
}
