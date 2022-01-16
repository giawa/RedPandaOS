using PELoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;

namespace IL2Asm.Assembler.x86_RealMode
{
    public class Assembler : IAssembler
    {
        private Dictionary<string, AssembledMethod> _staticConstructors = new Dictionary<string, AssembledMethod>();
        private List<AssembledMethod> _methods = new List<AssembledMethod>();
        private Dictionary<string, DataType> _initializedData = new Dictionary<string, DataType>();
        private Runtime _runtime = new Runtime(BytesPerRegister);

        public List<AssembledMethod> Methods { get { return _methods; } }

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
            var assembly = new AssembledMethod(pe.Metadata, method, null);
            List<string> localVarNames = new List<string>();

            _methods.Add(assembly);
            Runtime.GlobalMethodCounter++;

            var code = method.Code;

            //if (_methods.Count > 1)
            {
                string label = methodDef.ToAsmString();
                if (_methods.Count > 1)
                {
                    assembly.AddAsm($"{label}:");
                    assembly.AddAsm("push bp");
                    assembly.AddAsm("mov bp, sp");
                }

                if (method.LocalVars != null)
                {
                    int localVarCount = method.LocalVars.LocalVariables.Length;

                    if (localVarCount > 0)
                    {
                        localVarNames.Add("cx");
                        if (_methods.Count > 1) assembly.AddAsm($"push {localVarNames[0]}");
                    }
                    if (localVarCount > 1)
                    {
                        localVarNames.Add("dx");
                        if (_methods.Count > 1) assembly.AddAsm($"push {localVarNames[1]}");
                    }
                    if (localVarCount > 2)
                    {
                        localVarNames.Add("di");
                        if (_methods.Count > 1) assembly.AddAsm($"push {localVarNames[2]}");
                    }
                }
            }

            if (method.LocalVars != null)
            {
                int localVarCount = method.LocalVars.LocalVariables.Length;
                if (localVarCount > localVarNames.Count)
                {
                    if (_methods.Count > 1) assembly.AddAsm($"sub sp, {BytesPerRegister * (localVarCount - localVarNames.Count)} ; {localVarCount} localvars");
                    else assembly.AddAsm($"sub sp, {BytesPerRegister * localVarCount} ; {localVarCount} localvars");
                }
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
                    case 0x06: LDLOC(0, assembly, localVarNames); break;
                    // LDLOC.1
                    case 0x07: LDLOC(1, assembly, localVarNames); break;
                    // LDLOC.2
                    case 0x08: LDLOC(2, assembly, localVarNames); break;
                    // LDLOC.3
                    case 0x09: LDLOC(3, assembly, localVarNames); break;

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
                    case 0x13: STLOC(code[i++], assembly, localVarNames); break;

                    // STLOC.0
                    case 0x0A: STLOC(0, assembly, localVarNames); break;
                    // STLOC.1
                    case 0x0B: STLOC(1, assembly, localVarNames); break;
                    // STLOC.2
                    case 0x0C: STLOC(2, assembly, localVarNames); break;
                    // STLOC.3
                    case 0x0D: STLOC(3, assembly, localVarNames); break;

                    // LDLOC.S
                    case 0x11: LDLOC(code[i++], assembly, localVarNames); break;

                    // LDLOCA.S
                    case 0x12:
                        _byte = code[i++];
                        /*if (_byte == 0) assembly.AddAsm("push 2");      // This is my made up address for CX
                        else if (_byte == 1) assembly.AddAsm("push 3"); // This is my made up address for DX
                        else
                        {
                            assembly.AddAsm("mov ax, bp");
                            assembly.AddAsm($"sub ax, {BytesPerRegister * (_byte + 1)}");
                            assembly.AddAsm("push ax");
                        }*/
                        assembly.AddAsm("mov ax, bp");
                        assembly.AddAsm($"sub ax, {BytesPerRegister * (_byte + 1)}");
                        assembly.AddAsm("push ax");
                        break;

                    // LDNULL
                    case 0x14: assembly.AddAsm("push 0"); break;

                    // LDC.I4.1
                    case 0x15: assembly.AddAsm("push -1"); break;
                    case 0x16: assembly.AddAsm("push 0"); break;
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

                    case 0x28: CALL(pe, assembly, pe.Metadata, code, ref i); break;
                    case 0x6F: CALLVIRT(assembly, pe.Metadata, code, ref i, localVarNames); break;

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
                            for (int p = localVarNames.Count; p < localVarCount; p++)
                                assembly.AddAsm("pop bx; localvar that was pushed on stack");
                            for (int p = localVarNames.Count - 1; p >= 0; p--)
                                assembly.AddAsm($"pop {localVarNames[p]}");
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
                        assembly.AddAsm("cmp ax, 0");    // compare values
                        assembly.AddAsm($"je {_jmpLabel}");
                        break;

                    // BRTRUE
                    case 0x3A:
                        _int = BitConverter.ToInt32(code, i);
                        i += 4;

                        _jmpLabel = $"IL_{(i + _int).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop ax");        // value2
                        assembly.AddAsm("cmp ax, 0");    // compare values
                        assembly.AddAsm($"jne {_jmpLabel}");
                        break;

