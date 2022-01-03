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
            var bootloader1 = FindEntryPoint(file, "Program", "BootloaderStage1");
            var bootloader2 = FindEntryPoint(file, "Program", "BootloaderStage2");
            var methodDef32 = FindEntryPoint(file, "Program", "Main32");

            if (bootloader1 != null && methodDef32 != null)
            {
                //MethodHeader method = new MethodHeader(file.Memory, methodDef);

                Assembler.x86_RealMode.Assembler assembler16 = new Assembler.x86_RealMode.Assembler();
                assembler16.AddAssembly(file);
                assembler16.Assemble(file, bootloader1);

                var stage1 = assembler16.WriteAssembly(0x7C00, 512, true);
                Optimizer.RemoveUnneededLabels.ProcessAssembly(stage1);
                Optimizer.MergePushPop.ProcessAssembly(stage1);
                Optimizer.MergePushPopAcrossMov.ProcessAssembly(stage1);
                Optimizer.x86_RealMode.SimplifyConstants.ProcessAssembly(stage1);
                File.WriteAllLines("stage1.asm", stage1.ToArray());

                assembler16 = new Assembler.x86_RealMode.Assembler();
                assembler16.AddAssembly(file);
                assembler16.Assemble(file, bootloader2);

                var stage2 = assembler16.WriteAssembly(0x9000, 4096, false);
                File.WriteAllLines("stage2.asm", stage2.ToArray());

                Assembler.x86.Ver1.Assembler assembler32 = new Assembler.x86.Ver1.Assembler();
                assembler32.AddAssembly(file);
                var methodHeader32 = new MethodHeader(file.Memory, file.Metadata, methodDef32);
                assembler32.Assemble(file, new AssembledMethod(file.Metadata, methodHeader32, null));//methodDef32, null);

                var pm = assembler32.WriteAssembly(0xA000, 8192);
                File.WriteAllLines("pm.asm", pm.ToArray());

                /*using (StreamWriter stream = new StreamWriter("bios.asm", true))
                {
                    stream.WriteLine();
                    stream.Write(File.ReadAllText("pm.asm"));
                }*/

                // process the assembly
                Console.WriteLine();
                Console.WriteLine("* Assembling OS!");
                RunNASM("stage1.asm", "stage1.bin");
                RunNASM("stage2.asm", "stage2.bin");
                RunNASM("pm.asm", "pm.bin");

                // combine the assemblies by tacking the protected mode code on to the boot loader
                try
                {
                    File.Copy("stage1.bin", "boot.bin", true);
                    using (var stream = new FileStream("boot.bin", FileMode.Append))
                    {
                        stream.Write(File.ReadAllBytes("stage2.bin"));
                        stream.Write(File.ReadAllBytes("pm.bin"));
                    }

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
