using System.Collections.Generic;

namespace IL2Asm.Optimizer
{
    public class MergePushPop
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

                    int offset = 1;
                    while (i + offset < assembly.Count && assembly[i + offset].Trim().StartsWith(";")) offset++;
                    var l2 = assembly[i + offset].Trim().Split(_split);

                    if (l1[0] == "push" && l2[0] == "pop")
                    {
                        if (l1[1] == l2[1])
                        {
                            assembly.RemoveAt(i + offset);
                            assembly.RemoveAt(i);
                            foundMerges = true;
                        }
                        else
                        {
                            assembly.RemoveAt(i + offset);
                            assembly[i] = $"    mov {l2[1]}, {l1[1]}";
                            foundMerges = true;
                        }
                    }
                }

            } while (foundMerges);
        }
    }
}
