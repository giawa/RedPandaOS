using PELoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IL2Asm.Assembler.x86_RealMode
{
    public class Assembler : IAssembler
    {
        private List<AssembledMethod> _methods = new List<AssembledMethod>();
        private Dictionary<string, object> _initializedData = new Dictionary<string, object>();
        private object[] _localVariables = new object[256];

        private int _bytesPerRegister = 2;

        private sbyte _sbyte;
        private string _jmpLabel;

        public void Assemble(VirtualMemory memory, CLIMetadata metadata, MethodDefLayout methodDef)
        {
            var method = new MethodHeader(memory, metadata, methodDef);
            var assembly = new AssembledMethod(metadata, method);

            var code = method.Code;

            if (_methods.Count == 0)
            {
                assembly.AddAsm("[org 0x7c00]");    // for bootsector code only
                assembly.AddAsm("");
            }
            else
            {
                string label = methodDef.ToString().Replace(".", "_");
                assembly.AddAsm($"{label}:");
                assembly.AddAsm("push bp");
                assembly.AddAsm("mov bp, sp");

                if (method.LocalVars != null)
                {
                    int localVarCount = method.LocalVars.LocalVariables.Length;
                    if (localVarCount > 0)
                    {
                        assembly.AddAsm("push cx");
                        assembly.AddAsm("mov cx, 0");
                    }
                    if (localVarCount > 1)
                    {
                        assembly.AddAsm("push dx");
                        assembly.AddAsm("mov dx, 0");
                    }
                    if (localVarCount > 2)
                    {
                        assembly.AddAsm("push 0");
                    }
                    if (localVarCount > 3) throw new Exception("Too many local variables for now, need a heap.");
                }
            }

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

                    // LDARG.0
                    case 0x02:
                        assembly.AddAsm($"push [bp + {_bytesPerRegister * 2}]");
                        break;

                    // LDLOC.0
                    case 0x06:
                        assembly.AddAsm("push cx");
                        break;
                    // LDLOC.1
                    case 0x07:
                        assembly.AddAsm("push cx");
                        break;
                    // LDLOC.2
                    case 0x08:
                        assembly.AddAsm("mov ax, [bp]");
                        assembly.AddAsm("push ax");
                        break;

                    // LDC.I4.0
                    case 0x16:
                        assembly.AddAsm("push 0");
                        break;

                    // STLOC.0
                    case 0x0A:
                        assembly.AddAsm("pop cx");
                        break;
                    // STLOC.1
                    case 0x0B:
                        assembly.AddAsm("pop dx");
                        break;
                    // STLOC.1
                    case 0x0C:
                        assembly.AddAsm("pop ax");
                        assembly.AddAsm("mov [bp], ax");
                        break;

                    // LDC.I4.S
                    case 0x1F:
                        assembly.AddAsm($"push {code[i++]}");
                        break;
                    
                    // LDC.I4.1
                    case 0x17:
                        assembly.AddAsm("push 1");
                        break;

                    case 0x28: CALL(assembly, metadata, code, ref i); break;
                    case 0x6F: CALLVIRT(assembly, metadata, code, ref i); break;

                    // RET
                    case 0x002A:
                        if (method.LocalVars != null)
                        {
                            int localVarCount = method.LocalVars.LocalVariables.Length;
                            if (localVarCount > 2) assembly.AddAsm("pop");      // this was actually just a 0
                            if (localVarCount > 1) assembly.AddAsm("pop dx");
                            if (localVarCount > 0) assembly.AddAsm("pop cx");
                        }

                        int bytes = (int)methodDef.paramList * _bytesPerRegister;
                        assembly.Assembly.Add("pop bp");
                        assembly.Assembly.Add($"ret {bytes}");
                        break;

                    // BR.S
                    case 0x002B:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{_methods.Count}";
                        assembly.AddAsm($"jmp {_jmpLabel}");
                        break;

                    // BRTRUE.S
                    case 0x002D:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{_methods.Count}";
                        assembly.AddAsm("pop ax");
                        assembly.AddAsm("cmp ax,1");
                        assembly.AddAsm($"je {_jmpLabel}");
                        break;

                    case 0x58:
                        assembly.Assembly.Add("pop ax");
                        assembly.Assembly.Add("pop bx");
                        assembly.Assembly.Add("add ax, bx");
                        assembly.Assembly.Add("push ax");
                        break;

                    case 0x72: LDSTR(assembly, metadata, code, ref i); break;// LDSTR

                    // CONV.U2
                    case 0xD1:
                        // already must be 16 bit because we're in real mode
                        /*assembly.Assembly.Add("pop ax");
                        assembly.Assembly.Add("and ax, 65535");
                        assembly.Assembly.Add("push ax");*/
                        break;

                    // CONV.U1
                    case 0xD2:
                        assembly.Assembly.Add("pop ax");
                        assembly.Assembly.Add("and ax, 255");
                        assembly.Assembly.Add("push ax");
                        break;

                    // CLT
                    case 0xFE04:
                        assembly.Assembly.Add("pop ax");           // value2
                        assembly.Assembly.Add("pop bx");           // value1
                        assembly.Assembly.Add("cmp ax, bx"); // compare values
                        assembly.Assembly.Add("lahf");              // load flags into ah
                        assembly.Assembly.Add("and ah, 128");        // get only the signs info
                        assembly.Assembly.Add("shr ax, 15");        // push the 1 into the LSB
                        assembly.Assembly.Add("push ax");
                        break;

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

            var methodsToCompile = _methodsToCompile.ToArray();
            _methodsToCompile.Clear();

            for (int i = 0; i < methodsToCompile.Length; i++)
            {
                Assemble(memory, metadata, methodsToCompile[i]);
            }
        }

        //private Stack<object> _stack = new Stack<object>();

        private void LDSTR(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
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
            assembly.AddAsm($"push {label}");
        }

        private List<MethodDefLayout> _methodsToCompile = new List<MethodDefLayout>();

        private void CALL(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint methodDesc = BitConverter.ToUInt32(code, i);
            i += 4;

            if ((methodDesc & 0xff000000) == 0x0a000000)
            {
                var memberRef = metadata.MemberRefs[(int)(methodDesc & 0x00ffffff) - 1];
                var memberName = memberRef.ToString();

                if (memberName == "IL2Asm.Bios.WriteByte")
                {
                    assembly.AddAsm("; IL2Asm.Bios.WriteByte plug");
                    assembly.AddAsm("pop ax");
                    assembly.AddAsm("mov ah, 0x0e");
                    assembly.AddAsm("int 0x10");
                }
                else
                {
                    throw new Exception("Unable to handle this method");
                }
            }
            else if ((methodDesc & 0xff000000) == 0x06000000)
            {
                var methodDef = metadata.MethodDefs[(int)(methodDesc & 0x00ffffff) - 1];
                var memberName = methodDef.ToString();

                bool methodAlreadyCompiled = false;
                foreach (var method in _methods)
                    if (method.Method.MethodDef.ToString() == methodDef.ToString())
                        methodAlreadyCompiled = true;

                if (!methodAlreadyCompiled)
                {
                    bool methodWaitingToCompile = false;
                    foreach (var method in _methodsToCompile)
                        if (method.ToString() == methodDef.ToString())
                            methodWaitingToCompile = true;
                    if (!methodWaitingToCompile) _methodsToCompile.Add(methodDef);
                }

                string callsite = methodDef.ToString().Replace(".", "_");
                assembly.AddAsm($"call {callsite}");
            }
            else throw new Exception("Unhandled CALL target");
        }

        private void CALLVIRT(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint methodDesc = BitConverter.ToUInt32(code, i);
            i += 4;

            if ((methodDesc & 0xff000000) == 0x0a000000)
            {
                var memberRef = metadata.MemberRefs[(int)(methodDesc & 0x00ffffff) - 1];
                var memberName = memberRef.ToString();

                if (memberName == "System.String.get_Chars")
                {
                    assembly.AddAsm("; System.String.get_Chars plug");
                    assembly.AddAsm("TODO");
                }
                else if (memberName == "System.String.get_Length")
                {
                    assembly.AddAsm("; System.String.get_Length plug");
                    assembly.AddAsm("TODO");
                }
                else
                {
                    throw new Exception("Unable to handle this method");
                }
            }
            else throw new Exception("Unhandled CALL target");
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

                        if (line == "call printstring")
                        {
                            if (!dependencies.Contains("realmode_printstring.asm")) dependencies.Add("realmode_printstring.asm");
                        }
                    }
                    stream.WriteLine();
                }

                /*stream.WriteLine("; Exporting dependencies");
                foreach (var dependency in dependencies)
                {
                    stream.WriteLine($"%include \"{dependency}\"");
                }
                stream.WriteLine();*/

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
}
