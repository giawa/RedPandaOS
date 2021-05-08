using System.Collections.Generic;

namespace IL2Asm.Optimizer
{
    public class MergePushPopAcrossMov
    {
        private static char[] _split = new char[] { ' ', ';' };

        public static void ProcessAssembly(List<string> assembly)
        {
            for (int i = 0; i < assembly.Count - 2; i++)
            {
                var l1 = assembly[i].Trim().Split(_split);

                int offset1 = 1;
                while (i + offset1 < assembly.Count && assembly[i + offset1].Trim().StartsWith(";")) offset1++;
                var l2 = assembly[i + offset1].Trim().Split(_split);

                int offset2 = offset1 + 1;
                while (i + offset2 < assembly.Count && assembly[i + offset2].Trim().StartsWith(";")) offset2++;
                var l3 = assembly[i + offset2].Trim().Split(_split);

                if (i + offset2 >= assembly.Count || i + offset2 >= assembly.Count) break;

                if (l1[0] == "push" && l3[0] == "pop" && l2[0] == "mov")
                {
                    string src = l1[1];
                    string dest = l3[1];

                    // we can either optimize the mov to occur before the center mov, or after
                    if (l2[1].Trim(',') != l3[1] && l2[2] != l3[1])
                    {
                        // easy case, we can just put it before
                        assembly.RemoveAt(i + offset2);
                        
                        if (src == dest) assembly.RemoveAt(i);
                        else assembly[i] = $"    mov {dest}, {src}";
                    }
                    else if (l2[1].Trim(',') != src)
                    {
                        // src and dest can sometimes be equivalent, in this case the mov is redundant
                        if (src == dest) assembly.RemoveAt(i + offset2);
                        else assembly[i + offset2] = $"    mov {dest}, {src}";

                        // remove the push since we took care of the assignment already
                        assembly.RemoveAt(i);
                    }
                }
            }
        }
    }
}
