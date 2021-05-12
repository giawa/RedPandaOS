using PELoader;
using System;
using System.Diagnostics;
using System.IO;
using IL2Asm;

namespace GiawaOS
{
    class Program
    {
        public static void Main()
        {
            PortableExecutableFile file = new PortableExecutableFile(@"GiawaOS.dll");

            // find the Program.Main entry point
            var bootloader1 = FindEntryPoint(file, "Stage1", "Start");
            var bootloader2 = FindEntryPoint(file, "Stage2", "Start");
            var methodDef32 = FindEntryPoint(file, "Init", "Start");
            var isrHandler = FindEntryPoint(file, "Interrupts", "IsrHandler");

            if (bootloader1 != null && methodDef32 != null)
            {
                //MethodHeader method = new MethodHeader(file.Memory, methodDef);

                var assembler16 = new IL2Asm.Assembler.x86_RealMode.Assembler();
                assembler16.AddAssembly(file);
                assembler16.Assemble(file, bootloader1);

                var stage1 = assembler16.WriteAssembly(0x7C00, 512, true);
                IL2Asm.Optimizer.RemoveUnneededLabels.ProcessAssembly(stage1);
                IL2Asm.Optimizer.MergePushPop.ProcessAssembly(stage1);
                IL2Asm.Optimizer.MergePushPopAcrossMov.ProcessAssembly(stage1);
                IL2Asm.Optimizer.SimplifyMoves.ProcessAssembly(stage1);
                File.WriteAllLines("stage1.asm", stage1.ToArray());

                assembler16 = new IL2Asm.Assembler.x86_RealMode.Assembler();
                assembler16.AddAssembly(file);
                assembler16.Assemble(file, bootloader2);

                var stage2 = assembler16.WriteAssembly(0x9000, 4096, false);
                File.WriteAllLines("stage2.asm", stage2.ToArray());

                var assembler32 = new IL2Asm.Assembler.x86.Assembler();
                assembler32.AddAssembly(file);
                var methodHeader32 = new MethodHeader(file.Memory, file.Metadata, methodDef32);
                assembler32.Assemble(file, new AssembledMethod(file.Metadata, methodHeader32, null));
                var isrHeader = new MethodHeader(file.Memory, file.Metadata, isrHandler);
                assembler32.Assemble(file, new AssembledMethod(file.Metadata, isrHeader, null));

                var pm = assembler32.WriteAssembly(0xA000, 90112);
                IL2Asm.Optimizer.RemoveUnneededLabels.ProcessAssembly(pm);
                IL2Asm.Optimizer.MergePushPop.ProcessAssembly(pm);
                IL2Asm.Optimizer.MergePushPopAcrossMov.ProcessAssembly(pm);
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

                var pmBytes = File.ReadAllBytes("pm.bin");

                // combine the assemblies by tacking the protected mode code on to the boot loader
                try
                {
                    File.Copy("stage1.bin", "boot.bin", true);
                    using (var stream = new FileStream("boot.bin", FileMode.Append))
                    {
                        stream.Write(File.ReadAllBytes("stage2.bin"));
                        stream.Write(File.ReadAllBytes("pm.bin"));

                        Console.WriteLine($"Kernel is using {pmBytes.Length / 94720.0 * 100}% of available space.");

                        stream.Write(new byte[512 - (pmBytes.Length % 512)]);

                        while (stream.Length < 94720)
                        {
                            byte[] temp = new byte[512];
                            for (int i = 0; i < 512; i++) temp[i] = (byte)(stream.Length / 512);
                            stream.Write(temp);
                        }
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
            nasm.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data)) Console.WriteLine(args.Data);
            };

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