                    // BGT
                    case 0x3D:
                        _int = BitConverter.ToInt32(code, i);
                        i += 4;

                        _jmpLabel = $"IL_{(i + _int).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop ax");        // value2
                        assembly.AddAsm("pop bx");        // value1
                        assembly.AddAsm("cmp bx, ax");    // compare values
                        assembly.AddAsm($"jg {_jmpLabel}");
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

                    // BNE.UN
                    case 0x40:
                        _int = BitConverter.ToInt32(code, i);
                        i += 4;
                        Branch(i + _int, assembly, pe.Metadata, "jne", "jne");
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

                    // SHL
                    case 0x62:
                        assembly.AddAsm("mov bx, cx");  // cx is needed to access cl for variable shift, so store is in bx temporarily
                        assembly.AddAsm("pop cx");      // get amount to shift
                        assembly.AddAsm("pop ax");
                        assembly.AddAsm("sal ax, cl");
                        assembly.AddAsm("mov cx, bx");  // restore cx
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

            while (_methodsToCompile.Count > 0)
            {
                var m = _methodsToCompile[0];
                _methodsToCompile.RemoveAt(0);
                Assemble(pe, m);
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

        private void Branch(int jmpTo, AssembledMethod assembly, CLIMetadata metadata, string asm32jmpType, string asmR4jmpType)
        {
            _jmpLabel = $"IL_{jmpTo.ToString("X4")}_{Runtime.GlobalMethodCounter}";

            assembly.AddAsm("pop ax");        // value2
            assembly.AddAsm("pop bx");        // value1
            assembly.AddAsm("cmp bx, ax");    // compare values
            assembly.AddAsm($"{asm32jmpType} {_jmpLabel}");
        }

        private void STLOC(byte b, AssembledMethod assembly, List<string> localVarNames)
        {
            assembly.AddAsm("pop ax");
            if (b < localVarNames.Count) assembly.AddAsm($"mov {localVarNames[b]}, ax");
            else assembly.AddAsm($"mov [bp - {(b + 1) * BytesPerRegister}], ax");
        }

        private void LDLOC(byte b, AssembledMethod assembly, List<string> localVarNames)
        {
            if (b < localVarNames.Count) assembly.AddAsm($"mov ax, {localVarNames[b]}");
            else assembly.AddAsm($"mov ax, [bp - {(b + 1) * BytesPerRegister}]");
            assembly.AddAsm("push ax");
        }

        private void STFLD(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint fieldToken = BitConverter.ToUInt32(code, i);
            i += 4;

            int offset = _runtime.GetFieldOffset(metadata, fieldToken);
            int size = _runtime.GetTypeSize(metadata, _runtime.GetFieldType(metadata, fieldToken));

            assembly.AddAsm("pop ax");
            assembly.AddAsm("pop bx");

            if (size == 2)
            {
                if (offset == 0) assembly.AddAsm("mov [bx], ax");
                else assembly.AddAsm($"mov [bx + {offset}], ax");
            }
            else if (size == 1)
            {
                if (offset == 0) assembly.AddAsm("mov byte [bx], al");
                else assembly.AddAsm($"mov byte [bx + {offset}], al");
            }
            else throw new Exception("Unsupported type");
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
            int size = _runtime.GetTypeSize(metadata, _runtime.GetFieldType(metadata, fieldToken));

            assembly.AddAsm("pop bx");
            if (size == 2)
            {
                if (offset == 0) assembly.AddAsm("mov ax, [bx]");
                else assembly.AddAsm($"mov ax, [bx + {offset}]");
            }
            else if (size == 1)
            {
                assembly.AddAsm("xor ax, ax");
                if (offset == 0) assembly.AddAsm("mov al, [bx]");
                else assembly.AddAsm($"mov al, [bx + {offset}]");
            }
            else throw new Exception("Unsupported type");
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

        private Dictionary<string, Assembly> _loadedAssemblies = new Dictionary<string, Assembly>();

        private bool CheckForPlugAndInvoke(string dllPath, string memberName, AssembledMethod assembly)
        {
            if (dllPath.Contains("System.dll")) dllPath = $"{Environment.CurrentDirectory}\\RedPandaOS.dll";

            if (File.Exists(dllPath))
            {
                if (!_loadedAssemblies.ContainsKey(dllPath)) _loadedAssemblies[dllPath] = Assembly.LoadFile(dllPath);

                var possiblePlugs = _loadedAssemblies[dllPath].GetTypes().
                    SelectMany(t => t.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)).
                    Where(m => m.GetCustomAttribute<BaseTypes.AsmPlugAttribute>()?.AsmMethodName == memberName &&
                               m.GetCustomAttribute<BaseTypes.AsmPlugAttribute>()?.Architecture == BaseTypes.Architecture.X86_Real).ToArray();

                if (possiblePlugs.Length == 1)
                {
                    var asmFlags = possiblePlugs[0].GetCustomAttribute<BaseTypes.AsmPlugAttribute>().Flags;

                    if ((asmFlags & BaseTypes.AsmFlags.Inline) != 0)
                    {
                        possiblePlugs[0].Invoke(null, new object[] { assembly });
                    }
                    else
                    {
                        var name = memberName.Replace(".", "_").Replace("<", "_").Replace(">", "_");
                        AssembledMethod plugMethod = new AssembledMethod(assembly.Metadata, null);
                        plugMethod.AddAsm($"{name}:");
                        possiblePlugs[0].Invoke(null, new object[] { plugMethod });
                        _methods.Add(plugMethod);
                        assembly.AddAsm($"call {name}");
                        assembly.AddAsm("push ax");
                    }

                    return true;
                }
            }

            return false;
        }

        private void CALL(PortableExecutableFile pe, AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint methodDesc = BitConverter.ToUInt32(code, i);
            i += 4;

            MethodSpecLayout methodSpec = null;

            if ((methodDesc & 0xff000000) == 0x2b000000)
            {
                methodSpec = metadata.MethodSpecs[(int)(methodDesc & 0x00ffffff) - 1];
                methodDesc = methodSpec.method;

                // use the parent methodSpec to work out the types
                if (assembly.MethodSpec != null)
                {
                    methodSpec = methodSpec.Clone();
                    for (int j = 0; j < methodSpec.MemberSignature.Types.Length; j++)
                    {
                        var type = methodSpec.MemberSignature.Types[j];

                        if (type.Type == ElementType.EType.MVar)
                        {
                            var token = methodSpec.MemberSignature.Types[j].Token;
                            methodSpec.MemberSignature.Types[j] = assembly.MethodSpec.MemberSignature.Types[token];
                            methodSpec.MemberSignature.TypeNames[j] = assembly.MethodSpec.MemberSignature.TypeNames[token];
                        }
                    }
                }
            }

            if ((methodDesc & 0xff000000) == 0x0a000000)
            {
                var memberRef = metadata.MemberRefs[(int)(methodDesc & 0x00ffffff) - 1];
                var memberName = memberRef.ToAsmString();

                assembly.AddAsm($"; start {memberName} plug");

                var file = memberName.Substring(0, memberName.IndexOf('.'));
                var path = $"{Environment.CurrentDirectory}\\{file}.dll";

                if (!CheckForPlugAndInvoke(path, memberName, assembly))
                {
                    throw new Exception("Unable to handle this method");
                }
                assembly.AddAsm("; end plug");
            }
            else if ((methodDesc & 0xff000000) == 0x06000000)
            {
                var methodDef = metadata.MethodDefs[(int)(methodDesc & 0x00ffffff) - 1];
                var memberName = methodDef.ToAsmString();

                if (!CheckForPlugAndInvoke(pe.Filename, memberName, assembly))
                {
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
            }
            else throw new Exception("Unhandled CALL target");
        }

        private void CALLVIRT(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i, List<string> localVarNames)
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
                    if (localVarNames.Contains("si")) assembly.AddAsm("mov ax, si");
                    assembly.AddAsm("pop si");  // pop index
                    assembly.AddAsm("pop bx");  // pop this
                    //assembly.AddAsm("add ax, bx");
                    assembly.AddAsm("lea bx, [bx + si]");
                    //assembly.AddAsm("mov bx, ax");
                    if (localVarNames.Contains("si")) assembly.AddAsm("mov si, ax");  // recover si
                    assembly.AddAsm("xor ax, ax");
                    assembly.AddAsm("mov al, [bx]");
                    //assembly.AddAsm("and ax, 255");
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

        public List<string> WriteAssembly(uint offset = 0x7C00, uint size = 512)
        {
            List<string> output = new List<string>();

            output.Add("[bits 16]");      // for bootsector code only
            output.Add($"[org 0x{offset.ToString("X")}]");   // for bootsector code only
            output.Add("");
            output.Add("    xor ax, ax");
            output.Add("    mov ds, ax");
            output.Add("    mov es, ax");
            output.Add("    mov ss, ax");
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
                        var s = (string)data.Value.Data;
                        output.Add($"{data.Key}:");
                        StringBuilder sb = new StringBuilder();
                        for (int i = 0; i < s.Length; i++) sb.Append($"{(int)s[i]}, ");

                        output.Add($"    db {sb.ToString()} 0 ; {s}");  // 0 for null termination after the string
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
            }

            if (size != 0)
            {
                output.Add($"times {size}-($-$$) db 0");
            }

            return output;
        }
    }
}
