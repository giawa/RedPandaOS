using PELoader;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace IL2Asm
{
    public class Program
    {
        public static void Main()
        {
            PortableExecutableFile file = new PortableExecutableFile(@"..\..\..\..\TestIL\bin\Debug\netcoreapp3.1\TestIL.dll");

            // find the Program.Main entry point
            var methodDef16 = FindEntryPoint(file, "Program", "Main");
            var methodDef32 = FindEntryPoint(file, "Program", "Main32");

            if (methodDef16 != null && methodDef32 != null)
            {
                //MethodHeader method = new MethodHeader(file.Memory, methodDef);

                Assembler.x86_RealMode.Assembler assembler16 = new Assembler.x86_RealMode.Assembler();
                assembler16.AddAssembly(file);
                assembler16.Assemble(file, methodDef16);

                assembler16.WriteAssembly("bios.asm");

                Assembler.x86.Assembler assembler32 = new Assembler.x86.Assembler();
                assembler32.AddAssembly(file);
                assembler32.Assemble(file, methodDef32);

                assembler32.WriteAssembly("pm.asm");

                /*using (StreamWriter stream = new StreamWriter("bios.asm", true))
                {
                    stream.WriteLine();
                    stream.Write(File.ReadAllText("pm.asm"));
                }*/

                // process the assembly
                Console.WriteLine();
                Console.WriteLine("* Assembling OS!");
                RunNASM("bios.asm", "bios.bin");
                RunNASM("pm.asm", "pm.bin");

                // combine the assemblies by tacking the protected mode code on to the boot loader
                try
                {
                    File.Copy("bios.bin", "boot.bin", true);
                    using (var stream = new FileStream("boot.bin", FileMode.Append))
                        stream.Write(File.ReadAllBytes("pm.bin"));

                    // then boot qemu
                    Console.WriteLine();
                    Console.WriteLine("* Booting OS!");
                    var qemu = Process.Start("qemu-system-x86_64", "-drive format=raw,file=boot.bin");
                    qemu.WaitForExit();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            Console.WriteLine("Press key to exit...");
            Console.ReadKey();
        }

        private static void RunNASM(string inputFile, string outputFile)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("nasm.exe", $"-fbin {inputFile} -o {outputFile}");
            startInfo.RedirectStandardOutput = true;
            startInfo.UseShellExecute = false;

            Process nasm = new Process();
            nasm.StartInfo = startInfo;
            nasm.OutputDataReceived += (sender, args) => Console.WriteLine("{0}", args);

            nasm.Start();
            nasm.BeginOutputReadLine();
            nasm.WaitForExit();
        }

        private static MethodDefLayout FindEntryPoint(PortableExecutableFile file, string typeName, string methodName)
        {
            foreach (var typeDef in file.Metadata.TypeDefs)
            {
                if (typeDef.Name == typeName)
                {
                    foreach (var methodDef in typeDef.Methods)
                    {
                        if (methodDef.Name == methodName)
                            return methodDef;
                    }
                }
            }

            return null;
        }
    }
}
