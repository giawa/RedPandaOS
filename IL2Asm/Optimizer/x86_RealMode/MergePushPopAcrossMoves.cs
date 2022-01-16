using System.Collections.Generic;

namespace IL2Asm.Optimizer.x86_RealMode
{
    public class MergePushPopAcrossMoves
    {
        private static char[] _split = new char[] { ' ' };

        public static void ProcessAssembly(List<string> assembly)
        {
            for (int i = 0; i < assembly.Count; i++)
            {
                var instruction = assembly[i].Trim();
                if (instruction == "" || !instruction.Contains(" ") || instruction.StartsWith(";")) continue;
                string register = instruction.Split(' ')[1].Trim().Trim(',');

                if (instruction.StartsWith("push"))
                {
                    for (int j = i + 1; j < assembly.Count; j++)
                    {
                        var nextInstruction = assembly[j].Trim();
                        if (nextInstruction.StartsWith(";")) continue;

                        if (nextInstruction.StartsWith("mov"))
                        {
                            // strip out any comments
                            if (nextInstruction.Contains(";"))
                                nextInstruction = nextInstruction.Substring(0, nextInstruction.IndexOf(";"));

                            // need to capture ah affecting ax, etc - so use first letter
                            if (nextInstruction.Split(' ')[1].Trim().StartsWith(register[0])) break;
                        }
                        else if (nextInstruction.StartsWith("pop"))
                        {
                            string newRegister = nextInstruction.Split(' ')[1].Trim().Trim(',');

                            if (newRegister == register)
                            {
                                assembly[i] = ";" + assembly[i];
                                assembly[j] = ";" + assembly[j];
                            }
                            else
                            {
                                assembly[i] = ";" + assembly[i];
                                assembly[j] = $"    mov {newRegister}, {register} ; {assembly[j]}";
                            }
                        }
                        else break;
                    }
                }
            }
        }
    }
}
