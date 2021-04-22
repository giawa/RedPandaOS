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
            var methodDef = FindEntryPoint(file, "Program", "Main");

            if (methodDef != null)
            {
                MethodHeader method = new MethodHeader(file.Memory, methodDef);

                var interpreter = new Gen3.Interpreter();

                Stopwatch watch = Stopwatch.StartNew();
                for (int i = 0; i < 1; i++)
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

        private static MethodDefLayout FindEntryPoint(PortableExecutableFile file, string typeName, string methodName)
        {
            foreach (var typeDef in file.Metadata.TypeDefs)
            {
                if (typeDef.GetName(file.Metadata) == "Program")
                {
                    foreach (var methodDef in typeDef.Methods)
                    {
                        if (methodDef.GetName(file.Metadata) == "Main")
                            return methodDef;
                    }
                }
            }

            return null;
        }
    }
}
