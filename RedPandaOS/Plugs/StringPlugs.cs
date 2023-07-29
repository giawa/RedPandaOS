using IL2Asm.BaseTypes;
using System;

namespace Plugs
{
    public static class StringPlugs
    {
        [CSharpPlug("System.String.op_Inequality_Boolean_String_String")]
        private static bool StringInequality(string s1, string s2)
        {
            return !StringEquality(s1, s2);
        }

        [CSharpPlug("System.String.op_Equality_Boolean_String_String")]
        private static bool StringEquality(string s1, string s2)
        {
            if (s1 == null && s2 == null) return true;
            if (s1 == null || s2 == null) return false;
            if (s1.Length != s2.Length) return false;

            for (int i = 0; i < s1.Length; i++)
                if (s1[i] != s2[i]) return false;

            return true;
        }

        [CSharpPlug("System.String.Equals_Boolean_String")]
        private static bool StringEquals(string s1, string s2)
        {
            return s1 == s2;
        }

        [AsmPlug("System.String.ToCharArray_SzArray", Architecture.X86)]
        private static void StringToCharArrayAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("; nop");
        }

        [CSharpPlug("System.String.Substring_String_I4_I4")]
        private static string StringSubstring(string s, int start, int length)
        {
            if (start + length > s.Length || start < 0) throw new ArgumentOutOfRangeException("Invalid startIndex or length");

            char[] array = new char[length + 1];
            for (int i = 0; i < array.Length - 1; i++) array[i] = s[i + start];
            return new string(array);
        }

        [CSharpPlug("System.String.Substring_String_I4")]
        private static string StringSubstring(string s, int start)
        {
            if (start > s.Length || start < 0) throw new ArgumentOutOfRangeException("Invalid startIndex");

            char[] array = new char[s.Length - start + 1];
            for (int i = 0; i < array.Length - 1; i++) array[i] = s[i + start];
            return new string(array);
        }

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

        [CSharpPlug("System.String.Equals_Boolean_String_String_ValueType")]
        private static bool StringEqualsWithValueType(string s1, string s2, StringComparison comparisonType)
        {
            if (s1 == null && s2 == null) return true;
            if (s1 == null || s2 == null) return false;
            if (s1.Length != s2.Length) return false;

            char c1, c2;

            switch (comparisonType)
            {
                case StringComparison.Ordinal: return s1.Equals(s2);
                case StringComparison.CurrentCultureIgnoreCase:
                    for (int i = 0; i < s1.Length; i++)
                    {
                        c1 = s1[i];
                        c2 = s2[i];

                        if (c1 >= 'A' && c1 <= 'Z') c1 = (char)(c1 + 32);
                        if (c2 >= 'A' && c2 <= 'Z') c2 = (char)(c2 + 32);

                        if (c1 != c2) return false;
                    }
                    break;
                default:
                    throw new Exception("Unsupported string comparison");
            }

            return true;
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

        [CSharpPlug("System.String.Concat_String_String_String_String")]
        private static string StringConcat3(string s1, string s2, string s3)
        {
            char[] concat = new char[s1.Length + s2.Length + s3.Length];
            for (int i = 0; i < s1.Length; i++) concat[i] = s1[i];
            for (int i = 0; i < s2.Length; i++) concat[i + s1.Length] = s2[i];
            for (int i = 0; i < s3.Length; i++) concat[i + s1.Length + s2.Length] = s3[i];

            // the actual string data starts at concat + 8 (skip over the array length and size per element)
            return new string(concat);
        }

        [CSharpPlug("System.String.Concat_String_String_String")]
        private static string StringConcat2(string s1, string s2)
        {
            char[] concat = new char[s1.Length + s2.Length];
            for (int i = 0; i < s1.Length; i++) concat[i] = s1[i];
            for (int i = 0; i < s2.Length; i++) concat[i + s1.Length] = s2[i];

            // the actual string data starts at concat + 8 (skip over the array length and size per element)
            return new string(concat);
        }

        [CSharpPlug("System.String.get_Length_I4")]
        private static int StringLength(string s1)
        {
            return (int)CPUHelper.CPU.ReadMemInt(Kernel.Memory.Utilities.ObjectToPtr(s1));
        }

        [AsmPlug("System.String.get_Chars_Char_I4", Architecture.X86)]
        private static void StringGetCharsAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax");  // pop index
            assembly.AddAsm("pop ebx");  // pop this
            assembly.AddAsm("lea ebx, [2 * eax + ebx + 8]");
            assembly.AddAsm("movzx eax, word [ebx]");
            assembly.AddAsm("push eax");
        }
    }
}
