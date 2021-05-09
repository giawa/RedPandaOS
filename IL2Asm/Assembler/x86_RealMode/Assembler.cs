using PELoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace IL2Asm.Assembler.x86_RealMode
{
    public class Assembler : IAssembler
    {
        private Dictionary<string, AssembledMethod> _staticConstructors = new Dictionary<string, AssembledMethod>();
        private List<AssembledMethod> _methods = new List<AssembledMethod>();
        private Dictionary<string, DataType> _initializedData = new Dictionary<string, DataType>();
        private Runtime _runtime = new Runtime();

        public const int BytesPerRegister = 2;

        private sbyte _sbyte;
        private byte _byte;
        private string _jmpLabel;
        private uint _uint;
        private int _int;

        public void AddAssembly(PortableExecutableFile pe)
        {
            _runtime.AddAssembly(pe);
        }

        public void Assemble(PortableExecutableFile pe, MethodDefLayout methodDef)
        {
            if (!_runtime.Assemblies.Contains(pe)) throw new Exception("The portable executable must be added via AddAssembly prior to called Assemble");
            if (methodDef != null && _methods.Where(m => m.Method != null && m.Method.MethodDef.ToAsmString() == methodDef.ToAsmString()).Any()) return;

            var method = new MethodHeader(pe.Memory, pe.Metadata, methodDef);
            var assembly = new AssembledMethod(pe.Metadata, method);
            _methods.Add(assembly);
            Runtime.GlobalMethodCounter++;

            var code = method.Code;
            int localVarOffset = 0;

            if (_methods.Count > 1)
            {
                string label = methodDef.ToAsmString();
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
                        localVarOffset = BytesPerRegister;
                    }
                    if (localVarCount > 1)
                    {
                        assembly.AddAsm("push dx");
                        assembly.AddAsm("mov dx, 0");
                        localVarOffset = BytesPerRegister * 2;
                    }
                }
            }

            if (method.LocalVars != null)
            {
                int localVarCount = method.LocalVars.LocalVariables.Length;
                for (int i = 2; i < localVarCount; i++)
                    assembly.AddAsm($"push 0; localvar.{i + 1}");
            }

            for (ushort i = 0; i < code.Length;)
            {
                int opcode = code[i++];

                if (opcode == 0xfe) opcode = (opcode << 8) | code[i++];

                // add label for this opcode
                string label = $"IL_{(i - 1).ToString("X4")}_{Runtime.GlobalMethodCounter}";
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
                        assembly.AddAsm($"mov ax, [bp - {localVarOffset + BytesPerRegister}]");
                        assembly.AddAsm("push ax");
                        break;
                    // LDLOC.3
                    case 0x09:
                        assembly.AddAsm($"mov ax, [bp - {localVarOffset + 2 * BytesPerRegister}]");
                        assembly.AddAsm("push ax");
                        break;

                    // LDARG.S
                    case 0x0E:
                        _byte = code[i++];
                        _uint = method.MethodDef.MethodSignature.ParamCount - _byte;
                        assembly.AddAsm($"mov ax, [bp + {BytesPerRegister * (1 + _uint)}]");
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
                            assembly.AddAsm($"mov [bp - {BytesPerRegister * (_byte - 1 + localVarOffset)}], ax");
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
                        assembly.AddAsm($"mov [bp - {localVarOffset + BytesPerRegister}], ax");
                        break;
                    // STLOC.3
                    case 0x0D:
                        assembly.AddAsm("pop ax");
                        assembly.AddAsm($"mov [bp - {localVarOffset + 2 * BytesPerRegister}], ax");
                        break;

                    // LDLOC.S
                    case 0x11:
                        _byte = code[i++];
                        if (_byte == 0) assembly.AddAsm("push cx");
                        else if (_byte == 1) assembly.AddAsm("push dx");
                        else
                        {
                            assembly.AddAsm($"mov ax, [bp - {BytesPerRegister * (_byte - 1 + localVarOffset)}]");
                            assembly.AddAsm("push ax");
                        }
                        break;

                    // LDLOCA.S
                    case 0x12:
                        _byte = code[i++];
                        if (_byte == 0) assembly.AddAsm("push 2");      // This is my made up address for CX
                        else if (_byte == 1) assembly.AddAsm("push 3"); // This is my made up address for DX
                        else
                        {
                            assembly.AddAsm("mov ax, bp");
                            assembly.AddAsm($"sub ax, {BytesPerRegister * (_byte + 1)}");
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
                        if (value > ushort.MaxValue || value < short.MinValue)
                            throw new Exception("Out of range for 16 bit mode");
                        i += 4;
                        assembly.AddAsm($"push {value}");
                        break;

                    // DUP
                    case 0x25:
                        assembly.AddAsm("pop ax");
                        assembly.AddAsm("push ax");
                        assembly.AddAsm("push ax");
                        break;

                    // POP
                    case 0x26:
                        assembly.AddAsm("pop ax");
                        break;

                    case 0x28: CALL(assembly, pe.Metadata, code, ref i); break;
                    case 0x6F: CALLVIRT(assembly, pe.Metadata, code, ref i); break;

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
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm($"jmp {_jmpLabel}");
                        break;

                    // BRFALSE.S
                    case 0x2C:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop ax");
                        assembly.AddAsm("cmp ax, 0");
                        assembly.AddAsm($"je {_jmpLabel}");
                        break;

                    // BRTRUE.S
                    case 0x2D:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop ax");
                        assembly.AddAsm("cmp ax, 0");
                        assembly.AddAsm($"jne {_jmpLabel}");
                        break;

                    // BEQ.S
                    case 0x2E:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop ax");        // value2
                        assembly.AddAsm("pop bx");        // value1
                        assembly.AddAsm("cmp bx, ax");    // compare values
                        assembly.AddAsm($"je {_jmpLabel}");
                        break;

                    // BGE.S
                    case 0x2F:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop ax");        // value2
                        assembly.AddAsm("pop bx");        // value1
                        assembly.AddAsm("cmp bx, ax");    // compare values
                        assembly.AddAsm($"jge {_jmpLabel}");
                        break;

                    // BGT.S
                    case 0x30:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop ax");        // value2
                        assembly.AddAsm("pop bx");        // value1
                        assembly.AddAsm("cmp bx, ax");    // compare values
                        assembly.AddAsm($"jg {_jmpLabel}");
                        break;

                    // BLE.S
                    case 0x31:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop ax");        // value2
                        assembly.AddAsm("pop bx");        // value1
                        assembly.AddAsm("cmp bx, ax");    // compare values
                        assembly.AddAsm($"jle {_jmpLabel}");
                        break;

                    // BLT.S
                    case 0x32:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop ax");        // value2
                        assembly.AddAsm("pop bx");        // value1
                        assembly.AddAsm("cmp bx, ax");    // compare values
                        assembly.AddAsm($"jl {_jmpLabel}");
                        break;

                    // BNE.UN.S
                    case 0x33:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop ax");        // value2
                        assembly.AddAsm("pop bx");        // value1
                        assembly.AddAsm("cmp bx, ax");    // compare values
                        assembly.AddAsm($"jne {_jmpLabel}");
                        break;

                    // BGE.UN.S
                    case 0x34:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop ax");        // value2
                        assembly.AddAsm("pop bx");        // value1
                        assembly.AddAsm("cmp bx, ax");    // compare values
                        assembly.AddAsm($"jae {_jmpLabel}");
                        break;

                    // BR
                    case 0x38:
                        _int = BitConverter.ToInt32(code, i);
                        i += 4;

                        _jmpLabel = $"IL_{(i + _int).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm($"jmp {_jmpLabel}");
                        break;

                    // BRFALSE
                    case 0x39:
                        _int = BitConverter.ToInt32(code, i);
                        i += 4;

                        _jmpLabel = $"IL_{(i + _int).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop ax");        // value2
                        assembly.AddAsm("cmp bx, 0");    // compare values
                        assembly.AddAsm($"je {_jmpLabel}");
                        break;

                    // BRTRUE
                    case 0x3A:
                        _int = BitConverter.ToInt32(code, i);
                        i += 4;

                        _jmpLabel = $"IL_{(i + _int).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop ax");        // value2
                        assembly.AddAsm("cmp bx, 0");    // compare values
                        assembly.AddAsm($"jne {_jmpLabel}");
                        break;

                    // BLT
                    case 0x3F:
                        _int = BitConverter.ToInt32(code, i);
                        i += 4;

                        _jmpLabel = $"IL_{(i + _int).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop ax");        // value2
                        assembly.AddAsm("pop bx");        // value1
                        assembly.AddAsm("cmp bx, ax");    // compare values
                        assembly.AddAsm($"jl {_jmpLabel}");
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
                        assembly.AddAsm("push dx"); // multiply clobbers dx
                        assembly.AddAsm("mul bx");
                        assembly.AddAsm("pop dx");  // multiply clobbers dx
                        assembly.AddAsm("push ax");
                        break;

                    // AND
                    case 0x5F:
                        assembly.AddAsm("pop bx");
                        assembly.AddAsm("pop ax");
                        assembly.AddAsm("and ax, bx");
                        assembly.AddAsm("push ax");
                        break;

                    // OR
                    case 0x60:
                        assembly.AddAsm("pop bx");
                        assembly.AddAsm("pop ax");
                        assembly.AddAsm("or ax, bx");
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
                    case 0x72: LDSTR(assembly, pe.Metadata, code, ref i); break;

                    // LDFLD
                    case 0x7B: LDFLD(assembly, pe.Metadata, code, ref i); break;

                    // LDFLDA
                    case 0x7C: LDFLDA(assembly, pe.Metadata, code, ref i); break;

                    // STFLD
                    case 0x7D: STFLD(assembly, pe.Metadata, code, ref i); break;

                    // LDSFLD
                    case 0x7E: LDSFLD(assembly, pe.Metadata, code, ref i); break;

                    // LDSFLDA
                    case 0x7F: LDSFLDA(assembly, pe.Metadata, code, ref i); break;

                    // STSFLD
                    case 0x80: STSFLD(assembly, pe.Metadata, code, ref i); break;

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

                    default: throw new Exception($"Unknown IL opcode 0x{opcode.ToString("X")} at {label} in method {method.MethodDef.Name}");
                }

                // remove the label if no new assembly was added
                if (assembly.Assembly.Count == asmCount)
                {
                    //assembly.Assembly.RemoveAt(assembly.Assembly.Count - 1);
                    //assembly.AddAsm("nop");
                }
            }

            ProcessStaticConstructor(pe, methodDef);

            var methodsToCompile = _methodsToCompile.ToArray();
            _methodsToCompile.Clear();

            for (int i = 0; i < methodsToCompile.Length; i++)
            {
                Assemble(pe, methodsToCompile[i]);
            }
        }

        private void ProcessStaticConstructor(PortableExecutableFile pe, MethodDefLayout methodDef)
        {
            // find any static constructors for methods we are calling, and if necessary assemble them
            if (!_staticConstructors.ContainsKey(methodDef.Parent.FullName))
            {
                _staticConstructors.Add(methodDef.Parent.FullName, null);

                foreach (var childMethod in methodDef.Parent.Methods)
                {
                    if (childMethod.Name == ".cctor")
                    {
                        int methodIndex = _methods.Count;

                        Assemble(pe, childMethod);
                        _staticConstructors[methodDef.Parent.FullName] = _methods[methodIndex];
                    }
                }
            }
        }

        private void STFLD(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint fieldToken = BitConverter.ToUInt32(code, i);
            i += 4;

            int offset = _runtime.GetFieldOffset(metadata, fieldToken);

            assembly.AddAsm("pop ax");
            assembly.AddAsm("pop bx");
            if (offset == 0) assembly.AddAsm("mov [bx], ax");
            else assembly.AddAsm($"mov [bx + {offset}], ax");
        }

        private void LDSTR(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint metadataToken = BitConverter.ToUInt32(code, i);
            i += 4;

            string label = $"DB_{metadataToken.ToString("X")}";

            string s = Encoding.Unicode.GetString(metadata.GetMetadata(metadataToken));

            if (!_initializedData.ContainsKey(label)) _initializedData.Add(label, new DataType(ElementType.EType.String, s));
            assembly.AddAsm($"push {label}");
        }

        private void LDFLD(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint fieldToken = BitConverter.ToUInt32(code, i);
            i += 4;

            int offset = _runtime.GetFieldOffset(metadata, fieldToken);

            assembly.AddAsm("pop bx");
            if (offset == 0) assembly.AddAsm("mov ax, [bx]");
            else assembly.AddAsm($"mov ax, [bx + {offset}]");
            assembly.AddAsm("push ax");
        }

        private void LDFLDA(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint fieldToken = BitConverter.ToUInt32(code, i);
            i += 4;

            int offset = _runtime.GetFieldOffset(metadata, fieldToken);

            if (offset == 0) return;
            else
            {
                assembly.AddAsm("pop ax");
                assembly.AddAsm($"add ax, {offset}");
                assembly.AddAsm("push ax");
            }
        }

        private void STSFLD(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            int addr = BitConverter.ToInt32(code, i);
            i += 4;

            string label = $"DB_{addr.ToString("X")}";

            if (!_initializedData.ContainsKey(label)) AddStaticField(metadata, label, addr);

            assembly.AddAsm($"pop ax");
            assembly.AddAsm($"mov [{label}], ax");
        }

        private void LDSFLD(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            int addr = BitConverter.ToInt32(code, i);
            i += 4;

            string label = $"DB_{addr.ToString("X")}";

            if (!_initializedData.ContainsKey(label)) AddStaticField(metadata, label, addr);

            assembly.AddAsm($"mov ax, [{label}]");
            assembly.AddAsm($"push ax");
        }

        private void LDSFLDA(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            int addr = BitConverter.ToInt32(code, i);
            i += 4;

            string label = $"DB_{addr.ToString("X")}";

            if ((addr & 0xff000000) == 0x04000000)
            {
                var field = metadata.Fields[(addr & 0x00ffffff) - 1];

                if ((field.flags & FieldLayout.FieldLayoutFlags.Static) == FieldLayout.FieldLayoutFlags.Static)
                {
                    if (!_initializedData.ContainsKey(label)) AddStaticField(metadata, label, field.Type);
                }
                else
                {
                    throw new Exception("Incomplete implementation");
                }
            }
            else throw new Exception("Unexpected table found when trying to find a field.");

            assembly.AddAsm($"push {label}");
        }

        private void AddStaticField(CLIMetadata metadata, string label, ElementType type)
        {
            switch (type.Type)
            {
                case ElementType.EType.U1: _initializedData.Add(label, new DataType(type, (byte)0)); break;
                case ElementType.EType.I1: _initializedData.Add(label, new DataType(type, (sbyte)0)); break;
                case ElementType.EType.U2: _initializedData.Add(label, new DataType(type, (ushort)0)); break;
                case ElementType.EType.I2: _initializedData.Add(label, new DataType(type, (short)0)); break;
                case ElementType.EType.ValueType:
                    _initializedData.Add(label, new DataType(type, new byte[_runtime.GetTypeSize(metadata, type)])); 
                    break;
                default: throw new Exception("Unsupported type");
            }
        }

        private void AddStaticField(CLIMetadata metadata, string label, int fieldToken)
        {
            if ((fieldToken & 0xff000000) == 0x04000000)
            {
                var field = metadata.Fields[(fieldToken & 0x00ffffff) - 1];

                if ((field.flags & FieldLayout.FieldLayoutFlags.Static) == FieldLayout.FieldLayoutFlags.Static)
                {
                    AddStaticField(metadata, label, field.Type);
                }
                else
                {
                    throw new Exception("Incomplete implementation");
                }
            }
            else throw new Exception("Unexpected table found when trying to find a field.");
        }

        private List<MethodDefLayout> _methodsToCompile = new List<MethodDefLayout>();
        private bool _addedLoadDisk = false;
        private bool _addedDetectMemory = false;

        private void CALL(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint methodDesc = BitConverter.ToUInt32(code, i);
            i += 4;

            if ((methodDesc & 0xff000000) == 0x0a000000)
            {
                var memberRef = metadata.MemberRefs[(int)(methodDesc & 0x00ffffff) - 1];
                var memberName = memberRef.ToAsmString();

                if (memberName == "CPUHelper.Bios.WriteByte_Void_I4" || memberName == "CPUHelper.Bios.WriteByte_Void_U1")
                {
                    assembly.AddAsm("; Bios.WriteByte plug");
                    assembly.AddAsm("pop ax");
                    assembly.AddAsm("mov ah, 0x0e");
                    assembly.AddAsm("int 0x10");
                }
                else if (memberName == "CPUHelper.Bios.EnterProtectedMode_Void_ByRefValueType")
                {
                    assembly.AddAsm("; Bios.EnterProtectedMode plug");
                    assembly.AddAsm("pop bx");
                    assembly.AddAsm("mov [gdt_ptr + 2], bx");
                    assembly.AddAsm("cli");
                    assembly.AddAsm("lgdt [gdt_ptr]");
                    assembly.AddAsm("mov eax, cr0");
                    assembly.AddAsm("or eax, 0x1");
                    assembly.AddAsm("mov cr0, eax");
                    assembly.AddAsm("jmp 08h:0xA000");  // our 32 bit code starts at 0xA000, freshly loaded from the disk
                    assembly.AddAsm("");
                    assembly.AddAsm("gdt_ptr:");
                    assembly.AddAsm("dw 23");
                    assembly.AddAsm("dd 0; this gets filled in with bx, which is the address of the gdt object");
                }
                else if (memberName == "CPUHelper.CPU.ReadDX_U2")
                {
                    assembly.AddAsm("; CPUHelper.CPU.ReadDX_U2 plug");
                    assembly.AddAsm("push dx");
                }
                else if (memberName == "CPUHelper.CPU.ReadMem_U2_U2")
                {
                    assembly.AddAsm("; CPUHelper.CPU.ReadMem_U2_U2 plug");
                    assembly.AddAsm("pop bx");
                    assembly.AddAsm("mov ax, [bx]");
                    assembly.AddAsm("push ax");
                }
                else if (memberName == "CPUHelper.CPU.WriteMemory_Void_I4_I4")
                {
                    assembly.AddAsm("; CPUHelper.CPU.WriteMemory_Void_I4_I4 plug");
                    assembly.AddAsm("pop ax");
                    assembly.AddAsm("pop bx");
                    assembly.AddAsm("mov [bx], ax");
                }
                else if (memberName == "CPUHelper.CPU.Jump_Void_U4")
                {
                    assembly.AddAsm("; CPUHelper.CPU.Jump_Void_U4 plug");
                    assembly.AddAsm("pop ax");
                    assembly.AddAsm("jmp ax");
                }
                else if (memberName == "CPUHelper.Bios.EnableA20_Boolean")
                {
                    // from https://wiki.osdev.org/A20_Line
                    assembly.AddAsm("; CPUHelper.CPU.EnableA20_Boolean plug");
                    assembly.AddAsm("mov ax, 0x2403");
                    assembly.AddAsm("int 0x15");
                    assembly.AddAsm("jb bios_a20_failed");
                    assembly.AddAsm("cmp ah, 0");
                    assembly.AddAsm("jnz bios_a20_failed");

                    assembly.AddAsm("mov ax, 0x2402");
                    assembly.AddAsm("int 0x15");
                    assembly.AddAsm("jb bios_a20_failed");
                    assembly.AddAsm("cmp ah, 0");
                    assembly.AddAsm("jnz bios_a20_failed");

                    assembly.AddAsm("cmp al, 1");
                    assembly.AddAsm("jz bios_a20_activated");

                    assembly.AddAsm("mov ax, 0x2401");
                    assembly.AddAsm("int 0x15");
                    assembly.AddAsm("jb bios_a20_failed");
                    assembly.AddAsm("cmp ah, 0");
                    assembly.AddAsm("jnz bios_a20_failed");

                    assembly.AddAsm("bios_a20_activated:");
                    assembly.AddAsm("push 1");
                    assembly.AddAsm("jmp bios_a20_complete");

                    assembly.AddAsm("bios_a20_failed:");
                    assembly.AddAsm("push 0");

                    assembly.AddAsm("bios_a20_complete:");
                }
                else if (memberName == "CPUHelper.Bios.ResetDisk_Void")
                {
                    assembly.AddAsm("mov ah, 0x00; reset disk drive");
                    assembly.AddAsm("int 0x13");
                }
                else if (memberName == "CPUHelper.Bios.LoadDisk_U2_U1_U1_U1_U2_U2_U1_U1")
                {
                    assembly.AddAsm("call LoadDisk_U2_U2_U2_U1_U1");
                    assembly.AddAsm("push ax");

                    if (!_addedLoadDisk)
                    {
                        AssembledMethod loadDiskMethod = new AssembledMethod(null, null);
                        loadDiskMethod.AddAsm("; Bios.LoadDisk_U2_U2_U2_U1_U1 plug");
                        loadDiskMethod.AddAsm("LoadDisk_U2_U2_U2_U1_U1:");
                        loadDiskMethod.AddAsm("push bp");
                        loadDiskMethod.AddAsm("mov bp, sp");
                        loadDiskMethod.AddAsm("push cx");
                        loadDiskMethod.AddAsm("push dx");
                        loadDiskMethod.AddAsm("push es");

                        // bp + 4 is sectors to read
                        // bp + 6 is drive
                        // bp + 8 is lowAddr
                        // bp + 10 is hiAddr
                        // bp + 12 is sector
                        // bp + 14 is head
                        // bp + 16 is cylinder
                        loadDiskMethod.AddAsm("mov es, [bp + 10]");
                        loadDiskMethod.AddAsm("mov bx, [bp + 8]");

                        loadDiskMethod.AddAsm("mov ah, 0x02");
                        loadDiskMethod.AddAsm("mov al, [bp + 4]");

                        loadDiskMethod.AddAsm("mov cl, [bp + 12]"); // starting at sector 2, sector 1 is our boot sector and already in memory
                        loadDiskMethod.AddAsm("mov ch, [bp + 16]"); // first 8 bits of cylinder
                        loadDiskMethod.AddAsm("mov dh, [bp + 14]"); // head
                        loadDiskMethod.AddAsm("mov dl, [bp + 6]");  // drive number from bios

                        loadDiskMethod.AddAsm("int 0x13");
                        //loadDiskMethod.AddAsm("mov al, dl");

                        loadDiskMethod.AddAsm("jc LoadDisk_U2_U2_U2_U1_U1_Cleanup");
                        loadDiskMethod.AddAsm("mov ah, 0"); // al will now contain the number of sectors read

                        loadDiskMethod.AddAsm("LoadDisk_U2_U2_U2_U1_U1_Cleanup:");
                        loadDiskMethod.AddAsm("pop es");
                        loadDiskMethod.AddAsm("pop dx");
                        loadDiskMethod.AddAsm("pop cx");
                        loadDiskMethod.AddAsm("pop bp");
                        loadDiskMethod.AddAsm("ret 14");

                        _methods.Add(loadDiskMethod);
                        _addedLoadDisk = true;
                    }
                }
                else if (memberName == "CPUHelper.Bios.DetectMemory_U2_U2_ByRefValueType")
                {
                    assembly.AddAsm("call DetectMemory_U2_U2_ByRef");
                    assembly.AddAsm("push ax");

                    if (!_addedDetectMemory)
                    {
                        AssembledMethod detectMemMethod = new AssembledMethod(null, null);
                        detectMemMethod.AddAsm("; Bios.DetectMemory_U2_U2_ByRef plug");
                        detectMemMethod.AddAsm("DetectMemory_U2_U2_ByRef:");
                        detectMemMethod.AddAsm("push bp");
                        detectMemMethod.AddAsm("mov bp, sp");
                        detectMemMethod.AddAsm("push cx");
                        detectMemMethod.AddAsm("push dx");

                        // bp + 4 is SMAP_ret
                        // bp + 6 is address

                        detectMemMethod.AddAsm("mov di, [bp + 6]");
                        detectMemMethod.AddAsm("mov bx, [bp + 4]");
                        detectMemMethod.AddAsm("mov ebx, [bx + 4]");
                        detectMemMethod.AddAsm("mov edx, 0x534D4150");
                        detectMemMethod.AddAsm("mov ecx, 24");
                        detectMemMethod.AddAsm("mov eax, 0xE820");
                        detectMemMethod.AddAsm("int 0x15");

                        detectMemMethod.AddAsm("jc DetectMemory_U2_U2_ByRef_Error");
                        detectMemMethod.AddAsm("push ebx"); // this is the continuation
                        detectMemMethod.AddAsm("mov bx, [bp + 4]");
                        detectMemMethod.AddAsm("mov [bx], eax");    // al will now contain magic number

                        // now grab the continuation
                        detectMemMethod.AddAsm("mov ax, bx");
                        detectMemMethod.AddAsm("add ax, 4");
                        detectMemMethod.AddAsm("mov bx, ax");
                        detectMemMethod.AddAsm("pop eax");
                        detectMemMethod.AddAsm("mov [bx], eax");    // now we have the continuation as well

                        detectMemMethod.AddAsm("DetectMemory_U2_U2_ByRef_Cleanup:");
                        detectMemMethod.AddAsm("pop dx");
                        detectMemMethod.AddAsm("pop cx");
                        detectMemMethod.AddAsm("pop bp");
                        detectMemMethod.AddAsm("ret 4");

                        detectMemMethod.AddAsm("DetectMemory_U2_U2_ByRef_Error:");
                        detectMemMethod.AddAsm("mov ax, 0xff");
                        detectMemMethod.AddAsm("jmp DetectMemory_U2_U2_ByRef_Cleanup");

                        _methods.Add(detectMemMethod);
                        _addedDetectMemory = true;
                    }
                }
                else
                {
                    throw new Exception("Unable to handle this method");
                }
                assembly.AddAsm("; end plug");
            }
            else if ((methodDesc & 0xff000000) == 0x06000000)
            {
                var methodDef = metadata.MethodDefs[(int)(methodDesc & 0x00ffffff) - 1];
                var memberName = methodDef.ToAsmString();

                bool methodAlreadyCompiled = false;
                foreach (var method in _methods)
                    if (method.Method != null && method.Method.MethodDef.ToAsmString() == methodDef.ToAsmString())
                        methodAlreadyCompiled = true;

                if (!methodAlreadyCompiled)
                {
                    bool methodWaitingToCompile = false;
                    foreach (var method in _methodsToCompile)
                        if (method.ToAsmString() == methodDef.ToAsmString())
                            methodWaitingToCompile = true;
                    if (!methodWaitingToCompile) _methodsToCompile.Add(methodDef);
                }

                string callsite = methodDef.ToAsmString().Replace(".", "_");
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
                var memberName = memberRef.ToAsmString();

                if (memberName == "System.String.get_Chars_Char_I4")
                {
                    assembly.AddAsm("; System.String.get_Chars plug");
                    assembly.AddAsm("pop ax");  // pop index
                    assembly.AddAsm("pop bx");  // pop this
                    assembly.AddAsm("add ax, bx");
                    assembly.AddAsm("mov bx, ax");
                    assembly.AddAsm("mov ax, [bx]");
                    assembly.AddAsm("and ax, 255");
                    assembly.AddAsm("push ax");
                }
                else
                {
                    throw new Exception("Unable to handle this method");
                }
                assembly.AddAsm("; end plug");
            }
            else throw new Exception("Unhandled CALL target");
        }

        public List<string> WriteAssembly(uint offset = 0x7C00, uint size = 512, bool bootSector = false)
        {
            List<string> output = new List<string>();

            output.Add("[bits 16]");      // for bootsector code only
            output.Add($"[org 0x{offset.ToString("X")}]");   // for bootsector code only
            output.Add("");
            output.Add("    mov bp, 0x9000");
            output.Add("    mov sp, bp");

            if (_staticConstructors.Count > 0)
            {
                output.Add("; Call static constructors");
                foreach (var cctor in _staticConstructors)
                {
                    if (cctor.Value == null) continue;
                    output.Add($"    call {cctor.Value.Method.MethodDef.ToAsmString()}");
                }
            }

            foreach (var method in _methods)
            {
                if (method.Method != null) output.Add($"; Exporting assembly for method {method.Method.MethodDef}");
                foreach (var line in method.Assembly)
                {
                    if (!line.EndsWith(":") && !line.StartsWith("[")) output.Add("    " + line);
                    else output.Add(line);
                }
                output.Add("");
            }

            if (_initializedData.Count > 0)
            {
                output.Add("; Exporting initialized data");
                foreach (var data in _initializedData)
                {
                    if (data.Value.Type.Type == ElementType.EType.String)
                    {
                        output.Add($"{data.Key}:");
                        output.Add($"    db '{data.Value.Data}', 0");  // 0 for null termination after the string
                    }
                    else if (data.Value.Type.Type == ElementType.EType.I2)
                    {
                        output.Add($"{data.Key}:");
                        output.Add($"    dw {(short)data.Value.Data}");
                    }
                    else if (data.Value.Type.Type == ElementType.EType.U2)
                    {
                        output.Add($"{data.Key}:");
                        output.Add($"    dw {(ushort)data.Value.Data}");
                    }
                    else if (data.Value.Type.Type == ElementType.EType.U1)
                    {
                        output.Add($"{data.Key}:");
                        output.Add($"    db {(byte)data.Value.Data}");
                    }
                    else if (data.Value.Type.Type == ElementType.EType.I1)
                    {
                        output.Add($"{data.Key}:");
                        output.Add($"    db {(sbyte)data.Value.Data}");
                    }
                    else if (data.Value.Type.Type == ElementType.EType.ValueType)
                    {
                        StringBuilder sb = new StringBuilder();
                        output.Add($"{data.Key}:");
                        sb.Append($"    db ");
                        var bytes = (byte[])data.Value.Data;
                        for (int i = 0; i < bytes.Length; i++)
                        {
                            sb.Append(bytes[i]);
                            if (i != bytes.Length - 1) sb.Append(", ");
                        }
                        output.Add(sb.ToString());
                    }
                    else
                    {
                        throw new Exception("Unexpected type allocated as part of initial data");
                    }
                }
                output.Add("");

                // should only do this for boot sector attribute code
                if (bootSector)
                {
                    output.Add("times 510-($-$$) db 0");
                    output.Add("dw 0xaa55");
                }
                else
                {
                    output.Add($"times {size}-($-$$) db 0");
                }
            }

            return output;
        }
    }
}
