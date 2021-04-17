using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace PELoader
{
    class Program
    {
        static void Main(string[] args)
        {
            PortableExecutableFile file = new PortableExecutableFile(@"..\..\..\..\TestIL\bin\Debug\netcoreapp3.1\TestIL.dll");

            Console.WriteLine(file.ToString());
            Console.ReadKey();
        }
    }
}
