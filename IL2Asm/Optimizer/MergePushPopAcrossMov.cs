using System.Collections.Generic;

namespace IL2Asm.Optimizer
{
    class MergePushPopAcrossMov
    {
        private static char[] _split = new char[] { ' ', ';' };

        public static void ProcessAssembly(List<string> assembly)
        {
            for (int i = 0; i < assembly.Count - 2; i++)
            {
                var l1 = assembly[i].Trim().Split(_split);
                var l2 = assembly[i + 1].Trim().Split(_split);
                var l3 = assembly[i + 2].Trim().Split(_split);

                if (l1[0] == "push" && l3[0] == "pop" && l2[0] == "mov")
                {
                    string src = l1[1];
                    string dest = l3[1];

                    // make sure the dest isn't the target of the mov (should never happen?)
                    if (l2[1] == dest) continue;

                    assembly[i] = $"    mov {dest}, {src}";
                    assembly.RemoveAt(i + 2);

                    // src and dest can sometimems be equivalent, in this case the mov is redundant
                    if (src == dest) assembly.RemoveAt(i);
                }
            }
        }
    }
}
