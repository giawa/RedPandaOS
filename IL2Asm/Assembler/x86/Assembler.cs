using PELoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IL2Asm.Assembler.x86
{
    public class Assembler : IAssembler
    {
        private Dictionary<string, AssembledMethod> _staticConstructors = new Dictionary<string, AssembledMethod>();
        private List<AssembledMethod> _methods = new List<AssembledMethod>();
        private Dictionary<string, object> _initializedData = new Dictionary<string, object>();
        private Runtime _runtime = new Runtime();

        public const int BytesPerRegister = 4;

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

            var method = new MethodHeader(pe.Memory, pe.Metadata, methodDef);
            var assembly = new AssembledMethod(pe.Metadata, method);
            foreach (var m in _methods) if (m != null && m.Method.MethodDef.ToAsmString() == methodDef.ToAsmString()) return;
            _methods.Add(assembly);
            Runtime.GlobalMethodCounter++;

            var code = method.Code;
            int localVarOffset = 0;

            if (_methods.Count > 1)
            {
                string label = methodDef.ToAsmString();
                assembly.AddAsm($"{label}:");
                assembly.AddAsm("push ebp");
                assembly.AddAsm("mov ebp, esp");

                if (method.LocalVars != null)
                {
                    int localVarCount = method.LocalVars.LocalVariables.Length;
                    if (localVarCount > 0)
                    {
                        assembly.AddAsm("push ecx; localvar.1");
                        assembly.AddAsm("mov ecx, 0");
                        localVarOffset = BytesPerRegister;
                    }
                    if (localVarCount > 1)
                    {
                        assembly.AddAsm("push edx; localvar.2");
                        assembly.AddAsm("mov edx, 0");
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
                        assembly.AddAsm($"mov eax, [ebp + {BytesPerRegister * (1 + _uint)}]");
                        assembly.AddAsm("push eax");
                        break;
                    // LDARG.1
                    case 0x03:
                        _uint = method.MethodDef.MethodSignature.ParamCount - 1;
                        assembly.AddAsm($"mov eax, [ebp + {BytesPerRegister * (1 + _uint)}]");
                        assembly.AddAsm("push eax");
                        break;
                    // LDARG.2
                    case 0x04:
                        _uint = method.MethodDef.MethodSignature.ParamCount - 2;
                        assembly.AddAsm($"mov eax, [ebp + {BytesPerRegister * (1 + _uint)}]");
                        assembly.AddAsm("push eax");
                        break;
                    // LDARG.3
                    case 0x05:
                        _uint = method.MethodDef.MethodSignature.ParamCount - 3;
                        assembly.AddAsm($"mov eax, [ebp + {BytesPerRegister * (1 + _uint)}]");
                        assembly.AddAsm("push eax");
                        break;

                    // LDLOC.0
                    case 0x06:
                        assembly.AddAsm("push ecx");
                        break;
                    // LDLOC.1
                    case 0x07:
                        assembly.AddAsm("push edx");
                        break;
                    // LDLOC.2
                    case 0x08:
                        assembly.AddAsm($"mov eax, [ebp - {localVarOffset + BytesPerRegister}]");
                        assembly.AddAsm("push eax");
                        break;
                    // LDLOC.3
                    case 0x09:
                        assembly.AddAsm($"mov eax, [ebp - {localVarOffset + 2 * BytesPerRegister}]");
                        assembly.AddAsm("push eax");
                        break;

                    // STARG.S
                    case 0x10:
                        _byte = code[i++];
                        _uint = method.MethodDef.MethodSignature.ParamCount - _byte;
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm($"mov [ebp + {BytesPerRegister * (1 + _uint)}], eax");
                        break;

                    // STLOC.S
                    case 0x13:
                        _byte = code[i++];
                        if (_byte == 0) assembly.AddAsm("pop ecx");
                        else if (_byte == 1) assembly.AddAsm("pop edx");
                        else
                        {
                            assembly.AddAsm("pop eax");
                            assembly.AddAsm($"mov [ebp - {BytesPerRegister * (_byte + 1)}], eax");
                        }
                        break;

                    // LDC.I4.0
                    case 0x16:
                        assembly.AddAsm("push 0");
                        break;

                    // STLOC.0
                    case 0x0A:
                        assembly.AddAsm("pop ecx");
                        break;
                    // STLOC.1
                    case 0x0B:
                        assembly.AddAsm("pop edx");
                        break;
                    // STLOC.2
                    case 0x0C:
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm($"mov [ebp - {localVarOffset + BytesPerRegister}], eax");
                        break;
                    // STLOC.3
                    case 0x0D:
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm($"mov [ebp - {localVarOffset + 2 * BytesPerRegister}], eax");
                        break;

                    // LDLOC.S
                    case 0x11:
                        _byte = code[i++];
                        if (_byte == 0) assembly.AddAsm("push ecx");
                        else if (_byte == 1) assembly.AddAsm("push edx");
                        else
                        {
                            assembly.AddAsm($"mov eax, [ebp - {BytesPerRegister * (_byte + 1)}]");
                            assembly.AddAsm("push eax");
                        }
                        break;

                    // LDLOCA.S
                    case 0x12:
                        _byte = code[i++];
                        if (_byte == 0) assembly.AddAsm("push 2");      // This is my made up address for ECX
                        else if (_byte == 1) assembly.AddAsm("push 3"); // This is my made up address for EDX
                        else
                        {
                            assembly.AddAsm("mov eax, bp");
                            assembly.AddAsm($"sub eax, {BytesPerRegister * (_byte + 1)}");
                            assembly.AddAsm("push eax");
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
                        i += 4;
                        assembly.AddAsm($"push {value}");
                        break;

                    // DUP
                    case 0x25:
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("push eax");
                        assembly.AddAsm("push eax");
                        break;

                    // POP
                    case 0x26:
                        assembly.AddAsm("pop eax");
                        break;

                    case 0x28: CALL(assembly, pe.Metadata, code, ref i); break;
                    case 0x6F: CALLVIRT(assembly, pe.Metadata, code, ref i); break;

                    // RET
                    case 0x2A:
                        // place the returned value on ax, which should clear our CLI stack
                        if (method.MethodDef.MethodSignature.RetType != null && method.MethodDef.MethodSignature.RetType.Type != ElementType.EType.Void)
                        {
                            assembly.AddAsm("pop eax; return value");
                        }

                        // pop any local variables we pushed at the start
                        if (method.LocalVars != null)
                        {
                            int localVarCount = method.LocalVars.LocalVariables.Length;
                            for (int p = 2; p < localVarCount; p++)
                                assembly.AddAsm("pop ebx; localvar that was pushed on stack");
                            if (localVarCount > 1) assembly.AddAsm("pop edx; localvar.1");
                            if (localVarCount > 0) assembly.AddAsm("pop ecx; localvar.0");
                        }

                        int bytes = (int)methodDef.MethodSignature.ParamCount * BytesPerRegister;
                        assembly.AddAsm("pop ebp");
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
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("cmp eax, 0");
                        assembly.AddAsm($"je {_jmpLabel}");
                        break;

                    // BRTRUE.S
                    case 0x2D:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("cmp eax, 0");
                        assembly.AddAsm($"jne {_jmpLabel}");
                        break;

                    // BEQ.S
                    case 0x2E:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop eax");        // value2
                        assembly.AddAsm("pop ebx");        // value1
                        assembly.AddAsm("cmp ebx, eax");    // compare values
                        assembly.AddAsm($"je {_jmpLabel}");
                        break;

                    // BGE.S
                    case 0x2F:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop eax");        // value2
                        assembly.AddAsm("pop ebx");        // value1
                        assembly.AddAsm("cmp ebx, eax");    // compare values
                        assembly.AddAsm($"jge {_jmpLabel}");
                        break;

                    // BGT.S
                    case 0x30:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop eax");        // value2
                        assembly.AddAsm("pop ebx");        // value1
                        assembly.AddAsm("cmp ebx, eax");    // compare values
                        assembly.AddAsm($"jg {_jmpLabel}");
                        break;

                    // BLE.S
                    case 0x31:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop eax");        // value2
                        assembly.AddAsm("pop ebx");        // value1
                        assembly.AddAsm("cmp ebx, eax");    // compare values
                        assembly.AddAsm($"jle {_jmpLabel}");
                        break;

                    // BLT.S
                    case 0x32:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop eax");        // value2
                        assembly.AddAsm("pop ebx");        // value1
                        assembly.AddAsm("cmp ebx, eax");    // compare values
                        assembly.AddAsm($"jl {_jmpLabel}");
                        break;

                    // BNE.UN.S
                    case 0x33:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop eax");        // value2
                        assembly.AddAsm("pop ebx");        // value1
                        assembly.AddAsm("cmp ebx, eax");    // compare values
                        assembly.AddAsm($"jne {_jmpLabel}");
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
                        assembly.AddAsm("pop eax");        // value2
                        assembly.AddAsm("cmp ebx, 0");    // compare values
                        assembly.AddAsm($"je {_jmpLabel}");
                        break;

                    // BRTRUE
                    case 0x3A:
                        _int = BitConverter.ToInt32(code, i);
                        i += 4;

                        _jmpLabel = $"IL_{(i + _int).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop eax");        // value2
                        assembly.AddAsm("cmp ebx, 0");    // compare values
                        assembly.AddAsm($"jne {_jmpLabel}");
                        break;

                    // BLT
                    case 0x3F:
                        _int = BitConverter.ToInt32(code, i);
                        i += 4;

                        _jmpLabel = $"IL_{(i + _int).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop eax");        // value2
                        assembly.AddAsm("pop ebx");        // value1
                        assembly.AddAsm("cmp ebx, eax");    // compare values
                        assembly.AddAsm($"jl {_jmpLabel}");
                        break;

                    // ADD
                    case 0x58:
                        assembly.AddAsm("pop ebx");
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("add eax, ebx");
                        assembly.AddAsm("push eax");
                        break;

                    // SUB
                    case 0x59:
                        assembly.AddAsm("pop ebx");
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("sub eax, ebx");
                        assembly.AddAsm("push eax");
                        break;

                    // MUL
                    case 0x5A:
                        assembly.AddAsm("pop ebx");
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("push edx"); // multiply clobbers edx
                        assembly.AddAsm("mul ebx");
                        assembly.AddAsm("pop edx");  // multiply clobbers edx
                        assembly.AddAsm("push eax");
                        break;

                    // AND
                    case 0x5F:
                        assembly.AddAsm("pop ebx");
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("and eax, ebx");
                        assembly.AddAsm("push eax");
                        break;

                    // OR
                    case 0x60:
                        assembly.AddAsm("pop ebx");
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("or eax, ebx");
                        assembly.AddAsm("push eax");
                        break;

                    // SHL
                    case 0x62:
                        assembly.AddAsm("mov ebx, ecx");  // cx is needed to access cl for variable shift, so store is in bx temporarily
                        assembly.AddAsm("pop ecx");      // get amount to shift
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("sal eax, cl");
                        assembly.AddAsm("mov ecx, ebx");  // restore cx
                        assembly.AddAsm("push eax");
                        break;

                    // SHR
                    case 0x63:
                        assembly.AddAsm("mov ebx, ecx");  // cx is needed to access cl for variable shift, so store is in bx temporarily
                        assembly.AddAsm("pop ecx");      // get amount to shift
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("sar eax, cl");
                        assembly.AddAsm("mov ecx, ebx");  // restore cx
                        assembly.AddAsm("push eax");
                        break;

                    // SHR.UN
                    case 0x64:
                        assembly.AddAsm("mov ebx, ecx");  // cx is needed to access cl for variable shift, so store is in bx temporarily
                        assembly.AddAsm("pop ecx");      // get amount to shift
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("shr eax, cl");
                        assembly.AddAsm("mov ecx, ebx");  // restore cx
                        assembly.AddAsm("push eax");
                        break;

                    // CONV.I2
                    case 0x68:
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("and eax, 65535");
                        assembly.AddAsm("push eax");
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
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("and eax, 65535");
                        assembly.AddAsm("push eax");
                        break;

                    // CONV.U1
                    case 0xD2:
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("and eax, 255");
                        assembly.AddAsm("push eax");
                        break;

                    // CEQ
                    case 0xFE01:
                        assembly.AddAsm("pop eax");       // value2
                        assembly.AddAsm("pop ebx");       // value1
                        assembly.AddAsm("cmp eax, ebx");  // compare values
                        assembly.AddAsm("lahf");          // load flags into ah
                        assembly.AddAsm("shr eax, 14");   // push the 1 into the LSB
                        assembly.AddAsm("and eax, 1");    // push the 1 into the LSB
                        assembly.AddAsm("push eax");
                        break;

                    // CGT
                    case 0xFE02:
                        assembly.AddAsm("pop eax");       // value2
                        assembly.AddAsm("pop ebx");       // value1
                        assembly.AddAsm("cmp eax, ebx");  // compare values
                        assembly.AddAsm("lahf");          // load flags into ah
                        assembly.AddAsm("shr eax, 15");   // push the 1 into the LSB
                        assembly.AddAsm("and eax, 1");    // push the 1 into the LSB
                        assembly.AddAsm("push eax");
                        break;

                    // CGT.UN (identical to CGT for now)
                    case 0xFE03:
                        assembly.AddAsm("pop eax");       // value2
                        assembly.AddAsm("pop ebx");       // value1
                        assembly.AddAsm("cmp eax, ebx");  // compare values
                        assembly.AddAsm("lahf");          // load flags into ah
                        assembly.AddAsm("shr eax, 15");   // push the 1 into the LSB
                        assembly.AddAsm("and eax, 1");    // push the 1 into the LSB
                        assembly.AddAsm("push eax");
                        break;

                    // CLT
                    case 0xFE04:
                        assembly.AddAsm("pop eax");       // value2
                        assembly.AddAsm("pop ebx");       // value1
                        assembly.AddAsm("cmp ebx, eax");  // compare values
                        assembly.AddAsm("lahf");          // load flags into ah
                        assembly.AddAsm("shr eax, 15");   // push the 1 into the LSB
                        assembly.AddAsm("and eax, 1");    // push the 1 into the LSB
                        assembly.AddAsm("push eax");
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
                        Console.WriteLine("Add constructor");

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

            assembly.AddAsm("pop eax");
            assembly.AddAsm("pop ebx");
            if (offset == 0) assembly.AddAsm("mov [ebx], eax");
            else assembly.AddAsm($"mov [ebx + {offset}], eax");
        }

        private void LDSTR(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint metadataToken = BitConverter.ToUInt32(code, i);
            i += 4;

            string label = $"DB_{metadataToken.ToString("X")}";

            string s = Encoding.Unicode.GetString(metadata.GetMetadata(metadataToken));

            if (!_initializedData.ContainsKey(label)) _initializedData.Add(label, s);
            assembly.AddAsm($"push {label}");
        }

        private void LDFLD(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint fieldToken = BitConverter.ToUInt32(code, i);
            i += 4;

            int offset = _runtime.GetFieldOffset(metadata, fieldToken);

            assembly.AddAsm("pop ebx");
            if (offset == 0) assembly.AddAsm("mov eax, [ebx]");
            else assembly.AddAsm($"mov eax, [ebx + {offset}]");
            assembly.AddAsm("push eax");
        }

        private void LDFLDA(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint fieldToken = BitConverter.ToUInt32(code, i);
            i += 4;

            int offset = _runtime.GetFieldOffset(metadata, fieldToken);

            if (offset == 0) return;
            else
            {
                assembly.AddAsm("pop eax");
                assembly.AddAsm($"add eax, {offset}");
                assembly.AddAsm("push eax");
            }
        }

        private void STSFLD(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            int addr = BitConverter.ToInt32(code, i);
            i += 4;

            string label = $"DB_{addr.ToString("X")}";

            if (!_initializedData.ContainsKey(label)) AddStaticField(metadata, label, addr);

            assembly.AddAsm($"pop eax");
            assembly.AddAsm($"mov [{label}], eax");
        }

        private void LDSFLD(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            int addr = BitConverter.ToInt32(code, i);
            i += 4;

            string label = $"DB_{addr.ToString("X")}";

            if (!_initializedData.ContainsKey(label)) AddStaticField(metadata, label, addr);

            assembly.AddAsm($"mov eax, [{label}]");
            assembly.AddAsm($"push eax");
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
            if (_initializedData.ContainsKey(label)) return;

            switch (type.Type)
            {
                case ElementType.EType.U1: _initializedData.Add(label, (byte)0); break;
                case ElementType.EType.I1: _initializedData.Add(label, (sbyte)0); break;
                case ElementType.EType.U2: _initializedData.Add(label, (ushort)0); break;
                case ElementType.EType.I2: _initializedData.Add(label, (short)0); break;
                case ElementType.EType.U4: _initializedData.Add(label, (uint)0); break;
                case ElementType.EType.I4: _initializedData.Add(label, (int)0); break;
                case ElementType.EType.ValueType: _initializedData.Add(label, new byte[_runtime.GetTypeSize(metadata, type)]); break;
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

        private void CALL(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint methodDesc = BitConverter.ToUInt32(code, i);
            i += 4;
            string generic = string.Empty;

            if ((methodDesc & 0xff000000) == 0x2b000000)
            {
                var methodSpec = metadata.MethodSpecs[(int)(methodDesc & 0x00ffffff) - 1];
                methodDesc = methodSpec.method;
                generic = methodSpec.MemberSignature.ToAsmString();
            }

            if ((methodDesc & 0xff000000) == 0x0a000000)
            {
                var memberRef = metadata.MemberRefs[(int)(methodDesc & 0x00ffffff) - 1];
                var memberName = memberRef.ToAsmString();
                if (!string.IsNullOrEmpty(generic)) memberName = memberName.Substring(0, memberName.IndexOf("_")) + generic + memberName.Substring(memberName.IndexOf("_"));

                if (memberName == "CPUHelper.CPU.WriteMemory_Void_I4_I4")
                {
                    assembly.AddAsm("pop eax"); // character
                    assembly.AddAsm("pop ebx"); // address
                    assembly.AddAsm("mov [ebx], ax");
                }
                else if (memberName == "CPUHelper.CPU.OutDxAl_Void_U2_U1")
                {
                    assembly.AddAsm("pop eax"); // al
                    assembly.AddAsm("pop ebx"); // dx
                    assembly.AddAsm("push edx");
                    assembly.AddAsm("mov edx, ebx");
                    assembly.AddAsm("out dx, al");
                    assembly.AddAsm("pop edx");
                }
                else if (memberName == "CPUHelper.CPU.InDx_U1_U2")
                {
                    assembly.AddAsm("pop eax"); // address
                    assembly.AddAsm("push edx");
                    assembly.AddAsm("mov edx, eax");
                    assembly.AddAsm("in al, dx");
                    assembly.AddAsm("pop edx");
                    assembly.AddAsm("push eax");
                }
                else if (memberName == "CPUHelper.CPU.ReadCR0_U4")
                {
                    assembly.AddAsm("mov eax, cr0");
                    assembly.AddAsm("push eax");
                }
                else if (memberName == "CPUHelper.CPU.ReadMem_U2_U2")
                {
                    assembly.AddAsm("; CPUHelper.CPU.ReadMem_U2_U2 plug");
                    assembly.AddAsm("pop ebx");
                    assembly.AddAsm("mov ax, [bx]");
                    assembly.AddAsm("and eax, 65535");
                    assembly.AddAsm("push eax");
                }
                else if (memberName == "CPUHelper.CPU.ReadMemByte_U1_U2")
                {
                    assembly.AddAsm("; CPUHelper.CPU.ReadMem_U2_U2 plug");
                    assembly.AddAsm("pop ebx");
                    assembly.AddAsm("mov ax, [bx]");
                    assembly.AddAsm("and eax, 255");
                    assembly.AddAsm("push eax");
                }
                else if (memberName == "CPUHelper.CPU.CopyByte<SMAP_entry>_Void_U4_U4_ByRef")
                {
                    assembly.AddAsm("; CPUHelper.CPU.CopyByte plug");
                    assembly.AddAsm("push ecx");

                    assembly.AddAsm("mov eax, [esp + 16]");
                    assembly.AddAsm("add eax, [esp + 12]"); // source + sourceOffset
                    assembly.AddAsm("mov ebx, eax");
                    assembly.AddAsm("mov al, [ebx]");       // read source
                    assembly.AddAsm("mov cl, al");

                    assembly.AddAsm("mov eax, [esp + 8]");
                    assembly.AddAsm("add eax, [esp + 4]"); // dest + destOffset
                    assembly.AddAsm("mov ebx, eax");
                    assembly.AddAsm("mov [ebx], cl");       // copy source to destination

                    assembly.AddAsm("pop ecx");
                    assembly.AddAsm("pop eax");
                    assembly.AddAsm("pop eax");
                    assembly.AddAsm("pop eax");
                    assembly.AddAsm("pop eax");
                }
                else throw new Exception("Unable to handle this method");
            }
            else if ((methodDesc & 0xff000000) == 0x06000000)
            {
                var methodDef = metadata.MethodDefs[(int)(methodDesc & 0x00ffffff) - 1];
                var memberName = methodDef.ToAsmString();
                if (!string.IsNullOrEmpty(generic)) memberName = memberName.Substring(0, memberName.IndexOf("_")) + generic + memberName.Substring(memberName.IndexOf("_"));

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

                if (methodDef.MethodSignature.RetType != null && methodDef.MethodSignature.RetType.Type != ElementType.EType.Void) assembly.AddAsm("push eax");
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
                    assembly.AddAsm("pop eax");  // pop index
                    assembly.AddAsm("pop ebx");  // pop this
                    assembly.AddAsm("add eax, ebx");
                    assembly.AddAsm("mov ebx, eax");
                    assembly.AddAsm("mov eax, [ebx]");
                    assembly.AddAsm("and eax, 255");
                    assembly.AddAsm("push eax");
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
                stream.WriteLine("[bits 32]");
                stream.WriteLine("[org 0x9000]");   // for bootsector code only
                stream.WriteLine("enter_pm:");
                stream.WriteLine("mov ax, 16");
                stream.WriteLine("mov ds, ax");
                stream.WriteLine("mov ss, ax");
                stream.WriteLine("mov es, ax");
                stream.WriteLine("mov fs, ax");
                stream.WriteLine("mov gs, ax");
                stream.WriteLine("mov ebp, 0x9000 ; reset the stack");
                stream.WriteLine("mov esp, ebp");
                stream.WriteLine();

                // draw a character in video memory
                /*stream.WriteLine("mov edx, 0xb8000");
                stream.WriteLine("mov al, 48");
                stream.WriteLine("mov ah, 0x01");
                stream.WriteLine("mov [edx], ax");*/

                if (_staticConstructors.Count > 0)
                {
                    stream.WriteLine("; Call static constructors");
                    foreach (var cctor in _staticConstructors)
                    {
                        if (cctor.Value == null) continue;
                        string callsite = cctor.Key.Replace(".", "_");
                        stream.WriteLine($"    call {cctor.Value.Method.MethodDef.ToAsmString()}");
                    }
                }

                foreach (var method in _methods)
                {
                    if (method.Method != null) stream.WriteLine($"; Exporting assembly for method {method.Method.MethodDef}");
                    foreach (var line in method.Assembly)
                    {
                        if (!line.EndsWith(":") && !line.StartsWith("[")) stream.Write("    ");
                        stream.WriteLine(line);
                    }
                    stream.WriteLine();
                }

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
                        else if (data.Value is int)
                        {
                            stream.WriteLine($"{data.Key}:");
                            stream.WriteLine($"    dd {(int)data.Value}");
                        }
                        else if (data.Value is uint)
                        {
                            stream.WriteLine($"{data.Key}:");
                            stream.WriteLine($"    dd {(uint)data.Value}");
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
                        else if (data.Value is byte[])
                        {
                            stream.WriteLine($"{data.Key}:");
                            stream.Write($"    db ");
                            var bytes = (byte[])data.Value;
                            for (int i = 0; i < bytes.Length; i++)
                            {
                                stream.Write(bytes[i]);
                                if (i != bytes.Length - 1) stream.Write(", ");
                            }
                            stream.WriteLine();
                        }
                        else
                        {
                            throw new Exception("Unexpected type allocated as part of initial data");
                        }
                    }
                    stream.WriteLine();
                }

                // pad this file to 2kB, since that is what we load in from disk
                stream.WriteLine("times 2048-($-$$) db 0");
            }
        }
    }
}
