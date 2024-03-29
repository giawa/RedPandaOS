﻿using PELoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;

namespace IL2Asm.Assembler.x86.Ver2
{
    public class Assembler : IAssembler
    {
        private Dictionary<string, AssembledMethod> _staticConstructors = new Dictionary<string, AssembledMethod>();
        private List<AssembledMethod> _methods = new List<AssembledMethod>();
        private Dictionary<string, DataType> _initializedData = new Dictionary<string, DataType>();
        private Runtime _runtime = new Runtime(BytesPerRegister);

        public List<AssembledMethod> Methods { get { return _methods; } }

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

        public class StackElementType
        {
            public ElementType Type;
            public int StackLocation;
            public int SizeInBytes;     // minimum size is 4 bytes

            public StackElementType(ElementType type, List<StackElementType> stackTypes, Runtime runtime, CLIMetadata metadata)
            {
                Type = type;

                StackLocation = 0;
                foreach (var t in stackTypes) StackLocation += t.SizeInBytes;

                if (Type.Is32BitCapable(metadata)) SizeInBytes = 4;
                else SizeInBytes = runtime.GetTypeSize(metadata, Type);

                if ((SizeInBytes % 4) != 0) throw new Exception("Invalid type size");
            }
        }

        private Stack<ElementType> DuplicateStack()
        {
            List<ElementType> temp = new List<ElementType>();
            while (_stack.Count > 0) temp.Add(_stack.Pop());
            Stack<ElementType> clone = new Stack<ElementType>();

            for (int i = temp.Count - 1; i >= 0; i--)
            {
                _stack.Push(temp[i]);
                clone.Push(temp[i]);
            }

            return clone;
        }

