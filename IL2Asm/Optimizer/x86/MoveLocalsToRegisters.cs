using System;
using System.Collections.Generic;

namespace IL2Asm.Optimizer.x86
{
    /// <summary>
    /// This method will pack the most used locals (defined by [ebp - X*4]) into hardware registers where available
    /// Note:  This method is dumb, and does not figure out the scope of the local variables.  It does this subsitution
    /// across full methods.  This means that we need to protected across far jumps or calls against our registers being
    /// modified.  Right now we protect across calls only.
    /// 
    /// A better method would be to scan the method and find the scope of each local.  Then assign registers across
    /// the scope of each local, which will likely clean up a lot of the issues across call sites.  Also these is no point
    /// protected across a call if the local is not used before (or after) the call.  It only matters if a local straddles
    /// the call.
    /// </summary>
    public class MoveLocalsToRegisters
    {
        public static void ProcessAssembly(List<string> assembly)
        {
            for (int i = 0; i < assembly.Count; i++)
            {
                ProcessMethod(assembly, ref i);
            }
        }

        private static void ProcessMethod(List<string> assembly, ref int i)
        {
            // never use eax and ebx since we use them so much
            List<string> availableRegisters = new List<string>(new string[] { "ecx", "edx", "edi", "esi" });

            // support localvars in the first 10 slots after ebp for now
            List<StackVariable> locals = new List<StackVariable>();
            for (int j = 0; j < 10; j++) locals.Add(new StackVariable()
            {
                Name = $"[ebp - {(j + 1) * 4}]"
            });

            int end = i;

            // the goal here is to find the end of a method, and while searching for the end of the method we eliminate
            // any hardware registers that are being used
            // we'll also keep track of how often a local is used here, eliminating the number of passes we have to make
            for (; end < assembly.Count; end++)
            {
                if (assembly[end].StartsWith("; Exporting assembly for method")) break;
                //if (assembly[end].Trim().StartsWith("call")) break;

                var line = assembly[end].Trim();
                if (line.StartsWith("rep"))
                {
                    availableRegisters.TryRemove("edi");
                    if (line.Contains("movs")) availableRegisters.TryRemove("esi");
                }

                if (line.Contains("edx")) availableRegisters.TryRemove("edx");
                if (line.Contains("ecx")) availableRegisters.TryRemove("ecx");

                foreach (var local in locals)
                    if (line.Contains(local.Name)) local.Uses++;
            }

            for (int j = locals.Count - 1; j >= 0; j--) if (locals[j].Uses == 0) locals.RemoveAt(j);
            locals.Sort();

            int replacements = Math.Min(availableRegisters.Count, locals.Count);

            // replace the most used locals with available registers
            if (locals.Count != 0 && availableRegisters.Count != 0)
            {
                for (int j = i; j < end; j++)
                {
                    if (!assembly[j].Contains("[ebp")) continue;

                    for (int r = 0; r < replacements; r++)
                    {
                        assembly[j] = assembly[j].Replace(locals[r].Name, availableRegisters[r]);
                    }
                }
            }

            // inject moves before/after any call in our method as the callee may modify our available registers
            for (int j = i; j < end; j++)
            {
                if (assembly[j].Trim().StartsWith("call"))
                {
                    for (int r = 0; r < replacements; r++)
                    {
                        assembly.Insert(j + 1, $"    mov {availableRegisters[r]}, {locals[r].Name}");
                    }
                    for (int r = 0; r < replacements; r++)
                    {
                        assembly.Insert(j, $"    mov {locals[r].Name}, {availableRegisters[r]}");
                    }
                    end += replacements * 2;
                    j += replacements * 2;
                }
            }

            i = end;
        }
    }

    internal class StackVariable : IComparable<StackVariable>
    {
        public string Name;
        public int Uses;

        public int CompareTo(StackVariable other)
        {
            return -Uses.CompareTo(other.Uses);
        }

        public override string ToString()
        {
            return $"{Name} used {Uses} times";
        }
    }

    internal static class ListExtensions
    {
        internal static void TryRemove(this List<string> list, string item)
        {
            if (list.Contains(item)) list.Remove(item);
        }
    }
}