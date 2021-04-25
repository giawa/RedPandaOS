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

        public const int BytesPerRegister = 2;

        private sbyte _sbyte;
        private byte _byte;
        private string _jmpLabel;
        private uint _uint;

        public void Assemble(VirtualMemory memory, CLIMetadata metadata, MethodDefLayout methodDef)
        {
            var method = new MethodHeader(memory, metadata, methodDef);
            var assembly = new AssembledMethod(metadata, method);

            var code = method.Code;

            if (_methods.Count == 0)
            {
                assembly.AddAsm("[bits 16]");    // for bootsector code only
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
                    for (int i = 2; i < localVarCount; i++)
                        assembly.AddAsm("push 0");
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
                    case 0x00: /*assembly.AddAsm("nop");*/ break;   // NOP

                    // LDARG.0
                    case 0x02:
                        _uint = method.MethodDef.MethodSignature.ParamCount - 0;
                        assembly.AddAsm($"mov ax, [bp + {BytesPerRegister * (1 + _uint)}]");
                        assembly.AddAsm("push ax");
                        break;
                    // LDARG.1
                    case 0x03:
                        _uint = method.MethodDef.MethodSignature.ParamCount - 1;
                        assembly.AddAsm($"mov ax, [bp + {BytesPerRegister * (1 + _uint)}]");
                        assembly.AddAsm("push ax");
                        break;
                    // LDARG.2
                    case 0x04:
                        _uint = method.MethodDef.MethodSignature.ParamCount - 2;
                        assembly.AddAsm($"mov ax, [bp + {BytesPerRegister * (1 + _uint)}]");
                        assembly.AddAsm("push ax");
                        break;
                    // LDARG.3
                    case 0x05:
                        _uint = method.MethodDef.MethodSignature.ParamCount - 3;
                        assembly.AddAsm($"mov ax, [bp + {BytesPerRegister * (1 + _uint)}]");
                        assembly.AddAsm("push ax");
                        break;

                    // LDLOC.0
                    case 0x06:
                        assembly.AddAsm("push cx");
                        break;
                    // LDLOC.1
                    case 0x07:
                        assembly.AddAsm("push dx");
                        break;
                    // LDLOC.2
                    case 0x08:
                        assembly.AddAsm("mov ax, [bp - 6]");
                        assembly.AddAsm("push ax");
                        break;
                    // LDLOC.3
                    case 0x09:
                        assembly.AddAsm("mov ax, [bp - 8]");
                        assembly.AddAsm("push ax");
                        break;

                    // STARG.S
                    case 0x10:
                        _byte = code[i++];
                        _uint = method.MethodDef.MethodSignature.ParamCount - _byte;
                        assembly.AddAsm("pop ax");
                        assembly.AddAsm($"mov [bp + {BytesPerRegister * (1 + _uint)}], ax");
                        break;

                    // STLOC.S
                    case 0x13:
                        _byte = code[i++];
                        if (_byte == 0) assembly.AddAsm("pop cx");
                        else if (_byte == 1) assembly.AddAsm("pop dx");
                        else
                        {
                            assembly.AddAsm("pop ax");
                            assembly.AddAsm($"mov [bp - {BytesPerRegister * (_byte + 1)}], ax");
                        }
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
                    // STLOC.2
                    case 0x0C:
                        assembly.AddAsm("pop ax");
                        assembly.AddAsm("mov [bp - 6], ax");
                        break;
                    // STLOC.3
                    case 0x0D:
                        assembly.AddAsm("pop ax");
                        assembly.AddAsm("mov [bp - 8], ax");
                        break;

                    // LDLOC.S
                    case 0x11:
                        _byte = code[i++];
                        if (_byte == 0) assembly.AddAsm("push cx");
                        else if (_byte == 1) assembly.AddAsm("push dx");
                        else
                        {
                            assembly.AddAsm($"mov ax, [bp - {BytesPerRegister * (_byte + 1)}]");
                            assembly.AddAsm("push ax");
                        }
                        break;

                    // LDC.I4.1
                    case 0x17: assembly.AddAsm("push 1"); break;
                    case 0x18: assembly.AddAsm("push 2"); break;
                    case 0x19: assembly.AddAsm("push 3"); break;
                    case 0x1A: assembly.AddAsm("push 4"); break;
                    case 0x1B: assembly.AddAsm("push 5"); break;
                    case 0x1C: assembly.AddAsm("push 6"); break;
                    case 0x1D: assembly.AddAsm("push 7"); break;
                    case 0x1E: assembly.AddAsm("push 8"); break;

                    // LDC.I4.S
                    case 0x1F:
                        assembly.AddAsm($"push {code[i++]}");
                        break;

                    // LDC.I4
                    case 0x20:
                        int value = BitConverter.ToInt32(code, i);
                        if (value > short.MaxValue || value < short.MinValue) 
                            throw new Exception("Out of range for 16 bit mode");
                        i += 4;
                        assembly.AddAsm($"push {value}");
                        break;

                    // POP
                    case 0x26:
                        assembly.AddAsm("pop ax");
                        break;

                    case 0x28: CALL(assembly, metadata, code, ref i); break;
                    case 0x6F: CALLVIRT(assembly, metadata, code, ref i); break;

                    // RET
                    case 0x2A:
                        // place the returned value on ax, which should clear our CLI stack
                        if (method.MethodDef.MethodSignature.RetType != null && method.MethodDef.MethodSignature.RetType.Type != ElementType.EType.Void)
                        {
                            assembly.AddAsm("pop ax; return value");
                        }

                        // pop any local variables we pushed at the start
                        if (method.LocalVars != null)
                        {
                            int localVarCount = method.LocalVars.LocalVariables.Length;
                            for (int p = 2; p < localVarCount; p++)
                                assembly.AddAsm("pop bx; localvar that was pushed on stack");
                            if (localVarCount > 1) assembly.AddAsm("pop dx; localvar.1");
                            if (localVarCount > 0) assembly.AddAsm("pop cx; localvar.0");
                        }

                        int bytes = (int)methodDef.MethodSignature.ParamCount * BytesPerRegister;
                        assembly.AddAsm("pop bp");
                        assembly.AddAsm($"ret {bytes}");
                        break;

                    // BR.S
                    case 0x2B:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{_methods.Count}";
                        assembly.AddAsm($"jmp {_jmpLabel}");
                        break;

                    // BRFALSE.S
                    case 0x2C:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{_methods.Count}";
                        assembly.AddAsm("pop ax");
                        assembly.AddAsm("cmp ax, 0");
                        assembly.AddAsm($"je {_jmpLabel}");
                        break;

                    // BRTRUE.S
                    case 0x2D:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{_methods.Count}";
                        assembly.AddAsm("pop ax");
                        assembly.AddAsm("cmp ax, 0");
                        assembly.AddAsm($"jne {_jmpLabel}");
                        break;

                    // BGE.S
                    case 0x2F:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{_methods.Count}";
                        assembly.AddAsm("pop ax");        // value2
                        assembly.AddAsm("pop bx");        // value1
                        assembly.AddAsm("cmp bx, ax");    // compare values
                        assembly.AddAsm($"jge {_jmpLabel}");
                        break;

                    // BGT.S
                    case 0x30:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{_methods.Count}";
                        assembly.AddAsm("pop ax");        // value2
                        assembly.AddAsm("pop bx");        // value1
                        assembly.AddAsm("cmp bx, ax");    // compare values
                        assembly.AddAsm($"jg {_jmpLabel}");
                        break;

                    // BLE.S
                    case 0x31:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{_methods.Count}";
                        assembly.AddAsm("pop ax");        // value2
                        assembly.AddAsm("pop bx");        // value1
                        assembly.AddAsm("cmp bx, ax");    // compare values
                        assembly.AddAsm($"jle {_jmpLabel}");
                        break;

                    // BLT.S
                    case 0x32:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{_methods.Count}";
                        assembly.AddAsm("pop ax");        // value2
                        assembly.AddAsm("pop bx");        // value1
                        assembly.AddAsm("cmp bx, ax");    // compare values
                        assembly.AddAsm($"jl {_jmpLabel}");
                        break;

                    // BNE.UN.S
                    case 0x33:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{_methods.Count}";
                        assembly.AddAsm("pop ax");        // value2
                        assembly.AddAsm("pop bx");        // value1
                        assembly.AddAsm("cmp bx, ax");    // compare values
                        assembly.AddAsm($"jne {_jmpLabel}");
                        break;

                    // ADD
                    case 0x58:
                        assembly.AddAsm("pop bx");
                        assembly.AddAsm("pop ax");
                        assembly.AddAsm("add ax, bx");
                        assembly.AddAsm("push ax");
                        break;

                    // SUB
                    case 0x59:
                        assembly.AddAsm("pop bx");
                        assembly.AddAsm("pop ax");
                        assembly.AddAsm("sub ax, bx");
                        assembly.AddAsm("push ax");
                        break;

                    // MUL
                    case 0x5A:
                        assembly.AddAsm("pop bx");
                        assembly.AddAsm("pop ax");
                        assembly.AddAsm("mul bx");
                        assembly.AddAsm("push ax");
                        break;

                    // AND
                    case 0x5F:
                        assembly.AddAsm("pop bx");
                        assembly.AddAsm("pop ax");
                        assembly.AddAsm("and ax, bx");
                        assembly.AddAsm("push ax");
                        break;

                    // SHR
                    case 0x63:
                        assembly.AddAsm("mov bx, cx");  // cx is needed to access cl for variable shift, so store is in bx temporarily
                        assembly.AddAsm("pop cx");      // get amount to shift
                        assembly.AddAsm("pop ax");
                        assembly.AddAsm("shr ax, cl");
                        assembly.AddAsm("mov cx, bx");  // restore cx
                        assembly.AddAsm("push ax");
                        break;

                    // CONV.I2
                    case 0x68:
                        // already must be 16 bit because we're in real mode
                        /*assembly.AddAsm("pop ax");
                        assembly.AddAsm("and ax, 65535");
                        assembly.AddAsm("push ax");*/
                        break;

                    // LDSTR
                    case 0x72: LDSTR(assembly, metadata, code, ref i); break;

                    // LDSFLD
                    case 0x7E: LDSFLD(assembly, metadata, code, ref i); break;

                    // LDSFLDA
                    case 0x7F: LDSFLDA(assembly, metadata, code, ref i); break;

                    // STSFLD
                    case 0x80: STSFLD(assembly, metadata, code, ref i); break;

                    // CONV.U2
                    case 0xD1:
                        // already must be 16 bit because we're in real mode
                        /*assembly.AddAsm("pop ax");
                        assembly.AddAsm("and ax, 65535");
                        assembly.AddAsm("push ax");*/
                        break;

                    // CONV.U1
                    case 0xD2:
                        assembly.AddAsm("pop ax");
                        assembly.AddAsm("and ax, 255");
                        assembly.AddAsm("push ax");
                        break;

                    // CEQ
                    case 0xFE01:
                        assembly.AddAsm("pop ax");        // value2
                        assembly.AddAsm("pop bx");        // value1
                        assembly.AddAsm("cmp ax, bx");    // compare values
                        assembly.AddAsm("lahf");          // load flags into ah
                        assembly.AddAsm("shr ax, 14");    // push the 1 into the LSB
                        assembly.AddAsm("and ax, 1");     // push the 1 into the LSB
                        assembly.AddAsm("push ax");
                        break;

                    // CGT
                    case 0xFE02:
                        assembly.AddAsm("pop ax");        // value2
                        assembly.AddAsm("pop bx");        // value1
                        assembly.AddAsm("cmp ax, bx");    // compare values
                        assembly.AddAsm("lahf");          // load flags into ah
                        assembly.AddAsm("shr ax, 15");    // push the 1 into the LSB
                        assembly.AddAsm("and ax, 1");     // push the 1 into the LSB
                        assembly.AddAsm("push ax");
                        break;

                    // CGT.UN (identical to CGT for now)
                    case 0xFE03:
                        assembly.AddAsm("pop ax");        // value2
                        assembly.AddAsm("pop bx");        // value1
                        assembly.AddAsm("cmp ax, bx");    // compare values
                        assembly.AddAsm("lahf");          // load flags into ah
                        assembly.AddAsm("shr ax, 15");    // push the 1 into the LSB
                        assembly.AddAsm("and ax, 1");     // push the 1 into the LSB
                        assembly.AddAsm("push ax");
                        break;

                    // CLT
                    case 0xFE04:
                        assembly.AddAsm("pop ax");        // value2
                        assembly.AddAsm("pop bx");        // value1
                        assembly.AddAsm("cmp bx, ax");    // compare values
                        assembly.AddAsm("lahf");          // load flags into ah
                        assembly.AddAsm("shr ax, 15");    // push the 1 into the LSB
                        assembly.AddAsm("and ax, 1");     // push the 1 into the LSB
                        assembly.AddAsm("push ax");
                        break;

                    default: throw new Exception($"Unknown IL opcode 0x{opcode.ToString("X")} at {label}");
                }

                // remove the label if no new assembly was added
                if (assembly.Assembly.Count == asmCount)
                {
                    //assembly.Assembly.RemoveAt(assembly.Assembly.Count - 1);
                    //assembly.AddAsm("nop");
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

            uint metadataToken = BitConverter.ToUInt32(code, i);
            i += 4;

            string s = Encoding.Unicode.GetString(metadata.GetMetadata(metadataToken));
            
            _initializedData.Add(label, s);
            assembly.AddAsm($"push {label}");
        }

        private void LDSFLD(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            string label = $"DB_{(i - 1).ToString("X4")}_{_methods.Count}";

            int addr = BitConverter.ToInt32(code, i);
            i += 4;

            if (!_initializedData.ContainsKey(label)) AddStaticField(metadata, label, addr);

            assembly.AddAsm($"mov ax, [{label}]");
            assembly.AddAsm($"push ax");
        }

        private void LDSFLDA(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            string label = $"DB_{(i - 1).ToString("X4")}_{_methods.Count}";

            int addr = BitConverter.ToInt32(code, i);
            i += 4;

            if (!_initializedData.ContainsKey(label)) AddStaticField(metadata, label, addr);

            assembly.AddAsm($"push {label}");
        }

        private void AddStaticField(CLIMetadata metadata, string label, int fieldToken)
        {
            if ((fieldToken & 0xff000000) == 0x04000000)
            {
                var field = metadata.Fields[(fieldToken & 0x00ffffff) - 1];

                if ((field.flags & FieldLayout.FieldLayoutFlags.Static) == FieldLayout.FieldLayoutFlags.Static)
                {
                    switch (field.Type.Type)
                    {
                        case ElementType.EType.U1: _initializedData.Add(label, (byte)0); break;
                        case ElementType.EType.I1: _initializedData.Add(label, (sbyte)0); break;
                        case ElementType.EType.U2: _initializedData.Add(label, (ushort)0); break;
                        case ElementType.EType.I2: _initializedData.Add(label, (short)0); break;
                        default: throw new Exception("Unsupported type");
                    }
                }
                else
                {
                    throw new Exception("Incomplete implementation");
                }
            }
            else throw new Exception("Unexpected table found when trying to find a field.");
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

                if (methodDef.MethodSignature.RetType != null && methodDef.MethodSignature.RetType.Type != ElementType.EType.Void) assembly.AddAsm("push ax");
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
                    assembly.AddAsm("pop ax");  // pop index
                    assembly.AddAsm("pop bx");  // pop this
                    assembly.AddAsm("add ax, bx");
                    assembly.AddAsm("mov bx, ax");
                    assembly.AddAsm("mov ax, [bx]");
                    assembly.AddAsm("push ax");
                }
                else if (memberName == "System.String.get_Length")
                {
                    assembly.AddAsm("; System.String.get_Length plug");
                    assembly.AddAsm("pop bx");  // pop this
                    assembly.AddAsm("push 14");
                }
                else
                {
                    throw new Exception("Unable to handle this method");
                }
            }
            else throw new Exception("Unhandled CALL target");
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
                        if (!line.EndsWith(":") && !line.StartsWith("[")) stream.Write("    ");
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

                if (_initializedData.Count > 0)
                {
                    stream.WriteLine("; Exporting initialized data");
                    foreach (var data in _initializedData)
                    {
                        if (data.Value is string)
                        {
                            stream.WriteLine($"{data.Key}:");
                            stream.WriteLine($"    db '{data.Value}', 0");  // 0 for null termination after the string
                        }
                        else if (data.Value is short)
                        {
                            stream.WriteLine($"{data.Key}:");
                            stream.WriteLine($"    dw {(short)data.Value}");
                        }
                        else if (data.Value is ushort)
                        {
                            stream.WriteLine($"{data.Key}:");
                            stream.WriteLine($"    dw {(ushort)data.Value}");
                        }
                        else if (data.Value is byte)
                        {
                            stream.WriteLine($"{data.Key}:");
                            stream.WriteLine($"    db {(byte)data.Value}");
                        }
                        else if (data.Value is sbyte)
                        {
                            stream.WriteLine($"{data.Key}:");
                            stream.WriteLine($"    db {(sbyte)data.Value}");
                        }
                        else
                        {
                            throw new Exception("Unexpected type allocated as part of initial data");
                        }
                    }
                    stream.WriteLine();
                }

                // should only do this for boot sector attribute code
                stream.WriteLine("times 510-($-$$) db 0");
                stream.WriteLine("dw 0xaa55");
            }
        }
    }
}
