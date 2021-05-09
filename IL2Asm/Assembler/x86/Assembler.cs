using PELoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IL2Asm.Assembler.x86
{
    public class Assembler : IAssembler
    {
        private Dictionary<string, AssembledMethod> _staticConstructors = new Dictionary<string, AssembledMethod>();
        private List<AssembledMethod> _methods = new List<AssembledMethod>();
        private Dictionary<string, DataType> _initializedData = new Dictionary<string, DataType>();
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

        private Stack<ElementType> _stack = new Stack<ElementType>();

        private void PopStack(int count)
        {
            for (int i = 0; i < count; i++) _stack.Pop();
        }

        private ElementType eaxType, ebxType;

        public void Assemble(PortableExecutableFile pe, AssembledMethod assembly)//MethodDefLayout methodDef, MethodSpecLayout methodSpec)
        {
            if (!_runtime.Assemblies.Contains(pe)) throw new Exception("The portable executable must be added via AddAssembly prior to called Assemble");
            if (assembly.Method != null && _methods.Where(m => m.Method.MethodDef == assembly.Method.MethodDef && m.MethodSpec == assembly.MethodSpec).Any()) return;

            var method = assembly.Method;//new MethodHeader(pe.Memory, pe.Metadata, assembly.Method.MethodDef);
            //var assembly = new AssembledMethod(pe.Metadata, method, methodSpec);
            var methodDef = assembly.Method.MethodDef;
            _methods.Add(assembly);
            Runtime.GlobalMethodCounter++;

            var code = method.Code;
            int localVarOffset = 0;

            if (_methods.Count > 1)
            {
                string label = assembly.ToAsmString();
                assembly.AddAsm($"{label}:");
                assembly.AddAsm("push ebp");
                assembly.AddAsm("mov ebp, esp");

                if (method.LocalVars != null)
                {
                    int localVarCount = method.LocalVars.LocalVariables.Length;
                    if (localVarCount > 0)
                    {
                        assembly.AddAsm("push ecx; localvar.0");
                        assembly.AddAsm("mov ecx, 0");
                        localVarOffset = BytesPerRegister;
                    }
                    if (localVarCount > 1)
                    {
                        assembly.AddAsm("push edx; localvar.1");
                        assembly.AddAsm("mov edx, 0");
                        localVarOffset = BytesPerRegister * 2;
                    }
                }
            }

            if (method.LocalVars != null)
            {
                int localVarCount = method.LocalVars.LocalVariables.Length;
                for (int i = 2; i < localVarCount; i++)
                {
                    assembly.AddAsm($"push 0; localvar.{i}");
                    _stack.Push(method.LocalVars.LocalVariables[i]);
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
                        if (methodDef.MethodSignature.Flags.HasFlag(SigFlags.HASTHIS)) _uint += 1;
                        assembly.AddAsm($"mov eax, [ebp + {BytesPerRegister * (1 + _uint)}]");
                        assembly.AddAsm("push eax");
                        if (methodDef.MethodSignature.Flags.HasFlag(SigFlags.HASTHIS)) eaxType = new ElementType(ElementType.EType.Object);
                        else eaxType = methodDef.MethodSignature.Params[0];
                        _stack.Push(eaxType);
                        break;
                    // LDARG.1
                    case 0x03:
                        _uint = method.MethodDef.MethodSignature.ParamCount - 1;
                        if (methodDef.MethodSignature.Flags.HasFlag(SigFlags.HASTHIS)) _uint += 1;
                        assembly.AddAsm($"mov eax, [ebp + {BytesPerRegister * (1 + _uint)}]");
                        assembly.AddAsm("push eax");
                        if (methodDef.MethodSignature.Flags.HasFlag(SigFlags.HASTHIS)) eaxType = methodDef.MethodSignature.Params[0];
                        else eaxType = methodDef.MethodSignature.Params[1];
                        _stack.Push(eaxType);
                        break;
                    // LDARG.2
                    case 0x04:
                        _uint = method.MethodDef.MethodSignature.ParamCount - 2;
                        if (methodDef.MethodSignature.Flags.HasFlag(SigFlags.HASTHIS)) _uint += 1;
                        assembly.AddAsm($"mov eax, [ebp + {BytesPerRegister * (1 + _uint)}]");
                        assembly.AddAsm("push eax");
                        if (methodDef.MethodSignature.Flags.HasFlag(SigFlags.HASTHIS)) eaxType = methodDef.MethodSignature.Params[1];
                        else eaxType = methodDef.MethodSignature.Params[2];
                        _stack.Push(eaxType);
                        break;
                    // LDARG.3
                    case 0x05:
                        _uint = method.MethodDef.MethodSignature.ParamCount - 3;
                        if (methodDef.MethodSignature.Flags.HasFlag(SigFlags.HASTHIS)) _uint += 1;
                        assembly.AddAsm($"mov eax, [ebp + {BytesPerRegister * (1 + _uint)}]");
                        assembly.AddAsm("push eax");
                        if (methodDef.MethodSignature.Flags.HasFlag(SigFlags.HASTHIS)) eaxType = methodDef.MethodSignature.Params[2];
                        else eaxType = methodDef.MethodSignature.Params[3];
                        _stack.Push(eaxType);
                        break;

                    // LDLOC.0
                    case 0x06:
                        assembly.AddAsm("push ecx");
                        _stack.Push(method.LocalVars.LocalVariables[0]);
                        break;
                    // LDLOC.1
                    case 0x07:
                        assembly.AddAsm("push edx");
                        _stack.Push(method.LocalVars.LocalVariables[1]);
                        break;
                    // LDLOC.2
                    case 0x08:
                        assembly.AddAsm($"mov eax, [ebp - {localVarOffset + BytesPerRegister}]");
                        assembly.AddAsm("push eax");
                        eaxType = method.LocalVars.LocalVariables[2];
                        _stack.Push(eaxType);
                        break;
                    // LDLOC.3
                    case 0x09:
                        assembly.AddAsm($"mov eax, [ebp - {localVarOffset + 2 * BytesPerRegister}]");
                        assembly.AddAsm("push eax");
                        eaxType = method.LocalVars.LocalVariables[3];
                        _stack.Push(eaxType);
                        break;

                    // STLOC.0
                    case 0x0A:
                        assembly.AddAsm("pop ecx");
                        _stack.Pop();
                        break;
                    // STLOC.1
                    case 0x0B:
                        assembly.AddAsm("pop edx");
                        _stack.Pop();
                        break;
                    // STLOC.2
                    case 0x0C:
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm($"mov [ebp - {localVarOffset + BytesPerRegister}], eax");
                        eaxType = _stack.Pop();
                        break;
                    // STLOC.3
                    case 0x0D:
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm($"mov [ebp - {localVarOffset + 2 * BytesPerRegister}], eax");
                        eaxType = _stack.Pop();
                        break;

                    // LDARG.S
                    case 0x0E:
                        _byte = code[i++];
                        _uint = method.MethodDef.MethodSignature.ParamCount - _byte;
                        if (methodDef.MethodSignature.Flags.HasFlag(SigFlags.HASTHIS)) _uint += 1;
                        assembly.AddAsm($"mov eax, [ebp + {BytesPerRegister * (1 + _uint)}]");
                        assembly.AddAsm("push eax");
                        if (methodDef.MethodSignature.Flags.HasFlag(SigFlags.HASTHIS)) eaxType = methodDef.MethodSignature.Params[_byte - 1];
                        else eaxType = methodDef.MethodSignature.Params[_byte];
                        _stack.Push(eaxType);
                        break;

                    // STARG.S
                    case 0x10:
                        _byte = code[i++];
                        _uint = method.MethodDef.MethodSignature.ParamCount - _byte;
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm($"mov [ebp + {BytesPerRegister * (1 + _uint)}], eax");
                        eaxType = _stack.Pop();
                        break;

                    // LDLOC.S
                    case 0x11:
                        _byte = code[i++];
                        if (_byte == 0) assembly.AddAsm("push ecx");
                        else if (_byte == 1) assembly.AddAsm("push edx");
                        else
                        {
                            assembly.AddAsm($"mov eax, [ebp - {BytesPerRegister * (_byte - 1 + localVarOffset)}]");
                            assembly.AddAsm("push eax");
                            eaxType = method.LocalVars.LocalVariables[_byte];
                        }
                        _stack.Push(method.LocalVars.LocalVariables[_byte]);
                        break;

                    // LDLOCA.S
                    case 0x12:
                        _byte = code[i++];

                        var locType = assembly.Method.LocalVars.LocalVariables[_byte];

                        if (locType.IsPointer())
                        {
                            if (_byte == 0) assembly.AddAsm("push ecx");
                            else if (_byte == 1) assembly.AddAsm("push edx");
                            else
                            {
                                //assembly.AddAsm("mov eax, ebp");
                                //assembly.AddAsm($"sub eax, {BytesPerRegister * (_byte + 1)}");
                                assembly.AddAsm($"mov eax, [ebp - {BytesPerRegister * (_byte - 1 + localVarOffset)}]");
                                assembly.AddAsm("push eax");
                                eaxType = new ElementType(ElementType.EType.ByRef);
                            }
                        }
                        else
                        {
                            throw new Exception("Unsupported");
                        }
                        _stack.Push(new ElementType(ElementType.EType.ByRef));
                        break;

                    // STLOC.S
                    case 0x13:
                        _byte = code[i++];
                        if (_byte == 0) assembly.AddAsm("pop ecx");
                        else if (_byte == 1) assembly.AddAsm("pop edx");
                        else
                        {
                            assembly.AddAsm("pop eax");
                            assembly.AddAsm($"mov [ebp - {BytesPerRegister * (_byte - 1 + localVarOffset)}], eax");
                        }
                        if (_byte > 1) eaxType = _stack.Pop();
                        else _stack.Pop();
                        break;

                    // LDC.I4.0-8
                    case 0x16: assembly.AddAsm("push 0"); _stack.Push(new ElementType(ElementType.EType.I4)); break;
                    case 0x17: assembly.AddAsm("push 1"); _stack.Push(new ElementType(ElementType.EType.I4)); break;
                    case 0x18: assembly.AddAsm("push 2"); _stack.Push(new ElementType(ElementType.EType.I4)); break;
                    case 0x19: assembly.AddAsm("push 3"); _stack.Push(new ElementType(ElementType.EType.I4)); break;
                    case 0x1A: assembly.AddAsm("push 4"); _stack.Push(new ElementType(ElementType.EType.I4)); break;
                    case 0x1B: assembly.AddAsm("push 5"); _stack.Push(new ElementType(ElementType.EType.I4)); break;
                    case 0x1C: assembly.AddAsm("push 6"); _stack.Push(new ElementType(ElementType.EType.I4)); break;
                    case 0x1D: assembly.AddAsm("push 7"); _stack.Push(new ElementType(ElementType.EType.I4)); break;
                    case 0x1E: assembly.AddAsm("push 8"); _stack.Push(new ElementType(ElementType.EType.I4)); break;

                    // LDC.I4.S
                    case 0x1F:
                        assembly.AddAsm($"push {code[i++]}");
                        _stack.Push(new ElementType(ElementType.EType.I4));
                        break;

                    // LDC.I4
                    case 0x20:
                        int value = BitConverter.ToInt32(code, i);
                        i += 4;
                        assembly.AddAsm($"push {value}");
                        _stack.Push(new ElementType(ElementType.EType.I4));
                        break;

                    // LDC.R4
                    case 0x22:
                        float _single = BitConverter.ToSingle(code, i);
                        i += 4;
                        assembly.AddAsm($"push dword __float32__({_single.ToString("F")})");
                        _stack.Push(new ElementType(ElementType.EType.R4));
                        break;

                    // DUP
                    case 0x25:
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("push eax");
                        assembly.AddAsm("push eax");
                        eaxType = _stack.Peek();
                        _stack.Push(eaxType);
                        break;

                    // POP
                    case 0x26:
                        assembly.AddAsm("pop eax");
                        eaxType = _stack.Pop();
                        break;

                    case 0x28: CALL(pe, assembly, code, ref i); break;
                    case 0x6F: CALL(pe, assembly, code, ref i, true); break;

                    // RET
                    case 0x2A:
                        // place the returned value on ax, which should clear our CLI stack
                        if (method.MethodDef.MethodSignature.RetType != null && method.MethodDef.MethodSignature.RetType.Type != ElementType.EType.Void)
                        {
                            assembly.AddAsm("pop eax; return value");
                            eaxType = _stack.Pop();
                        }

                        // pop any local variables we pushed at the start
                        if (method.LocalVars != null)
                        {
                            int localVarCount = method.LocalVars.LocalVariables.Length;
                            for (int p = 2; p < localVarCount; p++)
                            {
                                assembly.AddAsm("pop ebx; localvar that was pushed on stack");
                            }
                            if (localVarCount > 1) assembly.AddAsm("pop edx; localvar.1");
                            if (localVarCount > 0) assembly.AddAsm("pop ecx; localvar.0");
                            if (localVarCount > 2) PopStack(localVarCount - 2);
                        }

                        int bytes = (int)methodDef.MethodSignature.ParamCount * BytesPerRegister;
                        if (methodDef.MethodSignature.Flags.HasFlag(SigFlags.HASTHIS)) bytes += BytesPerRegister;
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
                        eaxType = _stack.Pop();
                        break;

                    // BRTRUE.S
                    case 0x2D:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("cmp eax, 0");
                        assembly.AddAsm($"jne {_jmpLabel}");
                        eaxType = _stack.Pop();
                        break;

                    // BEQ.S
                    case 0x2E:
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop eax");        // value2
                        assembly.AddAsm("pop ebx");        // value1
                        assembly.AddAsm("cmp ebx, eax");    // compare values
                        assembly.AddAsm($"je {_jmpLabel}");
                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
                        break;

                    // BGE.S
                    case 0x2F:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";

                        if (_stack.Peek().Is32BitCapable(pe.Metadata))
                        {
                            assembly.AddAsm("pop eax");        // value2
                            assembly.AddAsm("pop ebx");        // value1
                            assembly.AddAsm("cmp ebx, eax");    // compare values
                            assembly.AddAsm($"jge {_jmpLabel}");
                        }
                        else if (_stack.Peek().Type == ElementType.EType.R4)
                        {
                            assembly.AddAsm("fld dword [esp]");
                            assembly.AddAsm("fld dword [esp + 4]");
                            assembly.AddAsm("fcomip");
                            assembly.AddAsm("pop eax"); // remove one of the R4s from the stack
                            assembly.AddAsm("pop ebx"); // remove one of the R4s from the stack
                            assembly.AddAsm($"jae {_jmpLabel}");
                        }
                        else
                        {
                            throw new Exception("Unsupported type");
                        }

                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
                        break;

                    // BGT.S
                    case 0x30:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";

                        if (_stack.Peek().Is32BitCapable(pe.Metadata))
                        {
                            assembly.AddAsm("pop eax");        // value2
                            assembly.AddAsm("pop ebx");        // value1
                            assembly.AddAsm("cmp ebx, eax");    // compare values
                            assembly.AddAsm($"jg {_jmpLabel}");
                        }
                        else if (_stack.Peek().Type == ElementType.EType.R4)
                        {
                            assembly.AddAsm("fld dword [esp]");
                            assembly.AddAsm("fld dword [esp + 4]");
                            assembly.AddAsm("fcomip");
                            assembly.AddAsm("pop eax"); // remove one of the R4s from the stack
                            assembly.AddAsm("pop ebx"); // remove one of the R4s from the stack
                            assembly.AddAsm($"ja {_jmpLabel}");
                        }
                        else
                        {
                            throw new Exception("Unsupported type");
                        }

                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
                        break;

                    // BLE.S
                    case 0x31:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";

                        if (_stack.Peek().Is32BitCapable(pe.Metadata))
                        {
                            assembly.AddAsm("pop eax");        // value2
                            assembly.AddAsm("pop ebx");        // value1
                            assembly.AddAsm("cmp ebx, eax");    // compare values
                            assembly.AddAsm($"jle {_jmpLabel}");
                        }
                        else if (_stack.Peek().Type == ElementType.EType.R4)
                        {
                            assembly.AddAsm("fld dword [esp]");
                            assembly.AddAsm("fld dword [esp + 4]");
                            assembly.AddAsm("fcomip");
                            assembly.AddAsm("pop eax"); // remove one of the R4s from the stack
                            assembly.AddAsm("pop ebx"); // remove one of the R4s from the stack
                            assembly.AddAsm($"jbe {_jmpLabel}");
                        }
                        else
                        {
                            throw new Exception("Unsupported type");
                        }

                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
                        break;

                    // BLT.S
                    case 0x32:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";

                        if (_stack.Peek().Is32BitCapable(pe.Metadata))
                        {
                            assembly.AddAsm("pop eax");        // value2
                            assembly.AddAsm("pop ebx");        // value1
                            assembly.AddAsm("cmp ebx, eax");    // compare values
                            assembly.AddAsm($"jl {_jmpLabel}");
                        }
                        else if (_stack.Peek().Type == ElementType.EType.R4)
                        {
                            assembly.AddAsm("fld dword [esp]");
                            assembly.AddAsm("fld dword [esp + 4]");
                            assembly.AddAsm("fcomip");
                            assembly.AddAsm("pop eax"); // remove one of the R4s from the stack
                            assembly.AddAsm("pop ebx"); // remove one of the R4s from the stack
                            assembly.AddAsm($"jb {_jmpLabel}");
                        }
                        else
                        {
                            throw new Exception("Unsupported type");
                        }

                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
                        break;

                    // BNE.UN.S
                    case 0x33:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";

                        if (_stack.Peek().Is32BitCapable(pe.Metadata))
                        {
                            assembly.AddAsm("pop eax");        // value2
                            assembly.AddAsm("pop ebx");        // value1
                            assembly.AddAsm("cmp ebx, eax");    // compare values
                            assembly.AddAsm($"jne {_jmpLabel}");
                        }
                        else if (_stack.Peek().Type == ElementType.EType.R4)
                        {
                            assembly.AddAsm("fld dword [esp]");
                            assembly.AddAsm("fld dword [esp + 4]");
                            assembly.AddAsm("fcomip");
                            assembly.AddAsm("pop eax"); // remove one of the R4s from the stack
                            assembly.AddAsm("pop ebx"); // remove one of the R4s from the stack
                            assembly.AddAsm($"jne {_jmpLabel}");
                        }
                        else
                        {
                            throw new Exception("Unsupported type");
                        }

                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
                        break;

                    // BGE.UN.S
                    case 0x34:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";

                        if (_stack.Peek().Is32BitCapable(pe.Metadata))
                        {
                            assembly.AddAsm("pop eax");        // value2
                            assembly.AddAsm("pop ebx");        // value1
                            assembly.AddAsm("cmp ebx, eax");    // compare values
                            assembly.AddAsm($"jae {_jmpLabel}");
                        }
                        else if (_stack.Peek().Type == ElementType.EType.R4)
                        {
                            assembly.AddAsm("fld dword [esp]");
                            assembly.AddAsm("fld dword [esp + 4]");
                            assembly.AddAsm("fcomip");
                            assembly.AddAsm("pop eax"); // remove one of the R4s from the stack
                            assembly.AddAsm("pop ebx"); // remove one of the R4s from the stack
                            assembly.AddAsm($"jae {_jmpLabel}");
                        }
                        else
                        {
                            throw new Exception("Unsupported type");
                        }
                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
                        break;

                    // BLT.UN.S
                    case 0x37:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{Runtime.GlobalMethodCounter}";

                        if (_stack.Peek().Is32BitCapable(pe.Metadata))
                        {
                            assembly.AddAsm("pop eax");        // value2
                            assembly.AddAsm("pop ebx");        // value1
                            assembly.AddAsm("cmp ebx, eax");    // compare values
                            assembly.AddAsm($"jb {_jmpLabel}");
                        }
                        else if (_stack.Peek().Type == ElementType.EType.R4)
                        {
                            assembly.AddAsm("fld dword [esp]");
                            assembly.AddAsm("fld dword [esp + 4]");
                            assembly.AddAsm("fcomip");
                            assembly.AddAsm("pop eax"); // remove one of the R4s from the stack
                            assembly.AddAsm("pop ebx"); // remove one of the R4s from the stack
                            assembly.AddAsm($"jb {_jmpLabel}");
                        }
                        else
                        {
                            throw new Exception("Unsupported type");
                        }
                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
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
                        eaxType = _stack.Pop();
                        break;

                    // BRTRUE
                    case 0x3A:
                        _int = BitConverter.ToInt32(code, i);
                        i += 4;

                        _jmpLabel = $"IL_{(i + _int).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop eax");        // value2
                        assembly.AddAsm("cmp ebx, 0");    // compare values
                        assembly.AddAsm($"jne {_jmpLabel}");
                        eaxType = _stack.Pop();
                        break;

                    // BLT
                    case 0x3F:
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

                        _int = BitConverter.ToInt32(code, i);
                        i += 4;

                        _jmpLabel = $"IL_{(i + _int).ToString("X4")}_{Runtime.GlobalMethodCounter}";
                        assembly.AddAsm("pop eax");        // value2
                        assembly.AddAsm("pop ebx");        // value1
                        assembly.AddAsm("cmp ebx, eax");    // compare values
                        assembly.AddAsm($"jl {_jmpLabel}");
                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
                        break;

                    // BNE.UN
                    case 0x40:
                        _int = BitConverter.ToInt32(code, i);
                        i += 4;

                        _jmpLabel = $"IL_{(i + _int).ToString("X4")}_{Runtime.GlobalMethodCounter}";

                        if (_stack.Peek().Is32BitCapable(pe.Metadata))
                        {
                            assembly.AddAsm("pop eax");        // value2
                            assembly.AddAsm("pop ebx");        // value1
                            assembly.AddAsm("cmp ebx, eax");    // compare values
                            assembly.AddAsm($"jne {_jmpLabel}");
                        }
                        else if (_stack.Peek().Type == ElementType.EType.R4)
                        {
                            assembly.AddAsm("fld dword [esp]");
                            assembly.AddAsm("fld dword [esp + 4]");
                            assembly.AddAsm("fcomip");
                            assembly.AddAsm("pop eax"); // remove one of the R4s from the stack
                            assembly.AddAsm("pop ebx"); // remove one of the R4s from the stack
                            assembly.AddAsm($"jne {_jmpLabel}");
                        }
                        else
                        {
                            throw new Exception("Unsupported type");
                        }

                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
                        break;

                    // SWITCH
                    case 0x45:
                        var defaultLabel = $"IL_{(i - 1).ToString("X4")}_{Runtime.GlobalMethodCounter}_Default";
                        var jmpTableName = $"IL_{(i - 1).ToString("X4")}_{Runtime.GlobalMethodCounter}_JmpTable";

                        _uint = BitConverter.ToUInt32(code, i);
                        i += 4;
                        uint ilPosition = i + 4 * _uint;

                        assembly.AddAsm("pop eax");
                        eaxType = _stack.Pop();

                        // if eax >= N then jump to end
                        assembly.AddAsm($"cmp eax, {_uint}");
                        assembly.AddAsm($"jae {defaultLabel}");

                        // implement a jump table
                        assembly.AddAsm($"jmp [eax*4 + {jmpTableName}]");

                        StringBuilder sb = new StringBuilder();
                        
                        for (uint j = 0; j < _uint; j++)
                        {
                            _int = BitConverter.ToInt32(code, i);
                            i += 4;

                            _jmpLabel = $"IL_{(ilPosition + _int).ToString("X4")}_{Runtime.GlobalMethodCounter}";

                            sb.Append(_jmpLabel);
                            if (j < _uint - 1) sb.Append(", ");
                        }

                        DataType jmpTableData = new DataType(ElementType.EType.JmpTable, sb.ToString());
                        _initializedData.Add(jmpTableName, jmpTableData);

                        assembly.AddAsm($"{defaultLabel}:");

                        break;

                    // ADD
                    case 0x58:
                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
                        _stack.Push(eaxType);

                        if (eaxType.Is32BitCapable(pe.Metadata))
                        {
                            assembly.AddAsm("mov ebx, [esp]");
                            assembly.AddAsm("mov eax, [esp + 4]");
                            assembly.AddAsm("add eax, ebx");
                            assembly.AddAsm("pop ebx");
                            assembly.AddAsm("mov [esp], eax");
                        }
                        else if (eaxType.Type == ElementType.EType.R4)
                        {
                            assembly.AddAsm("fld dword [esp + 4]");
                            assembly.AddAsm("fld dword [esp]");
                            assembly.AddAsm("faddp");
                            assembly.AddAsm("pop eax"); // remove one of the R4s from the stack
                            assembly.AddAsm("fst dword [esp]"); // replace the next R4 on the stack
                        }
                        else
                        {
                            throw new Exception("Unsupported type");
                        }
                        break;

                    // SUB
                    case 0x59:
                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
                        _stack.Push(eaxType);

                        if (eaxType.Is32BitCapable(pe.Metadata) && ebxType.Is32BitCapable(pe.Metadata))
                        {
                            assembly.AddAsm("pop ebx");
                            assembly.AddAsm("pop eax");
                            assembly.AddAsm("sub eax, ebx");
                            assembly.AddAsm("push eax");
                        }
                        else if (eaxType.Type == ElementType.EType.R4 || ebxType.Type == ElementType.EType.R4)
                        {
                            assembly.AddAsm("fld dword [esp + 4]");
                            assembly.AddAsm("fld dword [esp]");
                            assembly.AddAsm("fsubp");
                            assembly.AddAsm("pop eax"); // remove one of the R4s from the stack
                            assembly.AddAsm("fst dword [esp]"); // replace the next R4 on the stack
                        }
                        else
                        {
                            throw new Exception("Unsupported type");
                        }
                        break;

                    // MUL
                    case 0x5A:
                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
                        _stack.Push(eaxType);

                        if (eaxType.Is32BitCapable(pe.Metadata))
                        {
                            assembly.AddAsm("pop ebx");
                            assembly.AddAsm("pop eax");
                            assembly.AddAsm("push edx"); // multiply clobbers edx
                            assembly.AddAsm("mul ebx");
                            assembly.AddAsm("pop edx");  // multiply clobbers edx
                            assembly.AddAsm("push eax");
                        }
                        else if (eaxType.Type == ElementType.EType.R4)
                        {
                            assembly.AddAsm("fld dword [esp + 4]");
                            assembly.AddAsm("fld dword [esp]");
                            assembly.AddAsm("fmulp");
                            assembly.AddAsm("pop eax"); // remove one of the R4s from the stack
                            assembly.AddAsm("fst dword [esp]"); // replace the next R4 on the stack
                        }
                        else
                        {
                            throw new Exception("Unsupported type");
                        }
                        break;

                    // MUL
                    case 0x5B:
                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
                        _stack.Push(eaxType);

                        if (eaxType.Type == ElementType.EType.R4)
                        {
                            assembly.AddAsm("fld dword [esp + 4]");
                            assembly.AddAsm("fld dword [esp]");
                            assembly.AddAsm("fdivp");
                            assembly.AddAsm("pop eax"); // remove one of the R4s from the stack
                            assembly.AddAsm("fst dword [esp]"); // replace the next R4 on the stack
                        }
                        else
                        {
                            throw new Exception("Unsupported type");
                        }
                        break;

                    // AND
                    case 0x5F:
                        assembly.AddAsm("pop ebx");
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("and eax, ebx");
                        assembly.AddAsm("push eax");
                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
                        _stack.Push(eaxType);
                        break;

                    // OR
                    case 0x60:
                        assembly.AddAsm("pop ebx");
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("or eax, ebx");
                        assembly.AddAsm("push eax");
                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
                        _stack.Push(eaxType);
                        break;

                    // SHL
                    case 0x62:
                        assembly.AddAsm("mov ebx, ecx");  // cx is needed to access cl for variable shift, so store is in bx temporarily
                        assembly.AddAsm("pop ecx");      // get amount to shift
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("sal eax, cl");
                        assembly.AddAsm("mov ecx, ebx");  // restore cx
                        assembly.AddAsm("push eax");
                        eaxType = _stack.Pop();
                        _stack.Pop();   // ECX but gets overwritten
                        ebxType = eaxType;
                        _stack.Push(eaxType);
                        break;

                    // SHR
                    case 0x63:
                        assembly.AddAsm("mov ebx, ecx");  // cx is needed to access cl for variable shift, so store is in bx temporarily
                        assembly.AddAsm("pop ecx");      // get amount to shift
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("sar eax, cl");
                        assembly.AddAsm("mov ecx, ebx");  // restore cx
                        assembly.AddAsm("push eax");
                        eaxType = _stack.Pop();
                        _stack.Pop();   // ECX but gets overwritten
                        ebxType = eaxType;
                        _stack.Push(eaxType);
                        break;

                    // SHR.UN
                    case 0x64:
                        assembly.AddAsm("mov ebx, ecx");  // cx is needed to access cl for variable shift, so store is in bx temporarily
                        assembly.AddAsm("pop ecx");      // get amount to shift
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("shr eax, cl");
                        assembly.AddAsm("mov ecx, ebx");  // restore cx
                        assembly.AddAsm("push eax");
                        eaxType = _stack.Pop();
                        _stack.Pop();   // ECX but gets overwritten
                        ebxType = eaxType;
                        _stack.Push(eaxType);
                        break;

                    // CONV.I2
                    case 0x68:
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("and eax, 65535");
                        assembly.AddAsm("push eax");
                        _stack.Pop();
                        eaxType = new ElementType(ElementType.EType.I2);
                        _stack.Push(eaxType);
                        break;

                    // CONV.I4
                    case 0x69:
                        if (_stack.Peek().Is32BitCapable(pe.Metadata))
                        {
                            // no conversion required, we're already 32 bit
                        }
                        else if (_stack.Peek().Type == ElementType.EType.R4)
                        {
                            assembly.AddAsm("fld dword [esp]");
                            assembly.AddAsm("fisttp dword [esp]");
                        }
                        else
                        {
                            throw new Exception("Unsupported type");
                        }
                        _stack.Pop();
                        eaxType = new ElementType(ElementType.EType.I4);
                        _stack.Push(eaxType);
                        break;

                    // CONV.R4
                    case 0x6B:
                        if (_stack.Peek().Is32BitCapable(pe.Metadata))
                        {
                            assembly.AddAsm("fild dword [esp]");
                            assembly.AddAsm("fstp dword [esp]");
                        }
                        else if (_stack.Peek().Type == ElementType.EType.R4)
                        {
                            // no conversion required, we're already a float
                        }
                        else
                        {
                            throw new Exception("Unsupported type");
                        }
                        _stack.Pop();
                        eaxType = new ElementType(ElementType.EType.R4);
                        _stack.Push(eaxType);
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
                        _stack.Pop();
                        eaxType = new ElementType(ElementType.EType.U2);
                        _stack.Push(eaxType);
                        break;

                    // CONV.U1
                    case 0xD2:
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("and eax, 255");
                        assembly.AddAsm("push eax");
                        _stack.Pop();
                        eaxType = new ElementType(ElementType.EType.U1);
                        _stack.Push(eaxType);
                        break;

                    // CEQ
                    case 0xFE01:
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

                        assembly.AddAsm("pop eax");       // value2
                        assembly.AddAsm("pop ebx");       // value1
                        assembly.AddAsm("cmp eax, ebx");  // compare values
                        assembly.AddAsm("lahf");          // load flags into ah
                        assembly.AddAsm("shr eax, 14");   // push the 1 into the LSB
                        assembly.AddAsm("and eax, 1");    // push the 1 into the LSB
                        assembly.AddAsm("push eax");
                        _stack.Pop();
                        ebxType = _stack.Pop();
                        eaxType = new ElementType(ElementType.EType.I4);
                        _stack.Push(eaxType);
                        break;

                    // CGT
                    case 0xFE02:
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

                        assembly.AddAsm("pop eax");       // value2
                        assembly.AddAsm("pop ebx");       // value1
                        assembly.AddAsm("cmp eax, ebx");  // compare values
                        assembly.AddAsm("lahf");          // load flags into ah
                        assembly.AddAsm("shr eax, 15");   // push the 1 into the LSB
                        assembly.AddAsm("and eax, 1");    // push the 1 into the LSB
                        assembly.AddAsm("push eax");
                        _stack.Pop();
                        ebxType = _stack.Pop();
                        eaxType = new ElementType(ElementType.EType.I4);
                        _stack.Push(eaxType);
                        break;

                    // CGT.UN (identical to CGT for now)
                    case 0xFE03:
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

                        assembly.AddAsm("pop eax");       // value2
                        assembly.AddAsm("pop ebx");       // value1
                        assembly.AddAsm("cmp eax, ebx");  // compare values
                        assembly.AddAsm("lahf");          // load flags into ah
                        assembly.AddAsm("shr eax, 15");   // push the 1 into the LSB
                        assembly.AddAsm("and eax, 1");    // push the 1 into the LSB
                        assembly.AddAsm("push eax");
                        _stack.Pop();
                        ebxType = _stack.Pop();
                        eaxType = new ElementType(ElementType.EType.I4);
                        _stack.Push(eaxType);
                        break;

                    // CLT
                    case 0xFE04:
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

                        assembly.AddAsm("pop eax");       // value2
                        assembly.AddAsm("pop ebx");       // value1
                        assembly.AddAsm("cmp ebx, eax");  // compare values
                        assembly.AddAsm("lahf");          // load flags into ah
                        assembly.AddAsm("shr eax, 15");   // push the 1 into the LSB
                        assembly.AddAsm("and eax, 1");    // push the 1 into the LSB
                        assembly.AddAsm("push eax");
                        _stack.Pop();
                        ebxType = _stack.Pop();
                        eaxType = new ElementType(ElementType.EType.I4);
                        _stack.Push(eaxType);
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

                        var methodHeader = new MethodHeader(pe.Memory, pe.Metadata, childMethod);
                        Assemble(pe, new AssembledMethod(pe.Metadata, methodHeader, null));
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
            var type = _runtime.GetFieldType(metadata, fieldToken);

            assembly.AddAsm("pop eax");
            assembly.AddAsm("pop ebx");

            if (type.Type == ElementType.EType.U2 || type.Type == ElementType.EType.I2 || type.Type == ElementType.EType.Char)
            {
                if (offset == 0) assembly.AddAsm("mov word [ebx], ax");
                else assembly.AddAsm($"mov word [ebx + {offset}], ax");
            }
            else if (type.Type == ElementType.EType.U1 || type.Type == ElementType.EType.I1)
            {
                if (offset == 0) assembly.AddAsm("mov byte [ebx], al");
                else assembly.AddAsm($"mov byte [ebx + {offset}], al");
            }
            else if (type.Type == ElementType.EType.U8 || type.Type == ElementType.EType.I8 || type.Type == ElementType.EType.R8)
            {
                throw new Exception("Unsupported type");
            }
            else
            {
                if (offset == 0) assembly.AddAsm("mov [ebx], eax");
                else assembly.AddAsm($"mov [ebx + {offset}], eax");
            }
        }

        private void LDSTR(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint metadataToken = BitConverter.ToUInt32(code, i);
            i += 4;

            string label = $"DB_{metadataToken.ToString("X")}";

            string s = Encoding.Unicode.GetString(metadata.GetMetadata(metadataToken));

            if (!_initializedData.ContainsKey(label)) _initializedData.Add(label, new DataType(ElementType.EType.String, s));
            assembly.AddAsm($"push {label}");

            _stack.Push(new ElementType(ElementType.EType.String));
        }

        private void LDFLD(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint fieldToken = BitConverter.ToUInt32(code, i);
            i += 4;

            int offset = _runtime.GetFieldOffset(metadata, fieldToken);
            var type = _runtime.GetFieldType(metadata, fieldToken);

            assembly.AddAsm("pop ebx");

            if (type.Type == ElementType.EType.U2 || type.Type == ElementType.EType.I2 || type.Type == ElementType.EType.Char)
            {
                assembly.AddAsm("xor eax, eax");
                if (offset == 0) assembly.AddAsm("mov word ax, [ebx]");
                else assembly.AddAsm($"mov word ax, [ebx + {offset}]");
            }
            else if (type.Type == ElementType.EType.U1 || type.Type == ElementType.EType.I1)
            {
                assembly.AddAsm("xor eax, eax");
                if (offset == 0) assembly.AddAsm("mov byte al, [ebx]");
                else assembly.AddAsm($"mov byte al, [ebx + {offset}], ");
            }
            else if (type.Type == ElementType.EType.U8 || type.Type == ElementType.EType.I8 || type.Type == ElementType.EType.R8)
            {
                throw new Exception("Unsupported type");
            }
            else
            {
                if (offset == 0) assembly.AddAsm("mov eax, [ebx]");
                else assembly.AddAsm($"mov eax, [ebx + {offset}]");
            }

            assembly.AddAsm("push eax");

            ebxType = _stack.Pop();
            eaxType = _runtime.GetFieldType(metadata, fieldToken);
            _stack.Push(eaxType);
        }

        private void LDFLDA(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint fieldToken = BitConverter.ToUInt32(code, i);
            i += 4;

            int offset = _runtime.GetFieldOffset(metadata, fieldToken);

            _stack.Pop();
            eaxType = new ElementType(ElementType.EType.ByRef);
            _stack.Push(eaxType);

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

            eaxType = _stack.Pop();

            assembly.AddAsm($"pop eax");
            assembly.AddAsm($"mov [{label}], eax");
        }

        private void LDSFLD(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            int addr = BitConverter.ToInt32(code, i);
            i += 4;

            string label = $"DB_{addr.ToString("X")}";

            if (!_initializedData.ContainsKey(label)) AddStaticField(metadata, label, addr);

            eaxType = _initializedData[label].Type;
            _stack.Push(eaxType);

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

            _stack.Push(new ElementType(ElementType.EType.ByRef));
            assembly.AddAsm($"push {label}");
        }

        private void AddStaticField(CLIMetadata metadata, string label, ElementType type)
        {
            if (_initializedData.ContainsKey(label)) return;

            switch (type.Type)
            {
                case ElementType.EType.U1: _initializedData.Add(label, new DataType(type, (byte)0)); break;
                case ElementType.EType.I1: _initializedData.Add(label, new DataType(type, (sbyte)0)); break;
                case ElementType.EType.U2: _initializedData.Add(label, new DataType(type, (ushort)0)); break;
                case ElementType.EType.I2: _initializedData.Add(label, new DataType(type, (short)0)); break;
                case ElementType.EType.U4: _initializedData.Add(label, new DataType(type, (uint)0)); break;
                case ElementType.EType.I4: _initializedData.Add(label, new DataType(type, (int)0)); break;
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

        private List<AssembledMethod> _methodsToCompile = new List<AssembledMethod>();

        private void CALL(PortableExecutableFile pe, AssembledMethod assembly, byte[] code, ref ushort i, bool callvirt = false)
        {
            var metadata = pe.Metadata;

            uint methodDesc = BitConverter.ToUInt32(code, i);
            i += 4;
            string generic = string.Empty;

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
                generic = methodSpec.MemberSignature.ToAsmString();
            }

            if ((methodDesc & 0xff000000) == 0x0a000000)
            {
                var memberRef = metadata.MemberRefs[(int)(methodDesc & 0x00ffffff) - 1];
                var memberName = memberRef.ToAsmString();
                if (!string.IsNullOrEmpty(generic)) memberName = memberName.Substring(0, memberName.IndexOf("_")) + generic + memberName.Substring(memberName.IndexOf("_"));

                // eax and ebx may have been clobbered
                eaxType = null;
                ebxType = null;

                assembly.AddAsm($"; start {memberName} plug");

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
                else if (memberName == "CPUHelper.CPU.ReadMemShort_U2_U2")
                {
                    assembly.AddAsm("pop ebx");
                    assembly.AddAsm("mov ax, [bx]");
                    assembly.AddAsm("and eax, 65535");
                    assembly.AddAsm("push eax");
                }
                else if (memberName == "CPUHelper.CPU.ReadMemByte_U1_U2")
                {
                    assembly.AddAsm("pop ebx");
                    assembly.AddAsm("mov ax, [bx]");
                    assembly.AddAsm("and eax, 255");
                    assembly.AddAsm("push eax");
                }
                else if (memberName == "CPUHelper.CPU.CopyByte<SMAP_entry>_Void_U4_U4_ByRef")
                {
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
                else if (memberName == "CPUHelper.CPU.WriteMemInt_Void_U4_U4")
                {
                    assembly.AddAsm("pop eax");
                    assembly.AddAsm("pop ebx");
                    assembly.AddAsm("mov [ebx], eax");
                }
                else if (memberName == "CPUHelper.CPU.ReadMemInt_U4_U4")
                {
                    assembly.AddAsm("pop ebx");
                    assembly.AddAsm("mov eax, [ebx]");
                    assembly.AddAsm("push eax");
                }
                else if (memberName == "CPUHelper.CPU.FastA20_Void")
                {
                    // from https://wiki.osdev.org/A20_Line
                    assembly.AddAsm("in al, 0x92");
                    assembly.AddAsm("test al, 2");
                    assembly.AddAsm("jnz fasta20_enabled");
                    assembly.AddAsm("or al, 2");
                    assembly.AddAsm("and al, 0xFE");
                    assembly.AddAsm("out 0x92, al");
                    assembly.AddAsm("fasta20_enabled:");
                }
                else if (memberName == "CPUHelper.CPU.ReadCPUID_Void_ValueType_ByRefValueType")
                {
                    assembly.AddAsm("push ecx");
                    assembly.AddAsm("push edx");
                    assembly.AddAsm("push ebp");
                    assembly.AddAsm("mov eax, [esp+16]");
                    assembly.AddAsm("cpuid");
                    assembly.AddAsm("mov ebp, [esp+12]");
                    assembly.AddAsm("mov [ebp+12], edx");
                    assembly.AddAsm("mov [ebp+8], ecx");
                    assembly.AddAsm("mov [ebp+4], ebx");
                    assembly.AddAsm("mov [ebp], eax");
                    assembly.AddAsm("pop ebp");
                    assembly.AddAsm("pop edx");
                    assembly.AddAsm("pop ecx");
                    assembly.AddAsm("pop eax; pop arg 2");
                    assembly.AddAsm("pop eax; pop arg 1");
                }
                else if (memberName.StartsWith("System.Runtime.InteropServices.Marshal.SizeOf<"))
                {
                    int size = _runtime.GetTypeSize(metadata, methodSpec.MemberSignature.Types[0]);
                    assembly.AddAsm($"mov eax, {size}");
                    assembly.AddAsm("push eax");
                }
                else if (memberName == "System.String.get_Chars_Char_I4")
                {
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
                assembly.AddAsm("; end plug");

                for (int j = 0; j < memberRef.MemberSignature.ParamCount; j++)
                    _stack.Pop();
                if (memberRef.MemberSignature.RetType.Type != ElementType.EType.Void)
                    _stack.Push(memberRef.MemberSignature.RetType);
            }
            else if ((methodDesc & 0xff000000) == 0x06000000)
            {
                var methodDef = metadata.MethodDefs[(int)(methodDesc & 0x00ffffff) - 1];
                var memberName = methodDef.ToAsmString();

                if (!string.IsNullOrEmpty(generic)) memberName = memberName.Substring(0, memberName.IndexOf("_")) + generic + memberName.Substring(memberName.IndexOf("_"));

                if (methodDef.Name.StartsWith("PtrToObject"))
                {
                    assembly.AddAsm("; PtrToObject nop");
                    // this is a nop
                }
                else
                {
                    bool methodAlreadyCompiled = false;
                    foreach (var method in _methods)
                        if (method.Method != null && method.Method.MethodDef.ToAsmString() == methodDef.ToAsmString())
                            methodAlreadyCompiled = true;

                    var methodHeaderToCompile = new MethodHeader(pe.Memory, pe.Metadata, methodDef);
                    var methodToCompile = new AssembledMethod(metadata, methodHeaderToCompile, methodSpec);

                    if (!methodAlreadyCompiled)
                    {
                        bool methodWaitingToCompile = false;
                        foreach (var method in _methodsToCompile)
                            if (method.Method.MethodDef == methodDef && method.MethodSpec == methodSpec)
                                methodWaitingToCompile = true;
                        if (!methodWaitingToCompile)
                            _methodsToCompile.Add(methodToCompile);
                    }

                    string callsite = methodToCompile.ToAsmString().Replace(".", "_");
                    assembly.AddAsm($"call {callsite}");

                    if (methodDef.MethodSignature.RetType != null && methodDef.MethodSignature.RetType.Type != ElementType.EType.Void) assembly.AddAsm("push eax");
                }

                for (int j = 0; j < methodDef.MethodSignature.ParamCount; j++)
                    _stack.Pop();
                if (methodDef.MethodSignature.RetType.Type != ElementType.EType.Void)
                    _stack.Push(methodDef.MethodSignature.RetType);

                // eax and ebx may have been clobbered
                eaxType = null;
                ebxType = null;
            }
            else throw new Exception("Unhandled CALL target");
        }

        public List<string> WriteAssembly(uint offset = 0xA000, uint size = 512)
        {
            List<string> output = new List<string>();

            output.Add("[bits 32]");
            output.Add($"[org 0x{offset.ToString("X")}]");   // for bootsector code only
            output.Add("enter_pm:");
            output.Add("    mov ax, 16");
            output.Add("    mov ds, ax");
            output.Add("    mov ss, ax");
            output.Add("    mov es, ax");
            output.Add("    mov fs, ax");
            output.Add("    mov gs, ax");
            output.Add("    mov ebp, 0x9000 ; reset the stack");
            output.Add("    mov esp, ebp");
            output.Add("    finit");
            output.Add("");

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
                    else if (data.Value.Type.Type == ElementType.EType.I4)
                    {
                        output.Add($"{data.Key}:");
                        output.Add($"    dd {(int)data.Value.Data}");
                    }
                    else if (data.Value.Type.Type == ElementType.EType.U4)
                    {
                        output.Add($"{data.Key}:");
                        output.Add($"    dd {(uint)data.Value.Data}");
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
                    else if (data.Value.Type.Type == ElementType.EType.JmpTable)
                    {
                        output.Add($"{data.Key}:");
                        output.Add($"    dd {data.Value.Data}");
                    }
                    else
                    {
                        throw new Exception("Unexpected type allocated as part of initial data");
                    }
                }
                output.Add("");
            }

            // pad this file to 8kB, since that is what we load in from disk
            //output.Add($"times {size}-($-$$) db 0");

            return output;
        }
    }
}
