using PELoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IL2Asm.Assembler.x86_RealMode
{
    public class Assembler : IAssembler
    {
        private Dictionary<string, AssembledMethod> _staticConstructors = new Dictionary<string, AssembledMethod>();
        private List<AssembledMethod> _methods = new List<AssembledMethod>();
        private Dictionary<string, object> _initializedData = new Dictionary<string, object>();

        public const int BytesPerRegister = 2;

        private sbyte _sbyte;
        private byte _byte;
        private string _jmpLabel;
        private uint _uint;

        public List<PortableExecutableFile> _assemblies = new List<PortableExecutableFile>();

        public void AddAssembly(PortableExecutableFile pe)
        {
            foreach (var assembly in _assemblies)
                if (assembly.Name == pe.Name)
                    throw new Exception("Tried to add assembly more than once");

            _assemblies.Add(pe);
        }

        public void Assemble(PortableExecutableFile pe, MethodDefLayout methodDef)
        {
            if (!_assemblies.Contains(pe)) throw new Exception("The portable executable must be added via AddAssembly prior to called Assemble");

            var method = new MethodHeader(pe.Memory, pe.Metadata, methodDef);
            var assembly = new AssembledMethod(pe.Metadata, method);

            var code = method.Code;

            if (_methods.Count > 0)
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
                        if (value > ushort.MaxValue || value < short.MinValue)
                            throw new Exception("Out of range for 16 bit mode");
                        i += 4;
                        assembly.AddAsm($"push {value}");
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

            int offset = GetFieldOffset(metadata, fieldToken);

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
            
            _initializedData.Add(label, s);
            assembly.AddAsm($"push {label}");
        }

        private void LDFLD(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint fieldToken = BitConverter.ToUInt32(code, i);
            i += 4;

            int offset = GetFieldOffset(metadata, fieldToken);

            assembly.AddAsm("pop bx");
            if (offset == 0) assembly.AddAsm("mov ax, [bx]");
            else assembly.AddAsm($"mov ax, [bx + {offset}]");
            assembly.AddAsm("push ax");
        }

        private void LDFLDA(AssembledMethod assembly, CLIMetadata metadata, byte[] code, ref ushort i)
        {
            uint fieldToken = BitConverter.ToUInt32(code, i);
            i += 4;

            int offset = GetFieldOffset(metadata, fieldToken);

            assembly.AddAsm("pop bx");
            if (offset == 0) assembly.AddAsm("mov ax, bx");
            else
            {
                assembly.AddAsm("mov ax, bx");
                assembly.AddAsm($"add ax, {offset}");
            }
            assembly.AddAsm("push ax");
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
                case ElementType.EType.U1: _initializedData.Add(label, (byte)0); break;
                case ElementType.EType.I1: _initializedData.Add(label, (sbyte)0); break;
                case ElementType.EType.U2: _initializedData.Add(label, (ushort)0); break;
                case ElementType.EType.I2: _initializedData.Add(label, (short)0); break;
                case ElementType.EType.ValueType: _initializedData.Add(label, new byte[GetTypeSize(metadata, type)]); break;
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

        private int GetFieldOffset(CLIMetadata metadata, uint fieldToken)
        {
            if ((fieldToken & 0xff000000) == 0x04000000)
            {
                var field = metadata.Fields[(int)(fieldToken & 0x00ffffff) - 1];
                int offset = 0;

                foreach (var f in field.Parent.Fields)
                {
                    if (f == field) return offset;
                    offset += GetTypeSize(metadata, f.Type);
                }

                throw new Exception("Fields did not include requested fieldToken");
            }
            else if ((fieldToken & 0xff000000) == 0x0A000000)
            {
                var field = metadata.MemberRefs[(int)(fieldToken & 0x00ffffff) - 1];
                var type = field.MemberSignature.RetType;

                if (type.Token == 0)
                {
                    // simple built-in type
                    var parent = field.GetParentToken(metadata);

                    while ((parent & 0xff000000) == 0x01000000)
                    {
                        var typeRef = metadata.TypeRefs[(int)(parent & 0x00ffffff) - 1];
                        parent = typeRef.ResolutionScope;
                    }

                    if ((parent & 0xff000000) == 0x23000000)
                    {
                        var assemblyRef = metadata.AssemblyRefs[(int)(parent & 0x00ffffff) - 1];

                        var path = _assemblies[0].Filename.Substring(0, _assemblies[0].Filename.LastIndexOf("/"));
                        path += $"/{assemblyRef.Name}";

                        PortableExecutableFile pe = null;

                        foreach (var a in _assemblies) if (a.Filename == path + ".dll" || a.Filename == path + ".exe") pe = a;

                        if (pe == null)
                        {
                            if (File.Exists(path + ".dll")) pe = new PortableExecutableFile(path + ".dll");
                            else if (File.Exists(path + ".exe")) pe = new PortableExecutableFile(path + ".exe");
                            else new FileNotFoundException(path);

                            AddAssembly(pe);
                        }

                        for (int i = 0; i < pe.Metadata.Fields.Count; i++)
                        {
                            var f = pe.Metadata.Fields[i];
                            if (f.Name == field.Name)
                            {
                                var offset = GetFieldOffset(pe.Metadata, 0x04000000 | (uint)(i + 1));
                                return offset;
                            }
                        }
                    }
                    else throw new Exception("Unable to find assembly used by typeRef");

                    throw new Exception("MemberRefs did not include requested fieldToken");
                }
                else if ((type.Token & 0xff000000) == 0x01000000)
                {
                    var typeRef = metadata.TypeRefs[(int)(type.Token & 0x00ffffff) - 1];

                    while ((typeRef.ResolutionScope & 0xff000000) == 0x01000000)
                        typeRef = metadata.TypeRefs[(int)(typeRef.ResolutionScope & 0x00ffffff) - 1];

                    if ((typeRef.ResolutionScope & 0xff000000) == 0x23000000)
                    {
                        var assemblyRef = metadata.AssemblyRefs[(int)(typeRef.ResolutionScope & 0x00ffffff) - 1];

                        var path = _assemblies[0].Filename.Substring(0, _assemblies[0].Filename.LastIndexOf("/"));
                        path += $"/{assemblyRef.Name}";

                        PortableExecutableFile pe = null;

                        foreach (var a in _assemblies) if (a.Filename == path + ".dll" || a.Filename == path + ".exe") pe = a;

                        if (pe == null)
                        {
                            if (File.Exists(path + ".dll")) pe = new PortableExecutableFile(path + ".dll");
                            else if (File.Exists(path + ".exe")) pe = new PortableExecutableFile(path + ".exe");
                            else new FileNotFoundException(path);

                            AddAssembly(pe);
                        }

                        for (int i = 0; i < pe.Metadata.Fields.Count; i++)
                        {
                            var f = pe.Metadata.Fields[i];
                            if (f.Name == field.Name)
                            {
                                var offset = GetFieldOffset(pe.Metadata, 0x04000000 | (uint)(i + 1));
                                return offset;
                            }
                        }
                    }
                    else throw new Exception("Unable to find assembly used by typeRef");

                    throw new Exception("MemberRefs did not include requested fieldToken");
                }
                else throw new Exception("Valuetype did not reference the typeref table");
            }
            else
            {
                throw new Exception("Unsupported metadata table");
            }
        }

        private static Dictionary<string, int> _typeSizes = new Dictionary<string, int>();

        private int GetTypeSize(CLIMetadata metadata, ElementType type)
        {
            if (type.Type == ElementType.EType.ValueType || type.Type == ElementType.EType.Class)
            {
                var token = type.Token;
                int size = 0;

                if ((token & 0xff000000) == 0x02000000)
                {
                    var typeDef = metadata.TypeDefs[(int)(token & 0x00ffffff) - 1];
                    if (_typeSizes.ContainsKey(typeDef.FullName)) return _typeSizes[typeDef.FullName];

                    foreach (var field in typeDef.Fields)
                    {
                        size += GetTypeSize(metadata, field.Type);
                    }

                    _typeSizes.Add(typeDef.FullName, size);
                    return size;
                }
                else if ((token & 0xff000000) == 0x01000000)
                {
                    var typeRef = metadata.TypeRefs[(int)(token & 0x00ffffff) - 1];

                    return 0;
                }
                else
                {
                    throw new Exception("Unsupported table");
                }
            }
            else
            {
                switch (type.Type)
                {
                    case ElementType.EType.Boolean:
                    case ElementType.EType.U1:
                    case ElementType.EType.I1: return 1;
                    case ElementType.EType.Char:
                    case ElementType.EType.U2:
                    case ElementType.EType.I2: return 2;
                    case ElementType.EType.U4:
                    case ElementType.EType.I4: return 4;
                    case ElementType.EType.U8:
                    case ElementType.EType.I8: return 8;
                    case ElementType.EType.Void: return 0;
                    default: throw new Exception("Unknown size");
                }
            }
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

                if (memberName == "CPUHelper.Bios.WriteByte")
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
                stream.WriteLine("[bits 16]");    // for bootsector code only
                stream.WriteLine("[org 0x7c00]");    // for bootsector code only
                stream.WriteLine("");

                if (_staticConstructors.Count > 0)
                {
                    stream.WriteLine("; Call static constructors");
                    foreach (var cctor in _staticConstructors)
                    {
                        if (cctor.Value == null) continue;
                        string callsite = cctor.Key.Replace(".", "_");
                        stream.WriteLine($"    call {callsite}__cctor");
                    }
                }

                foreach (var method in _methods)
                {
                    stream.WriteLine($"; Exporting assembly for method {method.Method.MethodDef}");
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

                // should only do this for boot sector attribute code
                stream.WriteLine("times 510-($-$$) db 0");
                stream.WriteLine("dw 0xaa55");
            }
        }
    }
}
