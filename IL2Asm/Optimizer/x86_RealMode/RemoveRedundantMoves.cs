using System.Collections.Generic;

namespace IL2Asm.Optimizer.x86_RealMode
{
    public class RemoveRedundantMoves
    {
        private static char[] _split = new char[] { ' ', ';' };

        public static void ProcessAssembly(List<string> assembly)
        {
            for (int i = 0; i < assembly.Count - 1; i++)
            {
                var instruction = assembly[i].Trim();
                int j = i + 1;
                var nextInstruction = assembly[j++].Trim();
                while (nextInstruction.StartsWith(";")) nextInstruction = assembly[j++].Trim();

                if (nextInstruction.StartsWith("mov bx, ax"))
                {
                    if (instruction.StartsWith("mov ax,"))
                    {
                        assembly[i] = "    mov bx," + instruction.Substring(7);
                        assembly[i + 1] = ";" + assembly[i + 1];
                    }
                }
                else if (nextInstruction.StartsWith("cmp bx,"))
                {
                    if (instruction.StartsWith("mov bx,") && instruction.EndsWith("x"))
                    {
                        assembly[i] = ";" + assembly[i];
                        string rightSide = nextInstruction.Substring(nextInstruction.IndexOf(',') + 1);
                        assembly[j - 1] = $"    cmp {instruction.Substring(instruction.Length - 2)}, {rightSide}";
                    }
                }
            }
        }
    }
}