        public void Assemble(PortableExecutableFile pe, AssembledMethod assembly)//MethodDefLayout methodDef, MethodSpecLayout methodSpec)
        {
            if (assembly.Method.MethodDef.Attributes.Where(a => a.Name.Contains("IL2Asm.BaseTypes.AsmMethodAttribute")).Count() > 0)
            {
                var name = assembly.Method.MethodDef.ToAsmString();
                throw new Exception("Tried to compile a method flagged with the AsmMethod attribute.");
            }
            if (assembly.Method.MethodDef.Attributes.Where(a => a.Name.Contains("Il2Asm.BaseTypes.RequireStackFrameAttribute")).Count() > 0)
            {
                AddStackFrame(assembly);
            }
            if (!_runtime.Assemblies.Contains(pe)) throw new Exception("The portable executable must be added via AddAssembly prior to called Assemble");
            if (assembly.Method != null && _methods.Where(m => m.Method?.MethodDef == assembly.Method.MethodDef && m.MethodSpec == assembly.MethodSpec).Any()) return;

            var method = assembly.Method;//new MethodHeader(pe.Memory, pe.Metadata, assembly.Method.MethodDef);
            //var assembly = new AssembledMethod(pe.Metadata, method, methodSpec);
            var methodDef = assembly.Method.MethodDef;
            _methods.Add(assembly);
            assembly.MethodCounter = Runtime.GlobalMethodCounter++;

            Dictionary<int, Stack<ElementType>> branchStacks = new Dictionary<int, Stack<ElementType>>();

            var code = method.Code;

            if (_methods.Count > 1)
            {
                string label = assembly.ToAsmString(assembly.GenericInstSig);

                assembly.AddAsm($"{label}:");

                if (assembly.HasStackFrame)
                {
                    assembly.AddAsm("push ebp");
                    assembly.AddAsm("mov ebp, esp");
                }

                /*if (method.LocalVars != null)
                {
                    int localVarCount = method.LocalVars.LocalVariables.Length;
                    if (localVarCount > 0)
                    {
                        assembly.AddAsm("push ecx; localvar.0");
                        assembly.AddAsm("mov ecx, 0");
                    }
                    if (localVarCount > 1)
                    {
                        assembly.AddAsm("push edx; localvar.1");
                        assembly.AddAsm("mov edx, 0");
                    }
                }*/
            }

            if (method.LocalVars != null)
            {
                for (int v = 0; v < method.LocalVars.LocalVariables.Length; v++)
                {
                    var lvar = method.LocalVars.LocalVariables[v];
                    if (lvar.Type == ElementType.EType.Var || lvar.Type == ElementType.EType.MVar)
                        method.LocalVars.LocalVariables[v] = assembly.GenericInstSig.Params[0];
                }

                foreach (var v in method.LocalVars.LocalVariables)
                    if (!v.Is32BitCapable(pe.Metadata) && v.Type != ElementType.EType.R4) throw new Exception("Not supported yet");

                int localVarCount = method.LocalVars.LocalVariables.Length;
                for (int i = 0; i < localVarCount; i++)
                {
                    //assembly.AddAsm($"push 0; localvar.{i}");
                    _stack.Push(method.LocalVars.LocalVariables[i]);
                }
                if (localVarCount > 0) assembly.AddAsm($"sub esp, {BytesPerRegister * localVarCount} ; {localVarCount} localvars");
            }

            List<StackElementType> callingStackTypes = new List<StackElementType>();
            if (methodDef.Signature.Flags.HasFlag(SigFlags.HASTHIS))
            {
                callingStackTypes.Add(new StackElementType(new ElementType(ElementType.EType.Object), callingStackTypes, _runtime, pe.Metadata));
                //throw new Exception("Verify this is correct.  May need to adjust LDARG and others as well.");
            }
            if (methodDef.Signature.ParamCount > 0)
            {
                foreach (var param in methodDef.Signature.Params) callingStackTypes.Add(new StackElementType(param, callingStackTypes, _runtime, pe.Metadata));
            }

            int callingStackSize = 0;
            foreach (var a in callingStackTypes) callingStackSize += a.SizeInBytes;
            StackElementType arg = null;

            for (ushort i = 0; i < code.Length;)
            {
                int opcode = code[i++];

                if (opcode == 0xfe) opcode = (opcode << 8) | code[i++];

                if (branchStacks.ContainsKey(i - 1)) _stack = branchStacks[i - 1];

                // add label for this opcode
                string label = $"IL_{(i - 1).ToString("X4")}_{assembly.MethodCounter}";
                assembly.AddAsm($"{label}:");
                int asmCount = assembly.Assembly.Count;

                switch (opcode)
                {
                    case 0x00: /*assembly.AddAsm("nop");*/ break;   // NOP

                    // LDARG.0
                    case 0x02:
                        LDARG(0, assembly, callingStackTypes, callingStackSize, methodDef);
                        break;
                    // LDARG.1
                    case 0x03:
                        LDARG(1, assembly, callingStackTypes, callingStackSize, methodDef);
                        break;
                    // LDARG.2
                    case 0x04:
                        LDARG(2, assembly, callingStackTypes, callingStackSize, methodDef);
                        break;
                    // LDARG.3
                    case 0x05:
                        LDARG(3, assembly, callingStackTypes, callingStackSize, methodDef);
                        break;

                    // LDLOC.0
                    case 0x06: LDLOC(0, assembly); break;
                    // LDLOC.1
                    case 0x07: LDLOC(1, assembly); break;
                    // LDLOC.2
                    case 0x08: LDLOC(2, assembly); break;
                    // LDLOC.3
                    case 0x09: LDLOC(3, assembly); break;

                    // STLOC.0
                    case 0x0A: STLOC(0, assembly); break;
                    // STLOC.1
                    case 0x0B: STLOC(1, assembly); break;
                    // STLOC.2
                    case 0x0C: STLOC(2, assembly); break;
                    // STLOC.3
                    case 0x0D: STLOC(3, assembly); break;

                    // LDARG.S
                    case 0x0E:
                        _byte = code[i++];
                        LDARG(_byte, assembly, callingStackTypes, callingStackSize, methodDef);
                        break;

                    // LDARGA.S
                    case 0x0F:
                        _byte = code[i++];
                        if (methodDef.Signature.Flags.HasFlag(SigFlags.HASTHIS)) _byte++; // skip THIS
                        arg = callingStackTypes[_byte];
                        /*assembly.AddAsm("mov eax, ebp");// + {arg.StackLocation + 4}");
                        assembly.AddAsm($"add eax, {arg.StackLocation + 4}");
                        assembly.AddAsm("push eax");*/

                        _int = 1 + callingStackSize / 4 - arg.StackLocation / 4 - (arg.SizeInBytes / 4 - 1);
                        assembly.AddAsm("mov eax, ebp");
                        assembly.AddAsm($"add eax, {4 * _int}");
                        //assembly.AddAsm($"lea eax, [ebp + 4 * {_int}]");
                        assembly.AddAsm("push eax");

                        if (arg.Type.Type == ElementType.EType.ValueType) eaxType = new ElementType(ElementType.EType.ByRefValueType);
                        //else if (arg.Type.Type == ElementType.EType.Object) eaxType = new ElementType(ElementType.EType.ByRef);
                        else throw new Exception("Unsupported type");
                        /*_uint = method.MethodDef.MethodSignature.ParamCount - _byte;
                        if (methodDef.MethodSignature.Flags.HasFlag(SigFlags.HASTHIS)) _uint += 1;
                        assembly.AddAsm($"mov eax, [ebp + {BytesPerRegister * (1 + _uint)}]");
                        assembly.AddAsm("push eax");
                        if (methodDef.MethodSignature.Flags.HasFlag(SigFlags.HASTHIS)) eaxType = methodDef.MethodSignature.Params[_byte - 1];
                        else eaxType = methodDef.MethodSignature.Params[_byte];*/
                        _stack.Push(eaxType);
                        break;

                    // STARG.S
                    case 0x10:
                        STARG(code[i++], assembly, pe.Metadata, callingStackTypes, callingStackSize, methodDef);
                        break;

                    // LDLOC.S
                    case 0x11: LDLOC(code[i++], assembly); break;

                    // LDLOCA.S
                    case 0x12:
                        _byte = code[i++];
                        LDLOCA(_byte, assembly);
                        break;

                    // STLOC.S
                    case 0x13: STLOC(code[i++], assembly); break;

                    // LDNULL
                    case 0x14: assembly.AddAsm("push 0"); _stack.Push(new ElementType(ElementType.EType.Object)); break;

                    // LDC.I4.-1-8
                    case 0x15: assembly.AddAsm("push -1"); _stack.Push(new ElementType(ElementType.EType.I4)); break;
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
                        assembly.AddAsm($"push {(sbyte)code[i++]}");
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
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("push eax");
                        assembly.AddAsm("push eax");
                        eaxType = _stack.Peek();
                        _stack.Push(eaxType);
                        break;

                    // POP
                    case 0x26:
                        if (_stack.Peek().Is32BitCapable(pe.Metadata))
                        {
                            assembly.AddAsm("pop eax");
                        }
                        else
                        {
                            var sizeOf = _runtime.GetTypeSize(pe.Metadata, _stack.Peek());
                            if ((sizeOf % 4) != 0) throw new Exception("Unsupported type");

                            if (sizeOf <= 8)
                            {
                                for (int b = 0; b < Math.Ceiling(sizeOf / 4f); b++) assembly.AddAsm("pop eax");
                            }
                            else
                            {
                                assembly.AddAsm($"add esp, {sizeOf}");
                            }
                        }
                        eaxType = _stack.Pop();
                        break;

                    // CALL
                    case 0x28: CALL(pe, assembly, code, ref i); break;

                    // CALVIRT
                    case 0x6F: CALL(pe, assembly, code, ref i, true); break;

                    // RET
                    case 0x2A:
                        // place the returned value on ax, which should clear our CLI stack
                        if (method.MethodDef.Signature.RetType != null && method.MethodDef.Signature.RetType.Type != ElementType.EType.Void)
                        {
                            int retSize = _runtime.GetTypeSize(pe.Metadata, _stack.Peek());
                            if (retSize == 4) assembly.AddAsm("pop eax; return value");
                            else
                            {
                                for (int b = 0; b < Math.Ceiling(retSize / 4f); b++)
                                    assembly.AddAsm($"pop eax; return value {b + 1}/{Math.Ceiling(retSize / 4f)}");
                            }
                            eaxType = _stack.Pop();
                        }

                        // pop any local variables we pushed at the start
                        if (method.LocalVars != null)
                        {
                            int localVarCount = method.LocalVars.LocalVariables.Length;

                            if (localVarCount > 0)
                            {
                                if (localVarCount <= 2)
                                {
                                    for (int p = 0; p < localVarCount; p++)
                                    {
                                        assembly.AddAsm("pop ebx; localvar that was pushed on stack");
                                    }
                                }
                                else assembly.AddAsm($"add esp, {BytesPerRegister * localVarCount} ; pop {localVarCount} localvars");
                            }
                            //if (localVarCount > 1) assembly.AddAsm("pop edx; localvar.1");
                            //if (localVarCount > 0) assembly.AddAsm("pop ecx; localvar.0");
                            //if (localVarCount > 2) PopStack(localVarCount - 2);   // don't pop the stack because there can be multiple RET per method
                        }

                        int bytes = 0;
                        if (methodDef.Signature.ParamCount > 0)
                        {
                            foreach (var param in methodDef.Signature.Params)
                            {
                                int paramSize = _runtime.GetTypeSize(pe.Metadata, param);
                                bytes += 4 * (int)Math.Ceiling(paramSize / 4f);
                            }
                        }
                        if (methodDef.Signature.Flags.HasFlag(SigFlags.HASTHIS)) bytes += BytesPerRegister;
                        if (assembly.HasStackFrame) assembly.AddAsm("pop ebp");
                        if (method.MethodDef.Name.StartsWith("IsrHandler") || method.MethodDef.Name.StartsWith("IrqHandler")) assembly.AddAsm("ret");
                        else assembly.AddAsm($"ret {bytes}");
                        break;

                    // BR.S
                    case 0x2B:
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{assembly.MethodCounter}";
                        assembly.AddAsm($"jmp {_jmpLabel}");

                        // unconditional branches can unbalance the stack
                        // so we store the current stack state which will be recovered at the branch il code
                        branchStacks[i + _sbyte] = DuplicateStack();

                        break;

                    // BRFALSE.S
                    case 0x2C:
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{assembly.MethodCounter}";
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("cmp eax, 0");
                        assembly.AddAsm($"je {_jmpLabel}");
                        eaxType = _stack.Pop();
                        break;

                    // BRTRUE.S
                    case 0x2D:
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{assembly.MethodCounter}";
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("cmp eax, 0");
                        assembly.AddAsm($"jne {_jmpLabel}");
                        eaxType = _stack.Pop();
                        break;

                    // BEQ.S
                    case 0x2E:
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata) && _stack.Peek().Type != ElementType.EType.R4) throw new Exception("Unsupported type");

                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{assembly.MethodCounter}";
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
                        Branch(i + _sbyte, assembly, pe.Metadata, "jge", "jae");
                        break;

                    // BGT.S
                    case 0x30:
                        _sbyte = (sbyte)code[i++];
                        Branch(i + _sbyte, assembly, pe.Metadata, "jg", "ja");
                        break;

                    // BLE.S
                    case 0x31:
                        _sbyte = (sbyte)code[i++];
                        Branch(i + _sbyte, assembly, pe.Metadata, "jle", "jbe");
                        break;

                    // BLT.S
                    case 0x32:
                        _sbyte = (sbyte)code[i++];
                        Branch(i + _sbyte, assembly, pe.Metadata, "jl", "jb");
                        break;

                    // BNE.UN.S
                    case 0x33:
                        _sbyte = (sbyte)code[i++];
                        Branch(i + _sbyte, assembly, pe.Metadata, "jne", "jne");
                        break;

                    // BGE.UN.S
                    case 0x34:
                        _sbyte = (sbyte)code[i++];
                        Branch(i + _sbyte, assembly, pe.Metadata, "jae", "jae");
                        break;

                    // BGT.UN.S
                    case 0x35:
                        _sbyte = (sbyte)code[i++];
                        Branch(i + _sbyte, assembly, pe.Metadata, "jg", "ja");
                        break;

                    // BLE.UN.S
                    case 0x36:
                        _sbyte = (sbyte)code[i++];
                        Branch(i + _sbyte, assembly, pe.Metadata, "jbe", "jbe");
                        break;

                    // BLT.UN.S
                    case 0x37:
                        _sbyte = (sbyte)code[i++];
                        Branch(i + _sbyte, assembly, pe.Metadata, "jb", "jb");
                        break;

                    // BR
                    case 0x38:
                        _int = BitConverter.ToInt32(code, i);
                        i += 4;

                        _jmpLabel = $"IL_{(i + _int).ToString("X4")}_{assembly.MethodCounter}";
                        assembly.AddAsm($"jmp {_jmpLabel}");
                        break;

                    // BRFALSE
                    case 0x39:
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

                        _int = BitConverter.ToInt32(code, i);
                        i += 4;

                        _jmpLabel = $"IL_{(i + _int).ToString("X4")}_{assembly.MethodCounter}";
                        assembly.AddAsm("pop eax");        // value2
                        assembly.AddAsm("cmp eax, 0");    // compare values
                        assembly.AddAsm($"je {_jmpLabel}");
                        eaxType = _stack.Pop();
                        break;

                    // BRTRUE
                    case 0x3A:
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

                        _int = BitConverter.ToInt32(code, i);
                        i += 4;

                        _jmpLabel = $"IL_{(i + _int).ToString("X4")}_{assembly.MethodCounter}";
                        assembly.AddAsm("pop eax");        // value2
                        assembly.AddAsm("cmp eax, 0");    // compare values
                        assembly.AddAsm($"jne {_jmpLabel}");
                        eaxType = _stack.Pop();
                        break;

                    // BEQ
                    case 0x3B:
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

                        _int = BitConverter.ToInt32(code, i);
                        i += 4;

                        _jmpLabel = $"IL_{(i + _int).ToString("X4")}_{assembly.MethodCounter}";
                        assembly.AddAsm("pop eax");        // value2
                        assembly.AddAsm("pop ebx");        // value1
                        assembly.AddAsm("cmp ebx, eax");    // compare values
                        assembly.AddAsm($"je {_jmpLabel}");
                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
                        break;

                    // BGT
                    case 0x3D:
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

                        _int = BitConverter.ToInt32(code, i);
                        i += 4;

                        _jmpLabel = $"IL_{(i + _int).ToString("X4")}_{assembly.MethodCounter}";
                        assembly.AddAsm("pop eax");        // value2
                        assembly.AddAsm("pop ebx");        // value1
                        assembly.AddAsm("cmp ebx, eax");    // compare values
                        assembly.AddAsm($"jnbe {_jmpLabel}");
                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
                        break;

                    // BLE
                    case 0x3E:
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

                        _int = BitConverter.ToInt32(code, i);
                        i += 4;

                        _jmpLabel = $"IL_{(i + _int).ToString("X4")}_{assembly.MethodCounter}";
                        assembly.AddAsm("pop eax");        // value2
                        assembly.AddAsm("pop ebx");        // value1
                        assembly.AddAsm("cmp ebx, eax");    // compare values
                        assembly.AddAsm($"jle {_jmpLabel}");
                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
                        break;

                    // BLT
                    case 0x3F:
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

                        _int = BitConverter.ToInt32(code, i);
                        i += 4;

                        _jmpLabel = $"IL_{(i + _int).ToString("X4")}_{assembly.MethodCounter}";
                        assembly.AddAsm("pop eax");        // value2
                        assembly.AddAsm("pop ebx");        // value1
                        assembly.AddAsm("cmp ebx, eax");    // compare values
                        assembly.AddAsm($"jnge {_jmpLabel}");
                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
                        break;

                    // BNE.UN
                    case 0x40:
                        _int = BitConverter.ToInt32(code, i);
                        i += 4;
                        Branch(i + _int, assembly, pe.Metadata, "jne", "jne");
                        break;

                    // BLT.UN
                    case 0x44:
                        _int = BitConverter.ToInt32(code, i);
                        i += 4;
                        Branch(i + _int, assembly, pe.Metadata, "jnge", "jnge");
                        break;

                    // SWITCH
                    case 0x45:
                        var defaultLabel = $"IL_{(i - 1).ToString("X4")}_{assembly.MethodCounter}_Default";
                        var jmpTableName = $"DD_{(i - 1).ToString("X4")}_{assembly.MethodCounter}_JmpTable";

                        _uint = BitConverter.ToUInt32(code, i);
                        i += 4;
                        uint ilPosition = i + 4 * _uint;

                        assembly.AddAsm("pop eax");
                        eaxType = _stack.Pop();

                        // if eax >= N then jump to end
                        assembly.AddAsm($"cmp eax, {_uint}");
                        assembly.AddAsm($"jae {defaultLabel}");

                        // implement a jump table
                        assembly.AddAsm($"jmp [eax * 4 + {jmpTableName}]");

                        StringBuilder sb = new StringBuilder();

                        for (uint j = 0; j < _uint; j++)
                        {
                            _int = BitConverter.ToInt32(code, i);
                            i += 4;

                            _jmpLabel = $"IL_{(ilPosition + _int).ToString("X4")}_{assembly.MethodCounter}";

                            sb.Append(_jmpLabel);
                            if (j < _uint - 1) sb.Append(", ");
                        }

                        DataType jmpTableData = new DataType(ElementType.EType.JmpTable, sb.ToString());
                        _initializedData.Add(jmpTableName, jmpTableData);

                        assembly.AddAsm($"{defaultLabel}:");

                        break;

                    // LDIND.I4
                    case 0x4A:
                        if (_stack.Peek().Type != ElementType.EType.ByRef && _stack.Peek().Type != ElementType.EType.ByRefValueType)
                            throw new Exception("Unsupported type");

                        eaxType = _stack.Pop();
                        ebxType = new ElementType(ElementType.EType.I4);
                        _stack.Push(ebxType);

                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("mov ebx, [eax]");
                        assembly.AddAsm("push ebx");
                        break;

                    // LDIND.U4
                    case 0x4B:
                        if (_stack.Peek().Type != ElementType.EType.ByRef && _stack.Peek().Type != ElementType.EType.ByRefValueType)
                            throw new Exception("Unsupported type");

                        eaxType = _stack.Pop();
                        ebxType = new ElementType(ElementType.EType.U4);
                        _stack.Push(ebxType);

                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("mov ebx, [eax]");
                        assembly.AddAsm("push ebx");
                        break;

                    // STIND.I4
                    case 0x54:
                        ebxType = _stack.Pop();
                        eaxType = _stack.Pop();
                        if (ebxType.Type != ElementType.EType.I4) throw new Exception("Unexpected type");
                        if (eaxType.Type != ElementType.EType.ByRef && eaxType.Type != ElementType.EType.ByRefValueType)
                            throw new Exception("Unsupported type");

                        assembly.AddAsm("pop ebx"); // value
                        assembly.AddAsm("pop eax"); // address
                        assembly.AddAsm("mov [eax], ebx");
                        break;

                    // ADD
                    case 0x58:
                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
                        _stack.Push(eaxType);

                        if (eaxType.Is32BitCapable(pe.Metadata))
                        {
                            //assembly.AddAsm("mov ebx, [esp]");
                            //assembly.AddAsm("mov eax, [esp + 4]");
                            assembly.AddAsm("pop ebx");
                            assembly.AddAsm("pop eax");
                            assembly.AddAsm("add eax, ebx");
                            assembly.AddAsm("push eax");
                            //assembly.AddAsm("pop ebx");
                            //assembly.AddAsm("mov [esp], eax");
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
                            //assembly.AddAsm("mov ebx, [esp]");
                            //assembly.AddAsm("mov eax, [esp + 4]");
                            assembly.AddAsm("pop ebx");
                            assembly.AddAsm("pop eax");
                            assembly.AddAsm("sub eax, ebx");
                            assembly.AddAsm("push eax");
                            //assembly.AddAsm("pop ebx");
                            //assembly.AddAsm("mov [esp], eax");
                        }
                        else if (eaxType.Type == ElementType.EType.R4)
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

                    // DIV and DIV.UN
                    case 0x5B:
                    case 0x5C:
                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
                        _stack.Push(eaxType);

                        if (eaxType.Is32BitCapable(pe.Metadata))
                        {
                            assembly.AddAsm("pop ebx");
                            assembly.AddAsm("pop eax");
                            assembly.AddAsm("push edx"); // divide clobbers edx
                            assembly.AddAsm("mov edx, 0");
                            assembly.AddAsm("div ebx");
                            assembly.AddAsm("pop edx");  // divide clobbers edx
                            assembly.AddAsm("push eax");
                        }
                        else if (eaxType.Type == ElementType.EType.R4)
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

                    // MOD.UN and MOD (REM.UN and REM)
                    case 0x5D:
                    case 0x5E:
                        eaxType = _stack.Pop();
                        ebxType = _stack.Pop();
                        _stack.Push(eaxType);

                        if (eaxType.Is32BitCapable(pe.Metadata))
                        {
                            assembly.AddAsm("pop ebx");
                            assembly.AddAsm("pop eax");
                            assembly.AddAsm("push edx"); // divide clobbers edx
                            assembly.AddAsm("mov edx, 0");
                            assembly.AddAsm("div ebx");
                            assembly.AddAsm("mov eax, edx");    // we want the remainder
                            assembly.AddAsm("pop edx");  // divide clobbers edx
                            assembly.AddAsm("push eax");
                        }
                        else
                        {
                            throw new Exception("Unsupported type");
                        }
                        break;

                    // AND
                    case 0x5F:
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

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
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

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
                        ebxType = _stack.Pop();
                        eaxType = _stack.Peek();

                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

                        assembly.AddAsm("mov ebx, ecx");  // cx is needed to access cl for variable shift, so store is in bx temporarily
                        assembly.AddAsm("pop ecx");      // get amount to shift
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("sal eax, cl");
                        assembly.AddAsm("mov ecx, ebx");  // restore cx
                        assembly.AddAsm("push eax");
                        //eaxType = _stack.Pop();
                        //_stack.Pop();   // ECX but gets overwritten
                        //ebxType = eaxType;
                        //_stack.Push(eaxType); // TODO:  Check this
                        break;

                    // SHR
                    case 0x63:
                        ebxType = _stack.Pop();
                        eaxType = _stack.Peek();

                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

                        assembly.AddAsm("mov ebx, ecx");  // cx is needed to access cl for variable shift, so store is in bx temporarily
                        assembly.AddAsm("pop ecx");      // get amount to shift
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("sar eax, cl");
                        assembly.AddAsm("mov ecx, ebx");  // restore cx
                        assembly.AddAsm("push eax");
                        /*eaxType = _stack.Pop();
                        _stack.Pop();   // ECX but gets overwritten
                        ebxType = eaxType;
                        _stack.Push(eaxType);*/
                        break;

                    // SHR.UN
                    case 0x64:
                        ebxType = _stack.Pop();
                        eaxType = _stack.Peek();

                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

                        assembly.AddAsm("mov ebx, ecx");  // cx is needed to access cl for variable shift, so store is in bx temporarily
                        assembly.AddAsm("pop ecx");      // get amount to shift
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("shr eax, cl");
                        assembly.AddAsm("mov ecx, ebx");  // restore cx
                        assembly.AddAsm("push eax");
                        /*eaxType = _stack.Pop();
                        _stack.Pop();   // ECX but gets overwritten
                        ebxType = eaxType;
                        _stack.Push(eaxType);*/
                        break;

                    // NEG
                    case 0x65:
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

                        // we push same type back on to stack, so no modification to _stack is required
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("neg eax");
                        assembly.AddAsm("push eax");

                        break;

                    // NOT
                    case 0x66:
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

                        // we push same type back on to stack, so no modification to _stack is required
                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("not eax");
                        assembly.AddAsm("push eax");

                        break;

                    // CONV.I2
                    case 0x68:
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

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

                    // CONV.U4
                    case 0x6D:
                        if (_stack.Peek().Is32BitCapable(pe.Metadata))
                        {
                            // no conversion required, we're already 32 bit
                        }
                        else if (_stack.Peek().Type == ElementType.EType.R4)
                        {
                            // TODO: This doesn't work if the floating point number is negative
                            assembly.AddAsm("fld dword [esp]");
                            assembly.AddAsm("fisttp dword [esp]");
                        }
                        _stack.Pop();
                        eaxType = new ElementType(ElementType.EType.U4);
                        _stack.Push(eaxType);
                        break;

                    // LDOBJ
                    //case 0x71: LDOBJ(assembly, pe.Metadata, code, ref i); break;

                    // LDSTR
                    case 0x72: LDSTR(assembly, pe.Metadata, code, ref i); break;

                    // NEWOBJ
                    case 0x73: NEWOBJ(pe, assembly, code, ref i); break;

                    // THROW
                    case 0x7A: assembly.AddAsm($"call {ThrowExceptionMethod}"); _stack.Pop(); break;

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

                    // STOBJ
                    case 0x81: STOBJ(assembly, pe.Metadata, code, ref i); break;

                    // BOX
                    case 0x8C: BOX(assembly, pe.Metadata, code, ref i); break;

                    // NEWARR
                    case 0x8D: NEWARR(pe, assembly, code, ref i); break;

                    // LDLEN
                    case 0x8E:
                        // the length is just the first element of the array
                        assembly.AddAsm("pop eax");
                        eaxType = _stack.Pop();
                        assembly.AddAsm("mov ebx, [eax]");
                        ebxType = new ElementType(ElementType.EType.U4);
                        assembly.AddAsm("push ebx");
                        _stack.Push(ebxType);
                        break;

                    // LDELEMA
                    case 0x8F: LDELEMA(assembly, pe.Metadata, code, ref i); break;

                    // LDELEM_I1
                    case 0x90: LDELEM(1, assembly, pe.Metadata, code, ref i); break;

                    // LDELEM_U1
                    case 0x91: LDELEM(1, assembly, pe.Metadata, code, ref i); break;

                    // LDELEM_I2
                    case 0x92: LDELEM(2, assembly, pe.Metadata, code, ref i); break;

                    // LDELEM_U2
                    case 0x93: LDELEM(2, assembly, pe.Metadata, code, ref i); break;

                    // LDELEM_I4
                    case 0x94: LDELEM(4, assembly, pe.Metadata, code, ref i); break;

                    // LDELEM_U4
                    case 0x95: LDELEM(4, assembly, pe.Metadata, code, ref i); break;

                    // LDELEM_I
                    case 0x97: LDELEM(4, assembly, pe.Metadata, code, ref i); break;

                    // LDELEM_R4
                    case 0x98: LDELEM(4, assembly, pe.Metadata, code, ref i); break;

                    // LDELEM_REF
                    case 0x9A: LDELEM(4, assembly, pe.Metadata, code, ref i); break;

                    // STELEM_I (native int)
                    case 0x9B: STELEM(4, assembly, pe.Metadata, code, ref i); break;

                    // STELEM_I1
                    case 0x9C: STELEM(1, assembly, pe.Metadata, code, ref i); break;

                    // STELEM_I2
                    case 0x9D: STELEM(2, assembly, pe.Metadata, code, ref i); break;

                    // STELEM_I4
                    case 0x9E: STELEM(4, assembly, pe.Metadata, code, ref i); break;

                    // STELEM_R4
                    case 0xA0: STELEM(4, assembly, pe.Metadata, code, ref i); break;

                    // STELEM_REF
                    case 0xA2: STELEM(4, assembly, pe.Metadata, code, ref i); break;

                    // LDELEM
                    case 0xA3: LDELEM(0, assembly, pe.Metadata, code, ref i); break;

                    // STELEM
                    case 0xA4: STELEM(0, assembly, pe.Metadata, code, ref i); break;

                    // CONV.U2
                    case 0xD1:
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("and eax, 65535");
                        assembly.AddAsm("push eax");
                        _stack.Pop();
                        eaxType = new ElementType(ElementType.EType.U2);
                        _stack.Push(eaxType);
                        break;

                    // CONV.U1
                    case 0xD2:
                        if (!_stack.Peek().Is32BitCapable(pe.Metadata)) throw new Exception("Unsupported type");

                        assembly.AddAsm("pop eax");
                        assembly.AddAsm("and eax, 255");
                        assembly.AddAsm("push eax");
                        _stack.Pop();
                        eaxType = new ElementType(ElementType.EType.U1);
                        _stack.Push(eaxType);
                        break;

                    // ENDFINALLY
                    case 0xDC:
                        // TODO: We should use the value stored in LEAVE.S _jmpLabel to know where to actually jump to
                        assembly.AddAsm("nop");

                        break;

                    // LEAVE.S
                    case 0xDE:
                        // TODO:  We should store this somewhere so that the finally block knows where to actually jump
                        _sbyte = (sbyte)code[i++];
                        _jmpLabel = $"IL_{(i + _sbyte).ToString("X4")}_{assembly.MethodCounter}";   // technically we should jump here after executing any finally/etc

                        // find the try we are in
                        var exceptionHeader = method.ExceptionHeaders.Where(h => h.TryOffset < i && i <= h.TryOffset + h.TryLength).SingleOrDefault();
                        if (exceptionHeader.Flags.HasFlag(ExceptionHeader.ExceptionHeaderFlags.Finally))
                        {
                            string finallyJmp = $"IL_{(exceptionHeader.HandlerOffset).ToString("X4")}_{assembly.MethodCounter}";   // technically we should jump here after executing any finally/etc

                            assembly.AddAsm($"jmp {finallyJmp}");
                        }
                        else throw new Exception("Unhandled EHTable flag (II.25.4.6)");

                        break;

                    // CONV.U
                    case 0xE0:
                        if (_stack.Peek().Type != ElementType.EType.Pinned && !_stack.Peek().Is32BitCapable(pe.Metadata))
                            throw new Exception("Unsupported type");
                        // there is no difference for us between managed and unmanaged pointers, so this is a nop
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

                    // CLT and CLT.UN
                    case 0xFE04:
                    case 0xFE05:
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

                    // LDFTN
                    case 0xFE06:
                        CALL(pe, assembly, code, ref i, ldftn: true);
                        break;

                    // INITOBJ
                    case 0xFE15:
                        INITOBJ(pe, assembly, code, ref i);
                        break;

                    // SIZEOF
                    case 0xFE1C:
                        uint type = BitConverter.ToUInt32(code, i);
                        i += 4;

                        var sizeOfType = _runtime.GetTypeSize(pe.Metadata, new ElementType(ElementType.EType.ValueType, type));

                        assembly.AddAsm($"push {sizeOfType}");
                        _stack.Push(new ElementType(ElementType.EType.U4));
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

            if (_stack.Count > 0 && _stack.Count != (method.LocalVars?.LocalVariables.Length ?? 0))
                throw new Exception("Unbalanced stack");
            _stack.Clear();

            ProcessStaticConstructor(pe, methodDef);

            /*while (_methodsToCompile.Count > 0)
            {
                var m = _methodsToCompile[0];
                _methodsToCompile.RemoveAt(0);
                Assemble(pe, m);
            }*/
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

        private void Branch(int jmpTo, AssembledMethod assembly, CLIMetadata metadata, string asm32jmpType, string asmR4jmpType)
        {
            //_sbyte = (sbyte)code[i++];
            _jmpLabel = $"IL_{jmpTo.ToString("X4")}_{assembly.MethodCounter}";

            if (_stack.Peek().Is32BitCapable(metadata))
            {
                assembly.AddAsm("pop eax");        // value2
                assembly.AddAsm("pop ebx");        // value1
                assembly.AddAsm("cmp ebx, eax");    // compare values
                assembly.AddAsm($"{asm32jmpType} {_jmpLabel}");
            }
            else if (_stack.Peek().Type == ElementType.EType.R4)
            {
                assembly.AddAsm("fld dword [esp]");
                assembly.AddAsm("fld dword [esp + 4]");
                assembly.AddAsm("fcomip");
                assembly.AddAsm("pop eax"); // remove one of the R4s from the stack
                assembly.AddAsm("pop ebx"); // remove one of the R4s from the stack
                assembly.AddAsm($"{asmR4jmpType} {_jmpLabel}");
            }
            else
            {
                throw new Exception("Unsupported type");
            }

            eaxType = _stack.Pop();
            ebxType = _stack.Pop();
        }

        private void AddStackFrame(AssembledMethod assembly)
        {
            if (assembly.HasStackFrame) return;

            int offset = 0;
            if (assembly.Assembly[0].StartsWith(assembly.ToAsmString(assembly.GenericInstSig))) offset = 1;

            assembly.HasStackFrame = true;
            assembly.Assembly.Insert(offset, "push ebp");
            assembly.Assembly.Insert(offset + 1, "mov ebp, esp");

            // fix up any existing ret calls by prepending "pop ebp" before "ret"
            for (int i = 0; i < assembly.Assembly.Count; i++)
            {
                if (assembly.Assembly[i].TrimStart().StartsWith("ret "))
                    assembly.Assembly.Insert(i++, "pop ebp");
            }
        }

        private void STLOC(byte b, AssembledMethod assembly)
        {
            AddStackFrame(assembly);
            /*if (b == 0) assembly.AddAsm("pop ecx");
            else if (b == 1) assembly.AddAsm("pop edx");
            else*/
            {
                assembly.AddAsm("pop eax");
                /*if (_methods.Count <= 1) assembly.AddAsm($"mov [ebp - {(b + 1 - 2) * BytesPerRegister}], eax");
                else*/
                assembly.AddAsm($"mov [ebp - {(b + 1) * BytesPerRegister}], eax");
            }
            if (b > 1) eaxType = _stack.Pop();
            else _stack.Pop();
        }

        private void LDLOC(byte b, AssembledMethod assembly)
        {
            AddStackFrame(assembly);
            //if (b == 0) assembly.AddAsm("push ecx");
            //else if (b == 1) assembly.AddAsm("push edx");
            //else
            {
                /*if (_methods.Count <= 1) assembly.AddAsm($"mov eax, [ebp - {(b + 1 - 2) * BytesPerRegister}]");
                else*/
                assembly.AddAsm($"mov eax, [ebp - {(b + 1) * BytesPerRegister}]");
                assembly.AddAsm("push eax");
                eaxType = assembly.Method.LocalVars.LocalVariables[b];
            }
            _stack.Push(assembly.Method.LocalVars.LocalVariables[b]);
        }

        private void LDLOCA(byte b, AssembledMethod assembly)
        {
            AddStackFrame(assembly);
            /*assembly.AddAsm($"mov eax, ebp");
            assembly.AddAsm($"sub eax, {(b + 1) * BytesPerRegister}");*/
            assembly.AddAsm($"lea eax, [ebp - {(b + 1) * BytesPerRegister}]");
            assembly.AddAsm("push eax");
            eaxType = assembly.Method.LocalVars.LocalVariables[b];
            _stack.Push(assembly.Method.LocalVars.LocalVariables[b]);
        }

        private void LDARG(int s, AssembledMethod assembly, List<StackElementType> callingStackTypes, int callingStackSize, MethodDefLayout methodDef)
        {
            AddStackFrame(assembly);
            //if (methodDef.MethodSignature.Flags.HasFlag(SigFlags.HASTHIS)) throw new Exception("Verify this is working");
            var arg = callingStackTypes[s];
            _int = 1 + callingStackSize / 4 - arg.StackLocation / 4;
            for (int b = 0; b < Math.Ceiling(arg.SizeInBytes / 4f); b++)
            {
                assembly.AddAsm($"mov eax, [ebp + {BytesPerRegister * (_int - b)}]");
                assembly.AddAsm("push eax");
            }
            _stack.Push(arg.Type);
        }

        private void STARG(int s, AssembledMethod assembly, CLIMetadata metadata, List<StackElementType> callingStackTypes, int callingStackSize, MethodDefLayout methodDef)
        {
            AddStackFrame(assembly);
            //if (methodDef.MethodSignature.Flags.HasFlag(SigFlags.HASTHIS)) throw new Exception("Verify this is working");
            var arg = callingStackTypes[s];
            _int = 1 + callingStackSize / 4 - arg.StackLocation / 4;
            if (!IsEquivalentType(metadata, _stack.Peek(), arg.Type)) throw new Exception("Type mismatch");
            for (int b = 0; b < Math.Ceiling(arg.SizeInBytes / 4f); b++)
            {
                assembly.AddAsm("pop eax");
                assembly.AddAsm($"mov [ebp + {BytesPerRegister * (_int - b)}], eax");
            }
            eaxType = _stack.Pop();
        }

        private void STELEM(int sizeInBytes, AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            ebxType = _stack.Pop(); // the value (esp+0)
            _stack.Pop();           // the index (esp+4)
            eaxType = _stack.Pop(); // the array (esp+8)

            if (eaxType.Type != ElementType.EType.SzArray /*|| !eaxType.NestedType.Is32BitCapable(metadata)*/) throw new Exception("Unsupported type");

            // replace var/mvar with appropriate type (hack for now)
            if (eaxType.NestedType.Type == ElementType.EType.Var || eaxType.NestedType.Type == ElementType.EType.MVar)
            {
                var varType = BitConverter.ToUInt32(code, i);   // TODO:  Actually look this up in the metadata
                i += 4;

                if (assembly.GenericInstSig != null)
                {
                    eaxType.NestedType = assembly.GenericInstSig.Params[0];
                }
            }

            var sizePerElement = _runtime.GetTypeSize(metadata, eaxType.NestedType);
            if (sizeInBytes != 0 && sizePerElement != sizeInBytes) throw new Exception("Unsupported type");

            int shl = 0, size = 1;
            while (size < sizePerElement)
            {
                shl++;
                size <<= 1;
            }
            if (size != sizePerElement) throw new Exception("Unsupported type");

            assembly.AddAsm("pop ecx");
            assembly.AddAsm("pop eax");    // get index
            //if (shl > 0) assembly.AddAsm($"shl eax, {shl}");          // multiply by 'shl' to get offset
            assembly.AddAsm("pop ebx");    // get address of array
            //assembly.AddAsm("add eax, ebx");        // now we have the final address
            //assembly.AddAsm($"lea eax, [8 + ebx + {size} * eax]");
            LeaOrMul_EBXOffset(assembly, 8, size);
            //assembly.AddAsm("pop ebx");             // pop value off the stack
            if (sizePerElement == 1)
            {
                assembly.AddAsm("mov [eax], cl");
            }
            else if (sizePerElement == 2)
            {
                assembly.AddAsm("mov [eax], cx");
            }
            else if (sizePerElement == 4)
            {
                assembly.AddAsm("mov [eax], ecx");
            }
            else throw new Exception("Unsupported type");
            //assembly.AddAsm("add esp, 8");          // clean up the stack
        }

        private void LeaOrMul_EBXOffset(AssembledMethod assembly, int offset, int size)
        {
            int temp = size;
            while ((temp & 1) == 0) temp = temp >> 1;

            if (temp == 1)
            {
                // this was a power of 2, so we can use lea
                assembly.AddAsm($"lea eax, [{offset} + ebx + {size} * eax]");
            }
            else
            {
                assembly.AddAsm("push edx");    // mul clobbers edx
                assembly.AddAsm($"mov edx, {size}");
                assembly.AddAsm("mul edx");
                assembly.AddAsm("pop edx");
                assembly.AddAsm($"lea eax, [{offset} + eax + ebx]");
                //assembly.AddAsm("add eax, ebx");
                //assembly.AddAsm($"add eax, {offset}");
            }
        }

        private void LeaOrMul(AssembledMethod assembly, int offset, int size)
        {
            int temp = size;
            while ((temp & 1) == 0) temp = temp >> 1;

            if (temp == 1)
            {
                // this was a power of 2, so we can use lea
                assembly.AddAsm($"lea eax, [{offset} + {size} * eax]");
            }
            else
            {
                assembly.AddAsm("push edx");    // mul clobbers edx
                assembly.AddAsm($"mov edx, {size}");
                assembly.AddAsm("mul edx");
                assembly.AddAsm("pop edx");
                assembly.AddAsm($"add eax, {offset}");
            }
        }

        private void LDELEM(int sizeInBytes, AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            ebxType = _stack.Pop(); // the index (esp+0)
            eaxType = _stack.Pop(); // the array (esp+4)

            if (eaxType.Type != ElementType.EType.SzArray /*|| !eaxType.NestedType.Is32BitCapable(metadata)*/) throw new Exception("Unsupported type");

            // replace var/mvar with appropriate type (hack for now)
            if (eaxType.NestedType.Type == ElementType.EType.Var || eaxType.NestedType.Type == ElementType.EType.MVar)
            {
                var varType = BitConverter.ToUInt32(code, i);   // TODO:  Actually look this up in the metadata
                i += 4;

                if (assembly.GenericInstSig != null)
                {
                    eaxType.NestedType = assembly.GenericInstSig.Params[0];
                }
            }

            var sizePerElement = _runtime.GetTypeSize(metadata, eaxType.NestedType);
            if (sizeInBytes != 0 && sizePerElement != sizeInBytes) throw new Exception("Unsupported type");

            int shl = 0, size = 1;
            while (size < sizePerElement)
            {
                shl++;
                size <<= 1;
            }
            if (size != sizePerElement) throw new Exception("Unsupported type");

            assembly.AddAsm("pop eax");             // get index
            //if (shl > 0) assembly.AddAsm($"shl eax, {shl}");    // multiply by 'shl' to get offset
            assembly.AddAsm("pop ebx");             // get address of array
            //assembly.AddAsm("add eax, ebx");        // now we have the final address
            //assembly.AddAsm($"lea eax, [8 + ebx + {size} * eax]");
            LeaOrMul_EBXOffset(assembly, 8, size);
            assembly.AddAsm("mov ebx, [eax]");    // bring value from memory to register (+8 to skip array length and stride)

            if (sizePerElement == 2) assembly.AddAsm("and ebx, 65535");
            else if (sizePerElement == 1) assembly.AddAsm("and ebx, 255");
            else if (sizePerElement != 4) throw new Exception("Unsupported type");
            assembly.AddAsm("push ebx");

            ebxType = eaxType.NestedType;
            _stack.Push(ebxType);
        }

        private void LDELEMA(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i, bool ldelem_ref = false)
        {
            uint type = 0;
            if (!ldelem_ref)
            {
                type = BitConverter.ToUInt32(code, i);
                i += 4;
            }

            /*assembly.AddAsm("pop ebx"); // index
            ebxType = _stack.Pop();
            assembly.AddAsm("pop eax"); // array
            eaxType = _stack.Pop();
            
            if (eaxType.Type != ElementType.EType.SzArray) throw new Exception("Unsupported operation");
            if (!ldelem_ref && eaxType.NestedType.Token != type) throw new Exception("Type mismatch");
            
            var sizePerElement = _runtime.GetTypeSize(metadata, eaxType.NestedType);*/

            assembly.AddAsm("pop eax"); // index
            eaxType = _stack.Pop();
            assembly.AddAsm("pop ebx"); // array
            ebxType = _stack.Pop();

            if (ebxType.Type != ElementType.EType.SzArray) throw new Exception("Unsupported operation");
            if (!ldelem_ref && ebxType.NestedType.Token != type) throw new Exception("Type mismatch");

            var sizePerElement = _runtime.GetTypeSize(metadata, ebxType.NestedType);

            // going to use ecx to store the array position
            /*assembly.AddAsm("push ecx");
            assembly.AddAsm("mov ecx, eax");

            // edx will get clobbered by the multiply
            assembly.AddAsm("push edx");

            assembly.AddAsm($"mov eax, {sizePerElement}");
            assembly.AddAsm("mul ebx");
            assembly.AddAsm("add eax, ecx");
            assembly.AddAsm("add eax, 4");  // the first 4 bytes are the array size, so skip over that

            assembly.AddAsm("pop edx");
            assembly.AddAsm("pop ecx");*/
            //assembly.AddAsm($"lea eax, [8 + ebx + {sizePerElement} * eax]");
            LeaOrMul_EBXOffset(assembly, 8, sizePerElement);

            assembly.AddAsm("push eax");
            //eaxType = eaxType.NestedType;
            eaxType = ebxType.NestedType;
            if (eaxType.Type == ElementType.EType.ValueType) eaxType = new ElementType(ElementType.EType.ByRefValueType);
            _stack.Push(eaxType);
        }

        private void STFLD(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint fieldToken = BitConverter.ToUInt32(code, i);
            i += 4;

            int offset = _runtime.GetFieldOffset(metadata, fieldToken);
            var type = _runtime.GetFieldType(metadata, fieldToken);

            assembly.AddAsm("pop eax");
            assembly.AddAsm("pop ebx");

            eaxType = _stack.Pop();
            ebxType = _stack.Pop();

            if (!ebxType.Is32BitCapable(metadata))
                throw new Exception("Unsupported type");
            if (!IsEquivalentType(metadata, eaxType, type) && (!eaxType.Is32BitCapable(metadata) || !type.Is32BitCapable(metadata)))
                throw new Exception("Mismatched types");

            if (type.Type == ElementType.EType.GenericInst && type.NestedType != null)
                type = type.NestedType;

            if (type.Type == ElementType.EType.U2 || type.Type == ElementType.EType.I2 || type.Type == ElementType.EType.Char)
            {
                if (offset == 0) assembly.AddAsm("mov word [ebx], ax");
                else assembly.AddAsm($"mov word [ebx + {offset}], ax");
            }
            else if (type.Type == ElementType.EType.U1 || type.Type == ElementType.EType.I1 || type.Type == ElementType.EType.Boolean)
            {
                if (offset == 0) assembly.AddAsm("mov byte [ebx], al");
                else assembly.AddAsm($"mov byte [ebx + {offset}], al");
            }
            else if (type.Type == ElementType.EType.U8 || type.Type == ElementType.EType.I8 || type.Type == ElementType.EType.R8)
            {
                throw new Exception("Unsupported type");
            }
            else if (type.Is32BitCapable(metadata))//type.Type == ElementType.EType.U4 || type.Type == ElementType.EType.I4 || type.Type == ElementType.EType.R4 || type.Type == ElementType.EType.SzArray)
            {
                if (offset == 0) assembly.AddAsm("mov [ebx], eax");
                else assembly.AddAsm($"mov [ebx + {offset}], eax");
            }
            else
            {
                throw new Exception("Unsupported type");
            }
        }

        private void LDSTR(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint metadataToken = BitConverter.ToUInt32(code, i);
            i += 4;

            string label = $"BLOB_{metadataToken.ToString("X")}";

            string s = Encoding.Unicode.GetString(metadata.GetMetadata(metadataToken));

            if (!_initializedData.ContainsKey(label)) _initializedData.Add(label, new DataType(ElementType.EType.String, s));
            assembly.AddAsm($"push {label}");

            _stack.Push(new ElementType(ElementType.EType.String));
        }

        private void MovToEaxExtended(AssembledMethod assembly, CLIMetadata metadata, ElementType type, string address)
        {
            if (type.Type == ElementType.EType.U1 || type.Type == ElementType.EType.Boolean)
            {
                assembly.AddAsm($"movzx eax, byte {address}");
            }
            else if (type.Type == ElementType.EType.I1)
            {
                assembly.AddAsm($"movsx eax, byte {address}");
            }
            else if (type.Type == ElementType.EType.U2 || type.Type == ElementType.EType.Char)
            {
                assembly.AddAsm($"movzx eax, word {address}");
            }
            else if (type.Type == ElementType.EType.I2)
            {
                assembly.AddAsm($"movsx eax, word {address}");
            }
            else if (type.Type == ElementType.EType.U8 || type.Type == ElementType.EType.I8 || type.Type == ElementType.EType.R8)
            {
                throw new Exception("Unsupported type");
            }
            else if (type.Is32BitCapable(metadata))
            {
                assembly.AddAsm($"mov eax, {address}");
            }
            else
            {
                throw new Exception("Unsupported type");
            }
        }

        private void LDFLD(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint fieldToken = BitConverter.ToUInt32(code, i);
            i += 4;

            int offset = _runtime.GetFieldOffset(metadata, fieldToken);
            var type = _runtime.GetFieldType(metadata, fieldToken);

            ebxType = _stack.Pop();

            if (ebxType.Type == ElementType.EType.ValueType)
            {
                int ebxSize = _runtime.GetTypeSize(metadata, ebxType);

                MovToEaxExtended(assembly, metadata, type, $"[esp + {offset}]");

                // if this is just 4 or 8 bytes then we can pop once or twice, but otherwise manipulate esp directly
                if (ebxSize <= 8)
                {
                    for (int b = 0; b < Math.Ceiling(ebxSize / 4f); b++)
                        assembly.AddAsm("pop ebx");
                }
                else
                {
                    assembly.AddAsm($"add esp, {ebxSize}");
                }
            }
            else
            {
                if (!ebxType.Is32BitCapable(metadata))
                    throw new Exception("Unsupported type");

                if (type.Type == ElementType.EType.GenericInst && type.NestedType != null)
                    type = type.NestedType;

                assembly.AddAsm("pop ebx");

                if (offset == 0) MovToEaxExtended(assembly, metadata, type, "[ebx]");
                else MovToEaxExtended(assembly, metadata, type, $"[ebx + {offset}]");
            }

            assembly.AddAsm("push eax");

            //ebxType = _stack.Pop();
            eaxType = type;
            _stack.Push(eaxType);
        }

        private void LDFLDA(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint fieldToken = BitConverter.ToUInt32(code, i);
            i += 4;

            int offset = _runtime.GetFieldOffset(metadata, fieldToken);
            var type = _runtime.GetFieldType(metadata, fieldToken);

            if (!type.Is32BitCapable(metadata)) throw new Exception("Unsupported type");

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

        private string GetStaticLabel(int addr, CLIMetadata metadata)
        {
            if ((addr & 0xff000000) == 0x04000000)
            {
                var field = metadata.Fields[(addr & 0x00ffffff) - 1];
                var parent = field.Parent;

                return $"{parent.FullName}.{field.Name}".Replace('.', '_').Replace('<', '_').Replace('>', '_');
            }
            else if ((addr & 0xff000000) == 0x0a000000)
            {
                var memberref = metadata.MemberRefs[(addr & 0x00ffffff) - 1];
                var parent = memberref.ParentName;

                return $"{parent}.{memberref.Name}".Replace('.', '_').Replace('<', '_').Replace('>', '_');
            }
            else
            {
                throw new Exception("Unexpected static type");
            }
        }

        private void STSFLD(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            int addr = BitConverter.ToInt32(code, i);
            i += 4;

            string label = GetStaticLabel(addr, metadata);

            if (!_initializedData.ContainsKey(label)) AddStaticField(metadata, label, addr);
            var labelType = _initializedData[label].Type;

            if (!labelType.Is32BitCapable(metadata)) throw new Exception("Unsupported type");

            eaxType = _stack.Pop();
            if (!IsEquivalentType(metadata, labelType, eaxType))
            {
                // looks like .net is okay with an implicit cast from i4 to boolean ... weird
                if (labelType.Type == ElementType.EType.Boolean && eaxType.Type == ElementType.EType.I4)
                {
                    assembly.AddAsm("pop eax");
                    assembly.AddAsm($"mov byte [{label}], al");
                }
                else if (labelType.Type == ElementType.EType.Char && (eaxType.Type == ElementType.EType.U2 || eaxType.Type == ElementType.EType.I4))
                {
                    assembly.AddAsm("pop eax");
                    assembly.AddAsm($"mov word [{label}], ax");
                }
                else if (labelType.Type == ElementType.EType.Class)
                {
                    assembly.AddAsm("pop eax");
                    assembly.AddAsm($"mov [{label}], eax");
                }
                else throw new Exception("Unsupported type");
            }
            else
            {
                if (labelType.Type == ElementType.EType.U1 || labelType.Type == ElementType.EType.I1 || labelType.Type == ElementType.EType.Boolean)
                {
                    assembly.AddAsm($"pop eax");
                    assembly.AddAsm($"mov [{label}], al");
                }
                else if (labelType.Type == ElementType.EType.U2 || labelType.Type == ElementType.EType.I2 || labelType.Type == ElementType.EType.Char)
                {
                    assembly.AddAsm($"pop eax");
                    assembly.AddAsm($"mov [{label}], ax");
                }
                else
                {
                    assembly.AddAsm($"pop eax");
                    assembly.AddAsm($"mov [{label}], eax");
                }
            }
        }

        private void BOX(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint token = BitConverter.ToUInt32(code, i);
            i += 4;

            var type = _runtime.GetType(metadata, token);
            if (type.Type == ElementType.EType.Var || type.Type == ElementType.EType.MVar)
                type = assembly.GenericInstSig.Params[0];

            if (_stack.Peek() != type) throw new Exception("Unsupported type");

            // if the types match then there's nothing to do
        }

        private void STOBJ(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint token = BitConverter.ToUInt32(code, i);
            i += 4;

            var type = _runtime.GetType(metadata, token);
            if ((type.Type == ElementType.EType.Var || type.Type == ElementType.EType.MVar) && assembly.GenericInstSig != null)
                type = assembly.GenericInstSig.Params[0];
            var typeSize = _runtime.GetTypeSize(metadata, type);

            //if ((typeSize % 4) != 0) throw new Exception("Unsupported type");

            assembly.AddAsm("pop eax"); // the thing to copy
            assembly.AddAsm("pop ebx"); // the address to copy it to

            eaxType = _stack.Pop();
            ebxType = _stack.Pop();

            if (!ebxType.IsPointer()) throw new Exception("ebx should have been a pointer");

            //for (int b = 0; b < Math.Ceiling(typeSize / 4f); b++)
            //    assembly.AddAsm($"mov [ebx+{b * BytesPerRegister}], [eax+{b * BytesPerRegister}]");
            assembly.AddAsm("mov [ebx], eax");
        }

        /*private void LDOBJ(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint token = BitConverter.ToUInt32(code, i);
            i += 4;

            var type = _runtime.GetType(metadata, token);
            if (type.Type == ElementType.EType.Var || type.Type == ElementType.EType.MVar)
                type = assembly.GenericInstSig.Params[0];
            var typeSize = _runtime.GetTypeSize(metadata, type);

            if ((typeSize % 4) != 0) throw new Exception("Unsupported type");

            assembly.AddAsm("pop eax"); // the thing to copy
            assembly.AddAsm("pop ebx"); // the address to copy it to

            for (int b = 0; b < Math.Ceiling(typeSize / 4f); b++)
                assembly.AddAsm($"mov [ebx+{b * BytesPerRegister}], [eax+{b * BytesPerRegister}]");

            eaxType = _stack.Pop();
            ebxType = _stack.Pop();
        }*/

        private bool IsEquivalentType(CLIMetadata metadata, ElementType type1, ElementType type2)
        {
            if (type1.Type == ElementType.EType.MVar || type2.Type == ElementType.EType.MVar) return true;
            if (type1 == type2) return true;

            if (type1.Type == ElementType.EType.U4 || type1.Type == ElementType.EType.I4)
                return (type2.Type == ElementType.EType.U4 || type2.Type == ElementType.EType.I4);

            // U1 and U4 seem to be compatible...
            if ((type1.Type == ElementType.EType.U4 && type2.Type == ElementType.EType.U1) ||
                (type1.Type == ElementType.EType.U1 && type2.Type == ElementType.EType.U4)) return true;
            if ((type1.Type == ElementType.EType.U4 && type2.Type == ElementType.EType.I1) ||
                (type1.Type == ElementType.EType.I1 && type2.Type == ElementType.EType.U4)) return true;

            if (type1.Type == ElementType.EType.Class && type2.Type == ElementType.EType.Class)
            {
                if ((type1.Token & 0xff000000) == 0x02000000)
                {
                    var tdef = metadata.TypeDefs[(int)(type1.Token & 0x00ffffff) - 1];
                    if (tdef.Implements.Contains(type2.Token)) return true;
                }
                if ((type2.Token & 0xff000000) == 0x02000000)
                {
                    var tdef = metadata.TypeDefs[(int)(type2.Token & 0x00ffffff) - 1];
                    if (tdef.Implements.Contains(type1.Token)) return true;
                }
            }

            return false;
        }

        private void LDSFLD(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            int addr = BitConverter.ToInt32(code, i);
            i += 4;

            string label = GetStaticLabel(addr, metadata);

            if (!_initializedData.ContainsKey(label)) AddStaticField(metadata, label, addr);
            var labelType = _initializedData[label].Type;

            //if (!labelType.Is32BitCapable(metadata)) throw new Exception("Unsupported type");

            eaxType = _initializedData[label].Type;
            _stack.Push(eaxType);

            if (eaxType.Type == ElementType.EType.SzArray)// || eaxType.Type == ElementType.EType.ValueType)
            {
                // if the data is stored as part of the program data then pass the label directly
                // but if we are storing an address only then we need to redirect
                if (_initializedData[label].Data is string || _initializedData[label].Data is byte[])
                {
                    assembly.AddAsm($"mov eax, {label}");
                    assembly.AddAsm($"push eax");
                }
                else
                {
                    assembly.AddAsm($"mov eax, [{label}]");
                    assembly.AddAsm($"push eax");
                }
            }
            else if (eaxType.Type == ElementType.EType.ValueType)
            {
                var sizeOf = _runtime.GetTypeSize(metadata, eaxType);
                for (int b = (int)Math.Ceiling(sizeOf / 4f) - 1; b >= 0; b--)
                //for (int b = 0; b < Math.Ceiling(sizeOf / 4f); b++)
                {
                    assembly.AddAsm($"mov eax, [{label} + {4 * b}]");
                    assembly.AddAsm($"push eax");
                }
            }
            else
            {
                MovToEaxExtended(assembly, metadata, eaxType, $"[{label}]");
                assembly.AddAsm("push eax");
            }
        }

        private void LDSFLDA(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            int addr = BitConverter.ToInt32(code, i);
            i += 4;

            string label = GetStaticLabel(addr, metadata);

            if ((addr & 0xff000000) == 0x04000000)
            {
                var field = metadata.Fields[(addr & 0x00ffffff) - 1];

                if (field.Type.Type == ElementType.EType.ValueType) _stack.Push(new ElementType(ElementType.EType.ByRefValueType));
                else if (field.Type.Is32BitCapable(metadata)) _stack.Push(field.Type);
                else throw new Exception("Unsupported type (likely ByRef)");

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

            //_stack.Push(new ElementType(ElementType.EType.ByRef));
            assembly.AddAsm($"push {label}");
        }

        private void AddStaticField(CLIMetadata metadata, string label, ElementType type)
        {
            if (_initializedData.ContainsKey(label)) return;

            if (type.Type == ElementType.EType.GenericInst)
            {
                type = type.NestedType;
            }

            switch (type.Type)
            {
                case ElementType.EType.Boolean:
                case ElementType.EType.U1: _initializedData.Add(label, new DataType(type, (byte)0)); break;
                case ElementType.EType.I1: _initializedData.Add(label, new DataType(type, (sbyte)0)); break;
                case ElementType.EType.U2: _initializedData.Add(label, new DataType(type, (ushort)0)); break;
                case ElementType.EType.I2: _initializedData.Add(label, new DataType(type, (short)0)); break;
                case ElementType.EType.Char: _initializedData.Add(label, new DataType(type, (char)0)); break;
                case ElementType.EType.U4: _initializedData.Add(label, new DataType(type, (uint)0)); break;
                case ElementType.EType.I4: _initializedData.Add(label, new DataType(type, (int)0)); break;
                case ElementType.EType.Class:
                case ElementType.EType.SzArray: _initializedData.Add(label, new DataType(type, (uint)0)); break;  // just a pointer
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

                if (field.Name == "ISR_ADDRESSES")
                {
                    if (ISR.AddISRMethods(this))
                    {
                        // special case for inserting the 32 ISR addresses for the kernel
                        StringBuilder isrNames = new StringBuilder();
                        isrNames.Append("32");  // size of array
                        isrNames.Append(", 4");   // size per element
                        for (int i = 0; i < 32; i++) isrNames.Append($", ISR{i}");
                        var isrType = new ElementType(ElementType.EType.SzArray);
                        isrType.NestedType = new ElementType(ElementType.EType.I4);
                        _initializedData.Add(label, new DataType(isrType, isrNames.ToString()));
                    }
                }
                else if (field.Name == "IRQ_ADDRESSES")
                {
                    if (ISR.AddISRMethods(this, true))
                    {
                        // special case for inserting the 32 ISR addresses for the kernel
                        StringBuilder irqNames = new StringBuilder();
                        irqNames.Append("16");  // size of array
                        irqNames.Append(", 4");   // size per element
                        for (int i = 0; i < 16; i++) irqNames.Append($", IRQ{i}");
                        var irqType = new ElementType(ElementType.EType.SzArray);
                        irqType.NestedType = new ElementType(ElementType.EType.I4);
                        _initializedData.Add(label, new DataType(irqType, irqNames.ToString()));
                    }
                }
                else if ((field.flags & FieldLayout.FieldLayoutFlags.Static) == FieldLayout.FieldLayoutFlags.Static)
                {
                    AddStaticField(metadata, label, field.Type);
                }
                else
                {
                    throw new Exception("Incomplete implementation");
                }
            }
            else if ((fieldToken & 0xff000000) == 0x0a000000)
            {
                var memberref = metadata.MemberRefs[(fieldToken & 0x00ffffff) - 1];
                AddStaticField(metadata, label, memberref.Signature.RetType);
            }
            else throw new Exception("Unexpected table found when trying to find a field.");
        }

        private void INITOBJ(PortableExecutableFile pe, AssembledMethod assembly, byte[] code, ref ushort i)
        {
            var metadata = pe.Metadata;

            var type = _runtime.GetType(metadata, BitConverter.ToUInt32(code, i));
            i += 4;

            if ((type.Type == ElementType.EType.Var || type.Type == ElementType.EType.MVar) && assembly.GenericInstSig != null)
                type = assembly.GenericInstSig.Params[0];

            eaxType = _stack.Pop();
            assembly.AddAsm("pop eax");

            if (!eaxType.IsPointer()) throw new Exception("Unsupported type");

            var sizeOfType = _runtime.GetTypeSize(metadata, type);

            if ((sizeOfType % 4) != 0) throw new Exception("Unsupported type");

            for (int b = 0; b < Math.Ceiling(sizeOfType / 4f); b++) assembly.AddAsm($"mov dword [eax+{b * 4}], 0");
        }

        public string HeapAllocatorMethod { get; set; }
        public string ThrowExceptionMethod { get; set; }

        private void NEWOBJ(PortableExecutableFile pe, AssembledMethod assembly, byte[] code, ref ushort i)
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

            var genericOverride = GetNonGenericEquivalent(metadata, methodDesc);
            if (genericOverride.Item2 != null) methodDesc = genericOverride.Item1;

            if ((methodDesc & 0xff000000) == 0x0a000000)
            {
                var memberRef = metadata.MemberRefs[(int)(methodDesc & 0x00ffffff) - 1];
                var memberName = memberRef.ToAsmString();
                if (!string.IsNullOrEmpty(generic)) memberName = memberName.Substring(0, memberName.IndexOf("_")) + generic + memberName.Substring(memberName.IndexOf("_"));

                // eax and ebx may have been clobbered
                eaxType = null;
                ebxType = null;

                assembly.AddAsm($"; start {memberName} plug");

                if (memberName == "System.Action..ctor_Void_Object_IntPtr" || memberName == "System.Action`1..ctor_Void_Object_IntPtr")
                {
                    _stack.Pop();
                    _stack.Pop();

                    if (string.IsNullOrEmpty(HeapAllocatorMethod)) throw new Exception("Need heap allocator");

                    assembly.AddAsm("push 8");
                    assembly.AddAsm("push 0");
                    assembly.AddAsm($"call {HeapAllocatorMethod}");

                    assembly.AddAsm("pop ebx");         // the function ptr
                    //assembly.AddAsm($"add esp, {BytesPerRegister}");    // the object ptr (normally null, we don't use this yet)
                    assembly.AddAsm("mov [eax], ebx");  // the object only stores the function pointer
                    assembly.AddAsm("pop ebx");
                    assembly.AddAsm("mov [eax + 4], ebx");  // store the object ptr
                    assembly.AddAsm("push eax");

                    _stack.Push(new ElementType(ElementType.EType.Class, memberRef.Parent));

                    //assembly.AddAsm("pop eax"); // function pointer
                    //assembly.AddAsm("pop ebx"); // this (which is always null)
                }
                else if (memberName.EndsWith("Exception..ctor_Void_String"))
                {
                    _stack.Pop();

                    if (string.IsNullOrEmpty(HeapAllocatorMethod)) throw new Exception("Need heap allocator");

                    assembly.AddAsm("push 8");
                    assembly.AddAsm("push 0");
                    assembly.AddAsm($"call {HeapAllocatorMethod}");

                    assembly.AddAsm("pop ebx");         // the function ptr
                    assembly.AddAsm($"add esp, {BytesPerRegister}");    // the object ptr (normally null, we don't use this yet)
                    assembly.AddAsm("mov [eax], ebx");  // the object only stores the function pointer
                    assembly.AddAsm("pop ebx");
                    assembly.AddAsm("mov [eax+4], ebx");    // put the string address into the next slot
                    assembly.AddAsm("push eax");

                    _stack.Push(new ElementType(ElementType.EType.Class, memberRef.Parent));
                }
                else if (memberName.EndsWith("Exception..ctor_Void"))
                {
                    var split = memberName.Split('.');
                    var exceptionType = split[1];

                    if (string.IsNullOrEmpty(HeapAllocatorMethod)) throw new Exception("Need heap allocator");

                    assembly.AddAsm("push 8");
                    assembly.AddAsm("push 0");
                    assembly.AddAsm($"call {HeapAllocatorMethod}");

                    assembly.AddAsm("pop ebx");         // the function ptr
                    assembly.AddAsm($"add esp, {BytesPerRegister}");    // the object ptr (normally null, we don't use this yet)

                    string label = "Name_" + exceptionType;
                    if (!_initializedData.ContainsKey(label)) _initializedData.Add(label, new DataType(ElementType.EType.String, exceptionType));

                    assembly.AddAsm($"mov dword [eax], {label}");  // put the string in for the exception message

                    assembly.AddAsm("push eax");

                    _stack.Push(new ElementType(ElementType.EType.Class, memberRef.Parent));
                }
                else if (memberName == "System.String..ctor_Void_SzArray")
                {
                    var type = _stack.Pop();
                    if (type.NestedType == null || type.NestedType.Type != ElementType.EType.Char)
                        throw new Exception("Unsupported type");
                    assembly.AddAsm("; do nothing");
                    _stack.Push(new ElementType(ElementType.EType.String));
                }
                else
                {
                    // try to load this in
                    var file = memberName.Substring(0, memberName.IndexOf('.'));
                    var path = $"{Environment.CurrentDirectory}\\{file}.dll";

                    if (File.Exists(path))
                    {
                        /*if (!_loadedAssemblies.ContainsKey(path)) _loadedAssemblies[path] = Assembly.LoadFile(path);

                        var type = _loadedAssemblies[path].GetTypes().
                            Where(t => t.FullName == memberRef.ParentName).SingleOrDefault();

                        if (type.BaseType.Name == "Object")
                        {
                            
                        }
                        else throw new Exception("Unable to handle this method");*/
                        var referencedAssembly = _runtime.Assemblies.Where(a => string.Compare(path, Path.GetFullPath(a.Filename).TrimEnd('\\'), StringComparison.InvariantCultureIgnoreCase) == 0).SingleOrDefault();

                        if (referencedAssembly == null)
                        {
                            referencedAssembly = new PortableExecutableFile(path);
                            AddAssembly(referencedAssembly);
                        }

                        var type = referencedAssembly.Metadata.MethodDefs.Where(m => m.Name == memberRef.Name && m.Parent.FullName == memberRef.ParentName).SingleOrDefault();

                        if (type != null) NEWOBJCWithMethodDef(pe, assembly, code, ref i, type);
                        else throw new Exception("Unable to handle this method");
                    }
                    else throw new Exception("Unable to handle this method");
                }
                assembly.AddAsm("; end plug");

                if (memberRef.Signature.RetType.Type != ElementType.EType.Void)
                    _stack.Push(memberRef.Signature.RetType);
            }
            else if ((methodDesc & 0xff000000) == 0x06000000)
            {
                var method = metadata.MethodDefs[(int)(methodDesc & 0x00ffffff) - 1];

                // if we have a generic override then clone this memberref and replace values of mvar or var with the known parameter
                if (genericOverride.Item2 != null)
                {
                    method = method.Clone(metadata);
                    method.Signature.Override(genericOverride.Item2);
                }

                NEWOBJCWithMethodDef(pe, assembly, code, ref i, method);
            }
            else throw new Exception("Unsupported");
        }

        private void NEWOBJCWithMethodDef(PortableExecutableFile pe, AssembledMethod assembly, byte[] code, ref ushort i, MethodDefLayout method)
        {
            var metadata = pe.Metadata;

            var parent = method.Parent;
            int objSize = 0;
            foreach (var f in parent.Fields)
            {
                var fSize = _runtime.GetTypeSize(metadata, f.Type);
                //if ((fSize % 4) != 0) fSize += 4 - (fSize % 4);
                objSize += fSize;
            }

            if (string.IsNullOrEmpty(HeapAllocatorMethod)) throw new Exception("Need heap allocator");

            // first allocate the object using whatever heap allocator we have been provided
            assembly.AddAsm($"push {objSize}");
            assembly.AddAsm("push 0");
            assembly.AddAsm($"call {HeapAllocatorMethod}");

            // eax should now contain the object pointer.  push it twice
            // (once to use for the constructor THIS, and a second to recover the address)
            assembly.AddAsm("push eax");
            assembly.AddAsm("push eax");

            _stack.Push(new ElementType(ElementType.EType.Class, parent.Token));
            _stack.Push(new ElementType(ElementType.EType.Class, parent.Token));

            // now the stack is ordered arg1, ..., argn, this, this
            // the constructor needs this, arg1, ..., argn
            // push all the arguments for the constructor again to reorder the stack
            int argSize = 0;
            if (method.Signature.ParamCount > 0)
            {
                foreach (var a in method.Signature.Params)
                {
                    argSize += _runtime.GetTypeSize(metadata, a);
                }
                if ((argSize % 4) != 0) throw new Exception("Unsupported type");
                for (int j = 0; j < argSize / 4; j++)
                {
                    assembly.AddAsm($"mov ebx, [esp+{argSize + 4}]");
                    assembly.AddAsm("push ebx");
                }
            }

            // now the object is allocated, we need to call whatever constructor was given to us
            i -= 4;
            CALL(pe, assembly, code, ref i, true, false);

            assembly.AddAsm("pop eax");
            _stack.Pop();

            // remove the args pushed by the calling method as we don't need them anymore
            // (these are the args we duplicated above to call the constructor with the correct ordering)
            for (int j = 0; j < argSize / 4; j++)
            {
                assembly.AddAsm("pop ebx");
            }
            ebxType = null;

            // eax was consumed by the call instruction, so push it again
            assembly.AddAsm("push eax");

            eaxType = new ElementType(ElementType.EType.Class, parent.Token);
            _stack.Push(eaxType);
        }

        private void NEWARR(PortableExecutableFile pe, AssembledMethod assembly, byte[] code, ref ushort i)
        {
            var metadata = pe.Metadata;

            uint typeDesc = BitConverter.ToUInt32(code, i);
            i += 4;

            ElementType arrayType;

            if ((typeDesc & 0xff000000) == 0x1b000000)
            {
                var typeSpec = pe.Metadata.TypeSpecs[(int)(typeDesc & 0x00ffffff) - 1];

                if (typeSpec.Type.Type == ElementType.EType.Var || typeSpec.Type.Type == ElementType.EType.MVar)
                {
                    if (assembly.GenericInstSig == null) throw new Exception("Got var or mvar when GenericInstSig was null");
                    arrayType = assembly.GenericInstSig.Params[0];
                }
                else arrayType = typeSpec.Type;
            }
            else
            {
                if ((typeDesc & 0xff000000) != 0x01000000 && (typeDesc & 0xff000000) != 0x02000000) throw new Exception("Unsupported type");

                arrayType = _runtime.GetType(metadata, typeDesc);
            }
            var typeSize = _runtime.GetTypeSize(metadata, arrayType);

            if (string.IsNullOrEmpty(HeapAllocatorMethod)) throw new Exception("Need heap allocator");

            // compute the total size in bytes to allocate at runtime
            assembly.AddAsm("pop eax");
            assembly.AddAsm("push eax");    // push the size to recover this later
            /*assembly.AddAsm($"mov ebx, {typeSize}");
            assembly.AddAsm("push edx");    // multiply clobbers edx
            assembly.AddAsm("mul ebx");
            assembly.AddAsm("pop edx");     // multiply clobbers edx
            assembly.AddAsm("add eax, 4");  // add 4 bytes for the array length*/
            //assembly.AddAsm($"lea eax, [8 + {typeSize} * eax]");
            LeaOrMul(assembly, 8, typeSize);
            assembly.AddAsm("push eax");    // size in bytes to allocate

            ebxType = _stack.Pop();

            // first allocate the object using whatever heap allocator we have been provided
            assembly.AddAsm("push 0");
            assembly.AddAsm($"call {HeapAllocatorMethod}");

            // put the length into the first element
            assembly.AddAsm("pop ebx");     // take the size we pushed earlier
            assembly.AddAsm("mov [eax], ebx");
            assembly.AddAsm($"mov dword [eax + 4], {typeSize}");

            assembly.AddAsm("push eax");

            eaxType = new ElementType(ElementType.EType.SzArray);
            eaxType.NestedType = arrayType;
            _stack.Push(eaxType);
        }

        public class QueuedAssembledMethod
        {
            public AssembledMethod Method { get; private set; }
            public PortableExecutableFile PEFile { get; private set; }

            public QueuedAssembledMethod(AssembledMethod method, PortableExecutableFile peFile)
            {
                Method = method;
                PEFile = peFile;
            }
        }

        public List<QueuedAssembledMethod> _methodsToCompile = new List<QueuedAssembledMethod>();

        private Dictionary<string, Assembly> _loadedAssemblies = new Dictionary<string, Assembly>();

        private bool CheckForPlugAndInvoke(string dllPath, string memberName, MethodRefSig methodSignature, AssembledMethod assembly, ICommonMethodInfo methodInfo)
        {
            assembly.HeapAllocatorMethod = HeapAllocatorMethod;
            assembly.ThrowExceptionMethod = ThrowExceptionMethod;

            if (dllPath.Contains("System.dll")) dllPath = $"{Environment.CurrentDirectory}\\Runtime.dll";

            if (File.Exists(dllPath))
            {
                if (!_loadedAssemblies.ContainsKey(dllPath)) _loadedAssemblies[dllPath] = Assembly.LoadFile(dllPath);

                var possiblePlugs = _loadedAssemblies[dllPath].GetTypes().
                    SelectMany(t => t.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)).
                    Where(m => (m.GetCustomAttribute<BaseTypes.AsmPlugAttribute>()?.AsmMethodName == memberName ||
                                m.GetCustomAttribute<BaseTypes.AsmPlugAttribute>()?.AsmMethodName == memberName.Replace(".", "_")) &&
                               m.GetCustomAttribute<BaseTypes.AsmPlugAttribute>()?.Architecture == BaseTypes.Architecture.X86).ToArray();

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
                        bool methodAlreadyCompiled = false;
                        foreach (var method in _methods)
                            if (!string.IsNullOrEmpty(method.AsmName) && method.AsmName == name)
                                methodAlreadyCompiled = true;

                        if (!methodAlreadyCompiled)
                        {
                            AssembledMethod plugMethod = new AssembledMethod(assembly.Metadata, name);
                            plugMethod.AddAsm($"{name}:");
                            possiblePlugs[0].Invoke(null, new object[] { plugMethod });
                            _methods.Add(plugMethod);
                        }
                        assembly.AddAsm($"call {name}");
                    }

                    for (int i = 0; i < methodSignature.ParamCount; i++) _stack.Pop();
                    if ((methodSignature.Flags & SigFlags.HASTHIS) != 0) _stack.Pop();
                    if (methodSignature.RetType.Type != ElementType.EType.Void)
                    {
                        _stack.Push(methodSignature.RetType);
                        eaxType = methodSignature.RetType;
                    }
                    return true;
                }
                else
                {
                    // this is for a possible C# plug
                    possiblePlugs = _loadedAssemblies[dllPath].GetTypes().
                        SelectMany(t => t.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)).
                        Where(m => m.GetCustomAttribute<BaseTypes.CSharpPlugAttribute>()?.AsmMethodName == memberName).ToArray();

                    if (possiblePlugs.Length == 1)
                    {
                        var fullDllPath = Path.GetFullPath(dllPath).TrimEnd('\\');
                        var pe = _runtime.Assemblies.Where(a => string.Compare(fullDllPath, Path.GetFullPath(a.Filename).TrimEnd('\\'), StringComparison.InvariantCultureIgnoreCase) == 0).SingleOrDefault();

                        if (pe == null)
                        {
                            pe = new PortableExecutableFile(fullDllPath);
                            AddAssembly(pe);
                        }

                        var methodDef = pe?.Metadata.MethodDefs.Where(m => m.Name == possiblePlugs[0].Name &&
                                m.Parent.FullName == possiblePlugs[0].DeclaringType.FullName &&
                                m.Signature.ParamCount == possiblePlugs[0].GetParameters().Length).SingleOrDefault() ?? null;

                        var methodToCompile = QueueCompileMethod(pe, methodDef, null, null);
                        Call(assembly, false, methodToCompile, null);

                        for (int i = 0; i < methodSignature.ParamCount; i++) _stack.Pop();
                        if ((methodSignature.Flags & SigFlags.HASTHIS) != 0) _stack.Pop();
                        if (methodSignature.RetType.Type != ElementType.EType.Void)
                        {
                            _stack.Push(methodSignature.RetType);
                            eaxType = methodSignature.RetType;
                        }
                        return true;
                    }
                    else if (dllPath.EndsWith("Runtime.dll") && methodInfo.ParentName.StartsWith("Runtime."))
                    {
                        var fullDllPath = Path.GetFullPath(dllPath).TrimEnd('\\');
                        var pe = _runtime.Assemblies.Where(a => string.Compare(fullDllPath, Path.GetFullPath(a.Filename).TrimEnd('\\'), StringComparison.InvariantCultureIgnoreCase) == 0).SingleOrDefault();

                        if (pe == null)
                        {
                            pe = new PortableExecutableFile(fullDllPath);
                            AddAssembly(pe);
                        }

                        var methodDef = pe?.Metadata.MethodDefs.Where(m => m.Name == methodInfo.Name &&
                                m.Parent.FullName == methodInfo.ParentName &&
                                m.Signature.ParamCount == methodInfo.Signature.ParamCount).SingleOrDefault() ?? null;

                        var methodToCompile = QueueCompileMethod(pe, methodDef, null, null);
                        Call(assembly, false, methodToCompile, null);

                        for (int i = 0; i < methodSignature.ParamCount; i++) _stack.Pop();
                        if ((methodSignature.Flags & SigFlags.HASTHIS) != 0) _stack.Pop();
                        if (methodSignature.RetType.Type != ElementType.EType.Void)
                        {
                            _stack.Push(methodSignature.RetType);
                            eaxType = methodSignature.RetType;
                        }
                        return true;
                    }
                }
            }

