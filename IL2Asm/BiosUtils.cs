using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using PELoader;

namespace IL2Asm
{
    public class AssembledMethod
    {
        public MethodHeader Method { get; private set; }
        public CLIMetadata Metadata { get; private set; }

        public List<string> Assembly { get; private set; }

        public AssembledMethod(CLIMetadata metadata, MethodHeader method)
        {
            Method = method;
            Metadata = metadata;

            Assembly = new List<string>();
        }

        public void AddAsm(string asm)
        {
            Assembly.Add(asm);
        }
    }

    public class Assembler
    {
        private List<AssembledMethod> _methods = new List<AssembledMethod>();
        private Dictionary<string, object> _initializedData = new Dictionary<string, object>();
        private object[] _localVariables = new object[256];

        public void Assemble(CLIMetadata metadata, MethodHeader method)
        {
            AssembledMethod assembly = new AssembledMethod(metadata, method);

            var code = method.Code;

            assembly.AddAsm("[org 0x7c00]");    // for bootsector code only
            assembly.AddAsm("");

            for (ushort i = 0; i < code.Length;)
            {
                int opcode = code[i++];

                if (opcode == 0xfe) opcode = (opcode << 8) | code[i++];

                // add label for this opcode
                string label = $"IL_{(i - 1).ToString("X4")}_{_methods.Count}";
                assembly.AddAsm($"{label}:");
                int asmCount = assembly.Assembly.Count;

                switch (opcode)
                {
                    case 0x00: assembly.AddAsm("nop"); break;   // NOP

                    case 0x0A: _localVariables[0] = _stack.Pop(); break;

                    case 0x17: _stack.Push(1); break;

                    case 0x28: CALL(assembly, metadata, code, ref i); break;

                    // BR.S
                    case 0x002B:
                        sbyte b = (sbyte)code[i++];
                        string jmpLabel = $"IL_{(i + b).ToString("X4")}_{_methods.Count}";
                        assembly.AddAsm($"jmp {jmpLabel}");
                        break;

                    case 0x72: LDSTR(metadata, code, ref i); break;// LDSTR

                    default: throw new Exception($"Unknown IL opcode 0x{opcode.ToString("X")}");
                }

                // remove the label if no new assembly was added
                if (assembly.Assembly.Count == asmCount)
                {
                    //assembly.Assembly.RemoveAt(assembly.Assembly.Count - 1);
                    assembly.Assembly.Add("nop");
                }
            }

            _methods.Add(assembly);
        }

        private Stack<object> _stack = new Stack<object>();

        private void LDSTR(CLIMetadata metadata, byte[] code, ref ushort i)
        {
            string label = $"DB_{(i - 1).ToString("X4")}_{_methods.Count}";

            ushort addr = BitConverter.ToUInt16(code, i);
            i += 2;
            ushort unknown = BitConverter.ToUInt16(code, i);
            i += 2;

            byte blob = metadata.US.Heap[addr++];
            string s = string.Empty;

            if ((blob & 0x80) == 0)
            {
                var bytes = metadata.US.Heap.AsSpan(addr, blob - 1);
                s = Encoding.Unicode.GetString(bytes);
            }
            else
            {
                throw new Exception("No support yet for longer blobs.  See II.24.2.4");
            }

            AddToData(label, s);
            _stack.Push(label);
        }

        private void CALL(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint methodDesc = BitConverter.ToUInt32(code, i);
            i += 4;

            if ((methodDesc & 0xff000000) == 0x0a000000)
            {
                var memberRef = metadata.MemberRefs[(int)(methodDesc & 0x00ffffff) - 1];
                var memberName = memberRef.ToString();

                if (memberName == "IL2Asm.Bios.PrintString")
                {
                    var labelToPrint = _stack.Pop() as string;
                    assembly.AddAsm($"mov bx, {labelToPrint}");
                    assembly.AddAsm("call print");
                }
            }
        }

        public void AddToData(string label, string s)
        {
            _initializedData.Add(label, s);
        }

        public void WriteAssembly(string file)
        {
            HashSet<string> dependencies = new HashSet<string>();

            using (StreamWriter stream = new StreamWriter(file))
            {
                foreach (var method in _methods)
                {
                    stream.WriteLine($"; Exporting assembly for method {method.Method.MethodDef}");
                    foreach (var line in method.Assembly)
                    {
                        stream.WriteLine(line);

                        if (line == "call print")
                        {
                            if (!dependencies.Contains("realmode_print.asm")) dependencies.Add("realmode_print.asm");
                        }
                    }
                    stream.WriteLine();
                }

                stream.WriteLine("; Exporting dependencies");
                foreach (var dependency in dependencies)
                {
                    stream.WriteLine($"%include \"{dependency}\"");
                }
                stream.WriteLine();

                stream.WriteLine("; Exporting initialized data");
                foreach (var data in _initializedData)
                {
                    if (data.Value is string)
                    {
                        stream.WriteLine($"{data.Key}:");
                        stream.WriteLine($"    db '{data.Value}', 0");  // 0 for null termination after the string
                    }
                }

                // should only do this for boot sector attribute code
                stream.WriteLine();
                stream.WriteLine("times 510-($-$$) db 0");
                stream.WriteLine("dw 0xaa55");
            }
        }
    }

    public interface ICanBeAssembled
    {
        void Emit(Assembler assembler);
    }

    public class ASCIIString : ICanBeAssembled
    {
        private string _label;
        private string _data;

        public ASCIIString(string label, string data)
        {
            _label = label;
            _data = data;
        }

        public void Emit(Assembler assembler)
        {
            assembler.AddToData(_label, _data);
        }
    }

    public static class Bios
    {
        public static void PrintString(string s)
        {
            Console.WriteLine(s);
        }
    }
}
