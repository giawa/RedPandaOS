using System.Collections.Generic;

namespace IL2Asm.Optimizer
{
    class MergePushPop
    {
        private static char[] _split = new char[] { ' ', ';' };

        public static void ProcessAssembly(List<string> assembly)
        {
            bool foundMerges = false;
            do
            {
                foundMerges = false;

                for (int i = 0; i < assembly.Count - 1; i++)
                {
                    var l1 = assembly[i].Trim().Split(_split);
                    var l2 = assembly[i + 1].Trim().Split(_split);

                    if (l1[0] == "push" && l2[0] == "pop")
                    {
                        if (l1[1] == l2[1])
                        {
                            assembly.RemoveAt(i);
                            assembly.RemoveAt(i);
                            foundMerges = true;
                        }
                        else
                        {
                            assembly.RemoveAt(i + 1);
                            assembly[i] = $"    mov {l2[1]}, {l1[1]}";
                            foundMerges = true;
                        }
                    }
                }

            } while (foundMerges);
        }
    }
}
