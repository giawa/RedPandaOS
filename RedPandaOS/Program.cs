using PELoader;
using System;
using System.Diagnostics;
using System.IO;
using IL2Asm;
using System.Collections.Generic;

namespace GiawaOS
{
    class Program
    {
        public static void Main()
        {
            PortableExecutableFile file = new PortableExecutableFile(@"RedPandaOS.dll");

            // find the Program.Main entry point
            var bootloader1 = FindEntryPoint(file, "Stage1", "Start");
            var bootloader2 = FindEntryPoint(file, "Stage2", "Start");
            var methodDef32 = FindEntryPoint(file, "Init", "Start");
            var isrHandler = FindEntryPoint(file, "PIC", "IsrHandler");
            var irqHandler = FindEntryPoint(file, "PIC", "IrqHandler");
            var malloc = FindEntryPoint(file, "KernelHeap", "Malloc");
            var throwHandler = FindEntryPoint(file, "Exceptions", "Throw");

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
                IL2Asm.Optimizer.x86_RealMode.SimplifyConstants.ProcessAssembly(stage1);
                IL2Asm.Optimizer.RemoveDuplicateInstructions.ProcessAssembly(stage1);
                File.WriteAllLines("stage1.asm", stage1.ToArray());

                assembler16 = new IL2Asm.Assembler.x86_RealMode.Assembler();
                assembler16.AddAssembly(file);
                assembler16.Assemble(file, bootloader2);

                var stage2 = assembler16.WriteAssembly(0x9000, 4096, false);
                File.WriteAllLines("stage2.asm", stage2.ToArray());

                var assembler32 = new IL2Asm.Assembler.x86.Ver2.Assembler();
                assembler32.AddAssembly(file);

                var mallocHeader = new MethodHeader(file.Memory, file.Metadata, malloc);
                var mallocMethod = new AssembledMethod(file.Metadata, mallocHeader, null);
                assembler32.HeapAllocatorMethod = mallocMethod.ToAsmString().Replace(".", "_");

                var throwHeader = new MethodHeader(file.Memory, file.Metadata, throwHandler);
                var throwMethod = new AssembledMethod(file.Metadata, throwHeader, null);
                throwMethod.HasStackFrame = true;
                assembler32.ThrowExceptionMethod = throwMethod.ToAsmString().Replace(".", "_");

                var methodHeader32 = new MethodHeader(file.Memory, file.Metadata, methodDef32);
                assembler32.Assemble(file, new AssembledMethod(file.Metadata, methodHeader32, null));
                var isrHeader = new MethodHeader(file.Memory, file.Metadata, isrHandler);
                assembler32.Assemble(file, new AssembledMethod(file.Metadata, isrHeader, null));
                var irqHeader = new MethodHeader(file.Memory, file.Metadata, irqHandler);
                assembler32.Assemble(file, new AssembledMethod(file.Metadata, irqHeader, null));

                assembler32.Assemble(file, mallocMethod);
                assembler32.Assemble(file, throwMethod);

                var pm = assembler32.WriteAssembly(0xA000, 90112);
                //IL2Asm.Optimizer.RemoveUnneededLabels.ProcessAssembly(pm);
                //IL2Asm.Optimizer.UseIncOrDec.ProcessAssembly(pm);
                //IL2Asm.Optimizer.MergePushPop.ProcessAssembly(pm);
                //IL2Asm.Optimizer.MergePushPopAcrossMov.ProcessAssembly(pm);
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
                GenerateSymbols("pm.asm", "pm.elf");

                var pmBytes = File.ReadAllBytes("pm.bin");
                var stage1Bytes = File.ReadAllBytes("stage1.bin");
                int stage1Zeros = 0;
                for (int i = 439; i >= 0; i--)
                {
                    if (stage1Bytes[i] != 0) break;
                    stage1Zeros++;
                }

                // combine the assemblies by tacking the protected mode code on to the boot loader
                try
                {
                    File.Copy("stage1.bin", "boot.bin", true);
                    using (var stream = new FileStream("boot.bin", FileMode.Append))
                    {
                        stream.Write(File.ReadAllBytes("stage2.bin"));
                        stream.Write(File.ReadAllBytes("pm.bin"));

                        Console.WriteLine($"Stage 1 is using {(440 - stage1Zeros) / 440.0 * 100}% of available space.");
                        Console.WriteLine($"Kernel is using {pmBytes.Length / 94720.0 * 100}% of available space.");

                        stream.Write(new byte[512 - (pmBytes.Length % 512)]);

                        byte[] temp = new byte[512];
                        while (stream.Length < 94720)
                        {
                            for (int i = 0; i < 512; i++) temp[i] = (byte)(stream.Length / 512);
                            stream.Write(temp);
                        }

                        stream.Write(File.ReadAllBytes("symbols.bin"));
                        stream.Write(new byte[512 - (stream.Length % 512)]);

                        // create a 8MiB hard disk for now
                        while (stream.Length < 8 * 1024 * 1024)
                        {
                            Array.Clear(temp, 0, temp.Length);
                            stream.Write(temp);
                        }
                    }

                    // then boot qemu
                    Console.WriteLine();
                    Console.WriteLine("* Booting OS!");
                    //var qemu = Process.Start("qemu-system-x86_64", "-drive format=raw,file=boot.bin -usb -rtc clock=host -smp cores=2,sockets=1,threads=1 -device usb-audio,audiodev=alsa -audiodev alsa,id=alsa -device ich9-intel-hda -device hda-duplex,audiodev=alsa");
                    var qemu = Process.Start("qemu-system-x86_64", "-device piix3-ide,id=ide -drive id=disk,file=boot.bin,format=raw,if=none -device ide-hd,drive=disk,bus=ide.0 -usb -rtc clock=host -smp cores=2,sockets=1,threads=1 -device usb-audio,audiodev=alsa -audiodev alsa,id=alsa -device ich9-intel-hda -device hda-duplex,audiodev=alsa");
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

        private static void GenerateSymbols(string inputFile, string outputFile)
        {
            var temp = File.ReadAllLines(inputFile);
            List<string> lines = new List<string>();
            foreach (var line in temp)
            {
                if (!line.Contains("[org")) lines.Add(line);
            }
            File.WriteAllLines(inputFile + ".tmp", lines.ToArray());

            ProcessStartInfo startInfo = new ProcessStartInfo("nasm.exe", $"-fbin {inputFile}.tmp -o {outputFile} -f elf -F dwarf -g");
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

            //RunLinker(outputFile, "test.bin", "0xA000");
            int filePos = 0;

            using (StreamWriter output = new StreamWriter("symbols.txt"))
            using (BinaryWriter binary = new BinaryWriter(File.Open("symbols.bin", FileMode.Create)))
            {
                var elf = ELFSharp.ELF.ELFReader.Load(outputFile);
                foreach (var header in elf.Sections)
                {
                    if (header.Name.Contains("symtab"))
                    {
                        foreach (var symbol in (header as ELFSharp.ELF.Sections.ISymbolTable).Entries)
                        {
                            if (string.IsNullOrEmpty(symbol.Name)) continue;
                            if (symbol.Name.StartsWith("IL_") || symbol.Name.StartsWith("BLOB_")) continue;

                            if (symbol is ELFSharp.ELF.Sections.SymbolEntry<uint> entry)
                            {
                                // make sure each symbol completely falls in a sector
                                int length = 4 + entry.Name.Length + 1;
                                int sectorPosition = filePos % 512;
                                if (sectorPosition + length > 512)
                                {
                                    byte[] fill = new byte[512 - sectorPosition];
                                    Array.Fill(fill, (byte)0xff);
                                    binary.Write(fill);
                                    filePos += fill.Length;
                                }

                                output.WriteLine($"{entry.Value.ToString("X")} {entry.Name}");
                                binary.Write(entry.Value + 0xA000);
                                for (int i = 0; i < entry.Name.Length; i++)
                                    binary.Write((byte)entry.Name[i]);
                                binary.Write((byte)0);
                                filePos += length;
                            }
                        }
                    }
                }
            }
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
