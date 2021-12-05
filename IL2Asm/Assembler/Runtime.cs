using PELoader;
using System;
using System.Collections.Generic;
using System.IO;

namespace IL2Asm
{
    public class Runtime
    {
        public static int GlobalMethodCounter = 0;

        private List<PortableExecutableFile> _assemblies = new List<PortableExecutableFile>();

        public List<PortableExecutableFile> Assemblies { get { return _assemblies; } }

        public void AddAssembly(PortableExecutableFile pe)
        {
            foreach (var assembly in _assemblies)
                if (assembly.Name == pe.Name)
                    throw new Exception("Tried to add assembly more than once");

            _assemblies.Add(pe);
        }

        public PortableExecutableFile GetParentAssembly(CLIMetadata metadata, uint typeRefToken)
        {
            while ((typeRefToken & 0xff000000) == 0x01000000)
            {
                var typeRef = metadata.TypeRefs[(int)(typeRefToken & 0x00ffffff) - 1];
                typeRefToken = typeRef.ResolutionScope;
            }

            if ((typeRefToken & 0xff000000) == 0x23000000)
            {
                var assemblyRef = metadata.AssemblyRefs[(int)(typeRefToken & 0x00ffffff) - 1];

                var path = _assemblies[0].Filename.Substring(0, _assemblies[0].Filename.LastIndexOf("/"));
                //path += $"/{assemblyRef.Name}";

                PortableExecutableFile pe = null;

                foreach (var a in _assemblies) 
                    if (a.Filename == path + $"/{assemblyRef.Name}.dll" || 
                        a.Filename == path + $"/publish/{assemblyRef.Name}.dll" ||
                        a.Filename == path + $"/{assemblyRef.Name}.exe") pe = a;

                if (pe == null)
                {
                    if (File.Exists(path + $"/{assemblyRef.Name}.dll")) pe = new PortableExecutableFile(path + $"/{assemblyRef.Name}.dll");
                    else if (File.Exists(path + $"/publish/{assemblyRef.Name}.dll")) pe = new PortableExecutableFile(path + $"/publish/{assemblyRef.Name}.dll");
                    else if (File.Exists(path + $"/{assemblyRef.Name}.exe")) pe = new PortableExecutableFile(path + $"/{assemblyRef.Name}.exe");
                    else throw new FileNotFoundException(path + $"/{assemblyRef.Name}");

                    AddAssembly(pe);
                }

                return pe;
            }
            else throw new Exception("Unable to find assembly used by typeRef");
        }

        public ElementType GetFieldType(CLIMetadata metadata, uint fieldToken)
        {
            if ((fieldToken & 0xff000000) == 0x04000000)
            {
                var field = metadata.Fields[(int)(fieldToken & 0x00ffffff) - 1];

                foreach (var f in field.Parent.Fields)
                {
                    if (f == field) return f.Type;
                }

                throw new Exception("Fields did not include requested fieldToken");
            }
            else if ((fieldToken & 0xff000000) == 0x0A000000)
            {
                var field = metadata.MemberRefs[(int)(fieldToken & 0x00ffffff) - 1];
                var type = field.MemberSignature.RetType;

                uint typeRefToken = type.Token;
                if (typeRefToken == 0) typeRefToken = field.Parent;

                var pe = GetParentAssembly(metadata, typeRefToken);

                for (int i = 0; i < pe.Metadata.Fields.Count; i++)
                {
                    var f = pe.Metadata.Fields[i];
                    if (f.Name == field.Name)
                    {
                        return f.Type;
                    }
                }

                throw new Exception("Could not find type");
            }
            else
            {
                throw new Exception("Unsupported metadata table");
            }
        }

        public int GetFieldOffset(CLIMetadata metadata, uint fieldToken)
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

                uint typeRefToken = type.Token;
                if (typeRefToken == 0) typeRefToken = field.Parent;

                var pe = GetParentAssembly(metadata, typeRefToken);

                for (int i = 0; i < pe.Metadata.Fields.Count; i++)
                {
                    var f = pe.Metadata.Fields[i];
                    if (f.Name == field.Name)
                    {
                        var offset = GetFieldOffset(pe.Metadata, 0x04000000 | (uint)(i + 1));
                        return offset;
                    }
                }

                throw new Exception("Could not find offset");
            }
            else
            {
                throw new Exception("Unsupported metadata table");
            }
        }

        private Dictionary<string, int> _typeSizes = new Dictionary<string, int>();

        public int GetTypeSize(CLIMetadata metadata, ElementType type)
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
                    var pe = GetParentAssembly(metadata, token);

                    foreach (var typeDef in pe.Metadata.TypeDefs)
                    {
                        if (typeDef.Name == typeRef.Name && typeDef.Namespace == typeRef.Namespace)
                        {
                            if (_typeSizes.ContainsKey(typeDef.FullName)) return _typeSizes[typeDef.FullName];

                            foreach (var field in typeDef.Fields)
                            {
                                size += GetTypeSize(pe.Metadata, field.Type);
                            }

                            _typeSizes.Add(typeDef.FullName, size);
                            return size;
                        }
                    }

                    if (pe.Metadata.ExportedTypes != null)
                    {
                        foreach (var exportedType in pe.Metadata.ExportedTypes)
                        {
                            if (exportedType.TypeName == typeRef.Name && exportedType.TypeNamespace == typeRef.Namespace)
                            {
                                var nestedPe = GetParentAssembly(pe.Metadata, exportedType.implementation);

                                foreach (var typeDef in nestedPe.Metadata.TypeDefs)
                                {
                                    if (typeDef.Name == typeRef.Name && typeDef.Namespace == typeRef.Namespace)
                                    {
                                        if (_typeSizes.ContainsKey(typeDef.FullName)) return _typeSizes[typeDef.FullName];

                                        foreach (var field in typeDef.Fields)
                                        {
                                            size += GetTypeSize(nestedPe.Metadata, field.Type);
                                        }

                                        _typeSizes.Add(typeDef.FullName, size);
                                        return size;
                                    }
                                }
                            }
                        }
                    }

                    throw new Exception("Could not find size");
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
                    case ElementType.EType.R4:
                    case ElementType.EType.U4:
                    case ElementType.EType.I4: return 4;
                    case ElementType.EType.R8:
                    case ElementType.EType.U8:
                    case ElementType.EType.I8: return 8;
                    case ElementType.EType.Void: return 0;
                    default: throw new Exception("Unknown size");
                }
            }
        }
    }
}
