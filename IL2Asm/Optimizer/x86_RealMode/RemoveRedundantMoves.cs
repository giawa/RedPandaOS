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
                var nextInstruction = assembly[i + 1].Trim();

                if (nextInstruction.StartsWith("mov bx, ax"))
                {
                    if (instruction.StartsWith("mov ax,"))
                    {
                        assembly[i] = "    mov bx," + instruction.Substring(7);
                        assembly[i + 1] = ";" + assembly[i + 1];
                    }
                }
            }
        }
    }
}