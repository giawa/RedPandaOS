using System.Collections.Generic;

namespace IL2Asm.Optimizer.x86_RealMode
{
    public class SimplifyConstants
    {
        private static char[] _split = new char[] { ' ', ';' };

        private static List<string> aluInstructions = new List<string>(
            new string[] { "add", "sub", "and", "or", "xor", "not", "neg" }
            );

        public class MovInstruction
        {
            public int Line;
            public int Constant;

            public MovInstruction()
            {
                Constant = int.MaxValue;
            }

            public void Reset()
            {
                Constant = int.MaxValue;
            }
        }

        public static void ProcessAssembly(List<string> assembly)
        {
            Dictionary<string, MovInstruction> registers = new Dictionary<string, MovInstruction>();
            registers.Add("ax", new MovInstruction());
            registers.Add("bx", new MovInstruction());
            registers.Add("cx", new MovInstruction());
            registers.Add("dx", new MovInstruction());
            registers.Add("es", new MovInstruction());
            registers.Add("ed", new MovInstruction());
            registers.Add("bp", new MovInstruction());

            for (int i = 0; i < assembly.Count; i++)
            {
                var instruction = assembly[i].Trim();

                // any time we could have jumped here then reset the register state
                if (instruction.EndsWith(":") /*|| instruction.StartsWith("j")*/ || instruction.StartsWith("call"))
                {
                    foreach (var register in registers) register.Value.Reset();
                    continue;
                }

                var split = instruction.Split(_split);
                for (int j = 0; j < split.Length; j++) split[j] = split[j].Trim(',');

                if (split[0] == "mov")
                {
                    if (registers.ContainsKey(split[1]))
                    {
                        if (int.TryParse(split[2], out int constant))
                        {
                            registers[split[1]].Constant = constant;
                            registers[split[1]].Line = i;

                            continue;
                        }
                        else registers[split[1]].Reset();
                    }
                }
                
                if (split[0] == "pop")
                {
                    registers[split[1]].Reset();
                }
                else
                {
                    bool madeChanges = false;
                    for (int j = 1; j < split.Length; j++)
                    {
                        if (registers.TryGetValue(split[j], out var reg) && reg.Constant != int.MaxValue)
                        {
                            split[j] = reg.Constant.ToString();
                            madeChanges = true;

                            assembly[reg.Line] = "; " + assembly[reg.Line];
                        }
                    }
                    
                    if (madeChanges)
                    {
                        string result = string.Format($"    {split[0]} {split[1]}");
                        if (split.Length == 3) result += ", " + split[2] + " ; " + assembly[i];
                        assembly[i] = result;
                    }
                }
                
                if (aluInstructions.Contains(split[0]))
                {
                    registers["ax"].Reset();
                }
            }
        }
    }
}