            return false;
        }

        private (uint, GenericInstSig) GetNonGenericEquivalent(CLIMetadata metadata, uint token)
        {
            if ((token & 0xff000000) == 0x2b000000)
            {
                var methodSpec = metadata.MethodSpecs[(int)(token & 0x00ffffff) - 1];

                return (methodSpec.method, new GenericInstSig(methodSpec.MemberSignature));

                // use the parent methodSpec to work out the types
                /*if (ass.MethodSpec != null)
                {
                    methodSpec = methodSpec.Clone();
                    for (int j = 0; j < methodSpec.MemberSignature.Types.Length; j++)
                    {
                        var type = methodSpec.MemberSignature.Types[j];

                        if (type.Type == ElementType.EType.MVar)
                        {
                            token = methodSpec.MemberSignature.Types[j].Token;
                            methodSpec.MemberSignature.Types[j] = metadata.MethodSpec.MemberSignature.Types[token];
                            methodSpec.MemberSignature.TypeNames[j] = metadata.MethodSpec.MemberSignature.TypeNames[token];
                        }
                    }
                }
                generic = methodSpec.MemberSignature.ToAsmString();*/
            }
            else if ((token & 0xff000000) == 0x0a000000)
            {
                var memberRef = metadata.MemberRefs[(int)(token & 0x00ffffff) - 1];

                if ((memberRef.Parent & 0xff000000) == 0x1b000000)
                {
                    var typeSpec = metadata.TypeSpecs[(int)(memberRef.Parent & 0x00ffffff) - 1];

                    for (uint j = 0; j < metadata.MethodDefs.Count; j++)
                    {
                        var methodDef = metadata.MethodDefs[(int)j];
                        if (methodDef.IsEquivalent(memberRef, typeSpec))
                        {
                            if (typeSpec.GenericSig.Params.Length > 1) throw new Exception("Currently we only support a single generic argument");
                            return ((0x06000000 | (j + 1)), typeSpec.GenericSig);
                        }
                    }
                }
            }

            return (token, null);
        }

        private void CALL(PortableExecutableFile pe, AssembledMethod assembly, byte[] code, ref ushort i, bool callvirt = false, bool ldftn = false)
        {
            var metadata = pe.Metadata;

            uint methodDesc = BitConverter.ToUInt32(code, i);
            i += 4;

            MethodSpecLayout methodSpec = null;
            var genericOverride = GetNonGenericEquivalent(metadata, methodDesc);

            if (genericOverride.Item2 != null)
            {
                methodDesc = genericOverride.Item1;

                if (assembly.GenericInstSig != null)
                {
                    if (genericOverride.Item2.Params.Length == 1 && genericOverride.Item2.Params.Length == 1 &&
                        (genericOverride.Item2.Params[0].Type == ElementType.EType.Var || genericOverride.Item2.Params[0].Type == ElementType.EType.MVar))
                    {
                        // this is a generic method being called a generic method, so we pass along the generic signature
                        genericOverride = (genericOverride.Item1, assembly.GenericInstSig);
                    }
                }
            }

            if ((methodDesc & 0xff000000) == 0x0a000000)
            {
                var memberRef = metadata.MemberRefs[(int)(methodDesc & 0x00ffffff) - 1];
                var memberName = memberRef.ToAsmString();

                // eax and ebx may have been clobbered
                eaxType = null;
                ebxType = null;

                assembly.AddAsm($"; start {memberName} plug");
                if (ldftn) throw new Exception("Plugs are unsupported with ldftn");

                var file = memberName.Substring(0, memberName.IndexOf('.'));
                var path = $"{Environment.CurrentDirectory}\\{file}.dll";

                if (!CheckForPlugAndInvoke(path, memberName, memberRef.Signature, assembly, memberRef))
                {
                    /*if (memberName.StartsWith("System.Runtime.InteropServices.Marshal.SizeOf<"))
                    {
                        var type = methodSpec.MemberSignature.Types[0];
                        if (type.Type == ElementType.EType.Var || type.Type == ElementType.EType.MVar)
                            type = assembly.GenericInstSig.Params[0];
                        if (type.Type == ElementType.EType.Class)
                        {
                            type = new ElementType(ElementType.EType.ValueType, type.Token);
                        }
                        int size = _runtime.GetTypeSize(metadata, type);
                        assembly.AddAsm($"push {size}");
                    }
                    else
                    {
                        throw new Exception("Unable to handle this method");
                    }*/
                    if (memberName == "System.Runtime.InteropServices.Marshal.SizeOf_I4")
                    {
                        if (genericOverride.Item2 == null) throw new Exception("Unsupported operation");
                        var type = genericOverride.Item2.Params[0];
                        if (type.Type == ElementType.EType.Var || type.Type == ElementType.EType.MVar)
                            type = assembly.GenericInstSig.Params[0];
                        if (type.Type == ElementType.EType.Class)
                        {
                            type = new ElementType(ElementType.EType.ValueType, type.Token);
                        }
                        int size = _runtime.GetTypeSize(metadata, type);
                        assembly.AddAsm($"push {size}");
                    }
                    else if (memberName == "System.IDisposable.Dispose_Void")
                    {
                        // this is a callvirt on the object on the stack, usually happens via a "using" block
                        var _this = _stack.Peek();

                        if ((_this.Token & 0xff000000) == 0x02000000)
                        {
                            var tdef = metadata.TypeDefs[(int)(_this.Token & 0x00ffffff) - 1];
                            var method = tdef.Methods.Where(m => m.ToAsmString().EndsWith("Dispose_Void")).SingleOrDefault();

                            if (method != null)
                            {
                                var methodToCompile = QueueCompileMethod(pe, method, genericOverride.Item2, methodSpec);

                                Call(assembly, ldftn, methodToCompile, genericOverride.Item2);
                            }
                        }
                    }
                    else
                    {
                        throw new Exception("Unable to handle this method");
                    }

                    for (int j = 0; j < memberRef.Signature.ParamCount; j++)
                        _stack.Pop();
                    if ((memberRef.Signature.Flags & SigFlags.HASTHIS) != 0) _stack.Pop();
                    if (memberRef.Signature.RetType.Type != ElementType.EType.Void)
                        _stack.Push(memberRef.Signature.RetType);
                }
                assembly.AddAsm("; end plug");
            }
            else if ((methodDesc & 0xff000000) == 0x06000000)
            {
                var methodDef = metadata.MethodDefs[(int)(methodDesc & 0x00ffffff) - 1];

                // if we have a generic override then clone this memberref and replace values of mvar or var with the known parameter
                if (genericOverride.Item2 != null)
                {
                    methodDef = methodDef.Clone(metadata);
                    methodDef.Signature.Override(genericOverride.Item2);
                }

                var memberName = methodDef.ToAsmString(genericOverride.Item2);

                if (memberName.Contains("Kernel_Memory_Utilities") || memberName.Contains("Runtime_Memory_Utilities"))
                {
                    memberName = metadata.MethodDefs[(int)(methodDesc & 0x00ffffff) - 1].ToAsmString();
                }

                if (!CheckForPlugAndInvoke(pe.Filename, memberName, methodDef.Signature, assembly, methodDef))
                {
                    var methodToCompile = QueueCompileMethod(pe, methodDef, genericOverride.Item2, methodSpec);

                    Call(assembly, ldftn, methodToCompile, genericOverride.Item2);

                    if (!ldftn)
                    {
                        for (int j = 0; j < methodDef.Signature.ParamCount; j++)
                            _stack.Pop();

                        if (methodDef.Signature.Flags.HasFlag(SigFlags.HASTHIS))
                        {
                            _stack.Pop();   // pop the object reference from the stack
                        }

                        if (methodDef.Signature.RetType.Type != ElementType.EType.Void)
                            _stack.Push(methodDef.Signature.RetType);
                    }
                }

                // eax and ebx may have been clobbered
                eaxType = null;
                ebxType = null;
            }
            else throw new Exception("Unhandled CALL target");
        }

        private void Call(AssembledMethod assembly, bool ldftn, List<AssembledMethod> methodToCompile, GenericInstSig genericSig)
        {
            if (methodToCompile.Count == 1)
            {
                string callsite = methodToCompile[0].ToAsmString(genericSig);
                var methodDef = methodToCompile[0].Method.MethodDef;

                if (ldftn)
                {
                    assembly.AddAsm($"push {callsite}");
                    _stack.Push(new ElementType(ElementType.EType.MethodSignature));
                }
                else
                {
                    assembly.AddAsm($"call {callsite}");

                    if (methodDef.Signature.RetType != null && methodDef.Signature.RetType.Type != ElementType.EType.Void)
                    {
                        // TODO:  This is incorrect as eax can only store 32bits of the return type - we really need to store this on the stack
                        var sizeOfRetType = _runtime.GetTypeSize(assembly.Metadata, methodDef.Signature.RetType);
                        for (int b = 0; b < Math.Ceiling(sizeOfRetType / 4f); b++)
                            assembly.AddAsm("push eax");
                    }
                }
            }
            else
            {
                throw new Exception("Incomplete interface support");
            }
        }

        private List<AssembledMethod> QueueCompileMethod(PELoader.PortableExecutableFile pe, MethodDefLayout methodDef, GenericInstSig genericSig, MethodSpecLayout methodSpec = null)
        {
            var assembledMethods = new List<AssembledMethod>();

            if (methodDef.RVA == 0)
            {
                // this is likely an interface method, which has no code contents
                // we need to find all objects the implement this interface and queue their methods
                // because we do not know which one will be attached at runtime
                var parent = methodDef.Parent;

                // the quick hack is to only load types that implement the interface in this PE
                var types = pe.Metadata.TypeDefs.Where(t => t.Implements.Contains(parent.Token)).ToArray();
                var endOfName = methodDef.Name.Substring(methodDef.Name.LastIndexOf('.') + 1);

                List<MethodDefLayout> methods = new List<MethodDefLayout>();
                foreach (var t in types)
                {
                    var method = t.Methods.Where(m => m.Name.EndsWith(endOfName) && m.Signature.IsEquivalent(methodDef.Signature)).FirstOrDefault();
                    if (method != null) methods.Add(method);
                    else throw new Exception("Method was not implemented as expected");
                }

                foreach (var method in methods)
                {
                    assembledMethods.AddRange(QueueCompileMethod(pe, method, genericSig, methodSpec));
                }

                return assembledMethods;
            }

            var memberName = methodDef.ToAsmString(genericSig);
            bool methodAlreadyCompiled = false;
            foreach (var method in _methods)
                if (method.Method != null && method.Method.MethodDef.ToAsmString(method.GenericInstSig) == memberName)
                    methodAlreadyCompiled = true;

            var methodHeaderToCompile = new MethodHeader(pe.Memory, pe.Metadata, methodDef);
            var methodToCompile = new AssembledMethod(pe.Metadata, methodHeaderToCompile, methodSpec, genericSig);
            assembledMethods.Add(methodToCompile);

            if (!methodAlreadyCompiled)
            {
                bool methodWaitingToCompile = false;

                foreach (var method in _methodsToCompile)
                    if (method.Method.Method.MethodDef.ToAsmString(method.Method.GenericInstSig) == memberName && method.Method.MethodSpec == methodSpec)
                        methodWaitingToCompile = true;

                if (!methodWaitingToCompile)
                    _methodsToCompile.Add(new QueuedAssembledMethod(methodToCompile, pe));
            }

            return assembledMethods;
        }

        public List<string> WriteAssembly(uint offset = 0xA000, uint size = 512, uint stack = 0x9000, bool userApp = false)
        {
            List<string> output = new List<string>();

            output.Add("[bits 32]");
            output.Add($"[org 0x{offset.ToString("X")}]");   // for bootsector code only
            output.Add("_start:");
            if (!userApp)
            {
                output.Add("    mov ax, 16");
                output.Add("    mov ds, ax");
                output.Add("    mov ss, ax");
                output.Add("    mov es, ax");
                output.Add("    mov fs, ax");
                output.Add("    mov gs, ax");
                output.Add($"    mov ebp, 0x{stack.ToString("X")} ; reset the stack");
                output.Add("    mov esp, ebp");
                output.Add("    finit");
            }
            else
            {
                output.Add($"    mov ebp, 0x{stack.ToString("X")} ; reset the stack");
                output.Add("    mov esp, ebp");
            }
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
                        var s = (string)data.Value.Data;
                        output.Add($"{data.Key}:");
                        StringBuilder sb = new StringBuilder();
                        if (s.Length > 0) sb.Append($"{(int)s[0]}");
                        for (int i = 1; i < s.Length; i++) sb.Append($", {(int)s[i]}");

                        output.Add($"    dd {s.Length}, 2");        // length of array and stride of 2 bytes
                        if (s.Length > 0) output.Add($"    dw {sb.ToString()} ; {s}"); // no null termination required since we know the length of the string
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
                    else if (data.Value.Type.Type == ElementType.EType.Char)
                    {
                        output.Add($"{data.Key}:");
                        output.Add($"    dw {(ushort)(char)data.Value.Data}");
                    }
                    else if (data.Value.Type.Type == ElementType.EType.U1 || data.Value.Type.Type == ElementType.EType.Boolean)
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
                    else if (data.Value.Type.Type == ElementType.EType.SzArray || data.Value.Type.Type == ElementType.EType.Class)
                    {
                        if (data.Value.Data is string)
                        {
                            output.Add($"{data.Key}:");
                            output.Add($"    dd {data.Value.Data}");
                        }
                        else
                        {
                            output.Add($"{data.Key}:");
                            output.Add($"    dd {(uint)data.Value.Data}");
                        }
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
