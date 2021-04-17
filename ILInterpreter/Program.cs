using PELoader;
using System;

namespace ILInterpreter
{
    class Program
    {
        static void Main(string[] args)
        {
            PortableExecutableFile file = new PortableExecutableFile(@"..\..\..\..\TestIL\bin\Debug\netcoreapp3.1\TestIL.dll");

            uint rvaOffset = 0x2050;
            MethodHeader method = new MethodHeader(file.Memory, ref rvaOffset);

            Interpreter interpreter = new Interpreter();
            interpreter.LoadMethod(file.Metadata, method);
            interpreter.Execute();

            Console.WriteLine($"Interpreted method returned: {interpreter.ReturnValue}");
            Console.ReadKey();
        }
    }
}
