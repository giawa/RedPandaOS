using PELoader;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace IL2Asm
{
    public class Program
    {
        public static void Main()
        {
            PortableExecutableFile file = new PortableExecutableFile(@"..\..\..\..\TestIL\bin\Debug\netcoreapp3.1\TestIL.dll");

            // find the Program.Main entry point
            var methodDef = FindEntryPoint(file, "Program", "Main");

            if (methodDef != null)
            {
                //MethodHeader method = new MethodHeader(file.Memory, methodDef);

                Assembler.x86_RealMode.Assembler assembler = new Assembler.x86_RealMode.Assembler();
                assembler.AddAssembly(file);
                assembler.Assemble(file, methodDef);

                assembler.WriteAssembly("bios.asm");

                // process the assembly
                Console.WriteLine();
                Console.WriteLine("* Assembling OS!");
                ProcessStartInfo startInfo = new ProcessStartInfo("nasm.exe", "-fbin bios.asm -o boot.bin");
                startInfo.RedirectStandardOutput = true;
                startInfo.UseShellExecute = false;

                Process nasm = new Process();
                nasm.StartInfo = startInfo;
                nasm.OutputDataReceived += (sender, args) => Console.WriteLine("{0}", args);

                nasm.Start();
                nasm.BeginOutputReadLine();
                nasm.WaitForExit();

                // then boot qemu
                Console.WriteLine();
                Console.WriteLine("* Booting OS!");
                var qemu = Process.Start("qemu-system-x86_64", "boot.bin");
                qemu.WaitForExit();
            }

            Console.WriteLine("Press key to exit...");
            Console.ReadKey();
        }

        private static MethodDefLayout FindEntryPoint(PortableExecutableFile file, string typeName, string methodName)
        {
            foreach (var typeDef in file.Metadata.TypeDefs)
            {
                if (typeDef.Name == "Program")
                {
                    foreach (var methodDef in typeDef.Methods)
                    {
                        if (methodDef.Name == "Main")
                            return methodDef;
                    }
                }
            }

            return null;
        }
    }
}
