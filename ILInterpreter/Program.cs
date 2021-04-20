using PELoader;
using System;
using System.IO;
using System.Diagnostics;

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

                var interpreter = new Gen1.Interpreter();

                Stopwatch watch = Stopwatch.StartNew();
                for (int i = 0; i < 3; i++)
                {
                    //if (i == 5) watch = Stopwatch.StartNew();
                    interpreter.LoadMethod(file.Metadata, method);
                    interpreter.Execute();
                }
                watch.Stop();

                Console.WriteLine($"Interpreted method returned: {interpreter.ReturnValue}");
                Console.WriteLine($"Took {watch.ElapsedMilliseconds}ms to run");
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
