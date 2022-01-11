using System.Collections.Generic;

namespace IL2Asm.Optimizer.x86_RealMode
{
    public class ReplaceEquivalentInstructions
    {
        public static void ProcessAssembly(List<string> assembly)
        {
            for (int i = 0; i < assembly.Count - 1; i++)
            {
                var instruction = assembly[i].Trim();
                if (instruction.StartsWith(";")) continue;

                if (instruction.StartsWith("mov ax, 0")) assembly[i] = "    xor ax, ax ;" + assembly[i];
                if (instruction.StartsWith("mov bx, 0")) assembly[i] = "    xor bx, bx ;" + assembly[i];
                if (instruction.StartsWith("mov cx, 0")) assembly[i] = "    xor cx, cx ;" + assembly[i];
                if (instruction.StartsWith("mov dx, 0")) assembly[i] = "    xor dx, dx ;" + assembly[i];
                if (instruction.StartsWith("mov si, 0")) assembly[i] = "    xor si, si ;" + assembly[i];
                if (instruction.StartsWith("mov di, 0")) assembly[i] = "    xor di, di ;" + assembly[i];
                if (instruction.StartsWith("add ax, 1 ") || instruction == "add ax, 1") assembly[i] = "    inc ax ;" + assembly[i];
            }
        }
    }
}