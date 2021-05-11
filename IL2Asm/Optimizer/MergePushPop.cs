using System.Collections.Generic;

namespace IL2Asm.Optimizer
{
    public class MergePushPop
    {
        public static void ProcessAssembly(List<string> assembly)
        {
            for (int i = 0; i < assembly.Count - 1; i++)
            {
                var l1 = assembly[i].Trim();

                int offset = 1;
                while (i + offset < assembly.Count && assembly[i + offset].Trim().StartsWith(";")) offset++;
                var l2 = assembly[i + offset].Trim();

                if (l1.StartsWith("push") && l2.StartsWith("pop"))
                {
                    string dest = l2.Substring(4).Trim();
                    string src = l1.Substring(5).Trim();
                    string comment = "";

                    if (dest.Contains(";"))
                    {
                        comment = dest.Substring(dest.IndexOf(";"));
                        dest = dest.Substring(0, dest.IndexOf(";"));
                    }
                    if (src.Contains(";"))
                    {
                        comment += src.Substring(src.IndexOf(";"));
                        src = src.Substring(0, src.IndexOf(";"));
                    }

                    if (src == dest)
                    {
                        assembly.RemoveAt(i + offset);
                        assembly.RemoveAt(i);
                    }
                    else
                    {
                        assembly.RemoveAt(i + offset);
                        assembly[i] = $"    mov {dest}, {src}{comment}";
                    }
                }
            }
        }
    }
}
