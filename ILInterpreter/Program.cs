using PELoader;
using System;

namespace ILInterpreter
{
    class Program
    {
        static void Main(string[] args)
        {
            PortableExecutableFile file = new PortableExecutableFile(@"..\..\..\..\TestIL\bin\Debug\netcoreapp3.1\TestIL.dll");

            // find the Program.Main entry point
            uint rvaOffset = FindEntryPoint(file, "Program", "Main");

            if (rvaOffset != 0)
            {
                MethodHeader method = new MethodHeader(file.Memory, ref rvaOffset);

                Interpreter interpreter = new Interpreter();
                interpreter.LoadMethod(file.Metadata, method);
                interpreter.Execute();

                Console.WriteLine($"Interpreted method returned: {interpreter.ReturnValue}");
            }
            
            Console.ReadKey();
        }

        private static uint FindEntryPoint(PortableExecutableFile file, string typeName, string methodName)
        {
            foreach (var typeDef in file.Metadata.TypeDefs)
            {
                if (typeDef.GetName(file.Metadata) == "Program")
                {
                    foreach (var methodDef in typeDef.Methods)
                    {
                        if (methodDef.GetName(file.Metadata) == "Main")
                            return methodDef.rva;
                    }
                }
            }

            return 0;
        }
    }
}
