using System.Collections.Generic;

namespace IL2Asm.Optimizer.x86
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
            public string Constant;

            public MovInstruction()
            {
                Constant = null;
            }

            public void Reset()
            {
                Constant = null;
            }
        }

        public static void ProcessAssembly(List<string> assembly)
        {
            Dictionary<string, MovInstruction> registers = new Dictionary<string, MovInstruction>();
            registers.Add("eax", new MovInstruction());
            registers.Add("ebx", new MovInstruction());
            registers.Add("ecx", new MovInstruction());
            registers.Add("edx", new MovInstruction());
            registers.Add("esi", new MovInstruction());
            registers.Add("edi", new MovInstruction());
            //registers.Add("bp", new MovInstruction());

            for (int i = 0; i < assembly.Count; i++)
            {
                var instruction = assembly[i].Trim();
                if (instruction.StartsWith(";")) continue;

                // any time we could have jumped here then reset the register state
                if (instruction.EndsWith(":") || instruction.StartsWith("j") || instruction.StartsWith("call") || instruction.StartsWith("int"))
                {
                    foreach (var register in registers) register.Value.Reset();
                    continue;
                }
                // shift (sh* and sa*) instructions clobber cx
                if (instruction.StartsWith("s"))
                {
                    registers["ecx"].Reset();
                    foreach (var reg in registers) if (reg.Value.Constant == "ecx") reg.Value.Reset();
                }

                // remove any commented out portion
                if (instruction.Contains(";"))
                    instruction = instruction.Substring(0, instruction.IndexOf(';'));

                var split = instruction.Split(_split);
                for (int j = 0; j < split.Length; j++) split[j] = split[j].Trim(',');

                if (split[0] == "mov")
                {
                    if (registers.ContainsKey(split[1]))
                    {
                        if (int.TryParse(split[2], out int constant))
                        {
                            registers[split[1]].Constant = split[2];
                            registers[split[1]].Line = i;

                            continue;
                        }
                        else if (split[2] == "ecx" || split[2] == "edx" || split[2] == "edi" || split[2] == "esi" || split[2] == "ebx")
                        {
                            registers[split[1]].Constant = split[2];
                            registers[split[1]].Line = i;

                            continue;
                        }
                        else registers[split[1]].Reset();
                    }
                }

                if (split[0] == "pop" || split[0] == "inc" || split[0] == "dec")
                {
                    if (split[1] == "ebp") continue;
                    if (split[1] == "esp") continue;

                    string reg = split[1];
                    //if (reg.StartsWith("e")) reg = reg.Substring(1);

                    registers[reg].Reset();
                    foreach (var register in registers)
                        if (register.Value.Constant == reg)
                            register.Value.Reset();
                }
                else if (split[0] == "mul")
                {
                    // mul uses ax and also overwrites dx (dx is taken care of with push/pop in IL2Asm)
                    registers["eax"].Reset();
                    foreach (var register in registers)
                        if (register.Value.Constant == "eax")
                            register.Value.Reset();
                }
                else
                {
                    bool madeChanges = false;
                    int start = 2;
                    if (instruction.StartsWith("cmp"))
                        continue;// start = 1;
                    if (instruction.StartsWith("lea")) continue;
                    for (int j = start; j < split.Length; j++)
                    {
                        if (registers.TryGetValue(split[j], out var reg) && reg.Constant != null)
                        {
                            split[j] = reg.Constant.ToString();
                            reg.Reset();
                            madeChanges = true;

                            assembly[reg.Line] = ";" + assembly[reg.Line];
                        }
                    }

                    if (madeChanges)
                    {
                        string result = string.Format($"    {split[0]} {split[1]}");
                        if (split[1].StartsWith("[") && (split[2] == "-" || split[2] == "+"))
                            result = $"    {split[0]} dword {split[1]} {split[2]} {split[3]}, {split[4]}";
                        else if (split[1].StartsWith("[") && !split[2].EndsWith("x"))
                            result = $"    {split[0]} dword {split[1]}, {split[2]}";
                        else if (split.Length == 3) result += ", " + split[2] + " ; " + assembly[i];
                        assembly[i] = result;
                    }
                }

                if (aluInstructions.Contains(split[0]))
                {
                    registers["eax"].Reset();
                }
            }
        }
    }
}