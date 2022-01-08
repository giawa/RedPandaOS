using IL2Asm.BaseTypes;
using System;

namespace Plugs
{
    public static class StringPlugs
    {
        [CSharpPlug("System.String.IndexOf_I4_String")]
        private static int StringIndexOfString(string s, string match)
        {
            for (int i = 0; i <= s.Length - match.Length; i++)
            {
                if (s[i] == match[0])
                {
                    for (int j = 1; j < match.Length; j++)
                    {
                        if (s[i + j] != match[j]) break;
                        if (j == match.Length - 1) return i;
                    }
                }
            }

            return -1;
        }

        [CSharpPlug("System.String.IndexOf_I4_Char")]
        private static int StringIndexOfChar(string s, char c)
        {
            for (int i = 0; i < s.Length; i++) if (s[i] == c) return i;
            return -1;
        }

        [CSharpPlug("System.String.Contains_Boolean_String")]
        private static bool StringContainsString(string s, string match)
        {
            return StringIndexOfString(s, match) != -1;
        }

        [CSharpPlug("System.String.Contains_Boolean_Char")]
        private static bool StringContainsChar(string s, char c)
        {
            for (int i = 0; i < s.Length; i++) if (s[i] == c) return true;
            return false;
        }

        [CSharpPlug("System.String.Concat_String_String_String")]
        private static string StringConcat(string s1, string s2)
        {
            char[] concat = new char[s1.Length + s2.Length + 1];
            for (int i = 0; i < s1.Length; i++) concat[i] = s1[i];
            for (int i = 0; i < s2.Length; i++) concat[i + s1.Length] = s2[i];

            // the actual string data starts at concat + 8 (skip over the array length and size per element)
            return new string(concat);
        }

        [CSharpPlug("System.String.get_Length_I4")]
        private static int StringLength(string s1)
        {
            // the array is one element longer than the string due to the null termination
            return (int)CPUHelper.CPU.ReadMemInt(Kernel.Memory.Utilities.ObjectToPtr(s1)) - 1;
        }

        [AsmPlug("System.String.get_Chars_Char_I4", Architecture.X86)]
        private static void StringGetCharsAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax");  // pop index
            assembly.AddAsm("pop ebx");  // pop this
            assembly.AddAsm("lea ebx, [2 * eax + ebx + 8]");
            assembly.AddAsm("mov eax, [ebx]");
            assembly.AddAsm("and eax, 65535");
            assembly.AddAsm("push eax");
        }
    }
}
