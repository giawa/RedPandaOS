﻿using PELoader;
using System;
using System.Diagnostics;
using System.IO;
using IL2Asm;
using System.Collections.Generic;
using BuildTools;

namespace GiawaOS
{
    class Program
    {
        private static void CompileAndBundleApp(string path, string name, uint baseAddress = 0x400000U)
        {
            PortableExecutableFile portableExecutable = new PortableExecutableFile(path);

            var entryPoint = FindEntryPoint(portableExecutable, "Program", "Main");

            // create sample application
            var assembler = new IL2Asm.Assembler.x86.Ver2.Assembler();
            assembler.AddAssembly(portableExecutable);

            var methodHeader = new MethodHeader(portableExecutable.Memory, portableExecutable.Metadata, entryPoint);
            assembler.Assemble(portableExecutable, new AssembledMethod(portableExecutable.Metadata, methodHeader, null));

            while (assembler._methodsToCompile.Count > 0)
            {
                var m = assembler._methodsToCompile[0];
                assembler._methodsToCompile.RemoveAt(0);
                assembler.Assemble(m.PEFile, m.Method);
            }

            var program = assembler.WriteAssembly(baseAddress, 512, baseAddress + 4096, true);

            //IL2Asm.Optimizer.RemoveUnneededLabels.ProcessAssembly(program);
            IL2Asm.Optimizer.x86.MoveLocalsToRegisters.ProcessAssembly(program);
            RunOptimizers(program);

            File.WriteAllLines($"{name}.asm", program.ToArray());
            RunNASM($"{name}.asm", $"{name}.bin");

            PEWriter writer = new PEWriter(File.ReadAllBytes($"{name}.bin"), (int)baseAddress);
            if (!Directory.Exists("../../../disk/apps")) Directory.CreateDirectory("../../../disk/apps");
            writer.Write($"../../../disk/apps/{name}.exe");
        }

        private static void RunOptimizers(List<string> assembly)
        {
            int count = assembly.Count;

            IL2Asm.Optimizer.RemoveUnneededLabels.ProcessAssembly(assembly);
            IL2Asm.Optimizer.MergePushPop.ProcessAssembly(assembly);
            IL2Asm.Optimizer.MergePushPopAcrossMov.ProcessAssembly(assembly);
            IL2Asm.Optimizer.x86.SimplifyConstants.ProcessAssembly(assembly);
            IL2Asm.Optimizer.x86.RemoveRedundantMoves.ProcessAssembly(assembly);
            IL2Asm.Optimizer.RemoveDuplicateInstructions.ProcessAssembly(assembly);

            if (assembly.Count != count) RunOptimizers(assembly);   // maybe not done yet?
        }

        public static void Main()
        {
            PortableExecutableFile file = new PortableExecutableFile(@"RedPandaOS.dll");
            PortableExecutableFile bootloaders = new PortableExecutableFile(@"..\..\..\..\Bootloader\bin\Debug\net6.0\Bootloader.dll");

            Stopwatch stopwatch = Stopwatch.StartNew();

            // find the Program.Main entry point
            var bootloader1 = FindEntryPoint(bootloaders, "Stage1", "Start");
            var fatbootloader1 = FindEntryPoint(bootloaders, "FATStage1", "Start");
            var bootloader2 = FindEntryPoint(bootloaders, "Stage2", "Start");
            var methodDef32 = FindEntryPoint(file, "Init", "Start");
            var isrHandler = FindEntryPoint(file, "PIC", "IsrHandler");
            var irqHandler = FindEntryPoint(file, "PIC", "IrqHandler");
            var malloc = FindEntryPoint(file, "KernelHeap", "Malloc");
            var throwHandler = FindEntryPoint(file, "Exceptions", "Throw");

            PortableExecutableFile unamePE = new PortableExecutableFile(@"..\..\..\..\apps\uname\bin\Debug\net6.0\uname.dll");

            CompileAndBundleApp(@"..\..\..\..\apps\uname\bin\Debug\net6.0\uname.dll", "uname");
            CompileAndBundleApp(@"..\..\..\..\apps\compositor\bin\Debug\net6.0\compositor.dll", "composit");

            if (bootloader1 != null && methodDef32 != null)
            {
                //MethodHeader method = new MethodHeader(file.Memory, methodDef);

                var assembler16 = new IL2Asm.Assembler.x86_RealMode.Assembler();
                assembler16.AddAssembly(bootloaders);
                assembler16.Assemble(bootloaders, bootloader1);

                var stage1 = assembler16.WriteAssembly(0x7C00, 0);
                IL2Asm.Optimizer.RemoveUnneededLabels.ProcessAssembly(stage1);
                IL2Asm.Optimizer.MergePushPop.ProcessAssembly(stage1);
                IL2Asm.Optimizer.MergePushPopAcrossMov.ProcessAssembly(stage1);
                IL2Asm.Optimizer.x86_RealMode.SimplifyConstants.ProcessAssembly(stage1);
                IL2Asm.Optimizer.x86_RealMode.RemoveRedundantMoves.ProcessAssembly(stage1);
                IL2Asm.Optimizer.x86_RealMode.ReplaceEquivalentInstructions.ProcessAssembly(stage1);
                IL2Asm.Optimizer.RemoveDuplicateInstructions.ProcessAssembly(stage1);
                IL2Asm.Optimizer.x86_RealMode.MergePushPopAcrossMoves.ProcessAssembly(stage1);
                File.WriteAllLines("stage1.asm", stage1.ToArray());

                assembler16 = new IL2Asm.Assembler.x86_RealMode.Assembler();
                assembler16.AddAssembly(bootloaders);
                assembler16.Assemble(bootloaders, fatbootloader1);

                var fatstage1 = assembler16.WriteAssembly(0x7C60, 0);
                IL2Asm.Optimizer.RemoveUnneededLabels.ProcessAssembly(fatstage1);
                IL2Asm.Optimizer.MergePushPop.ProcessAssembly(fatstage1);
                IL2Asm.Optimizer.MergePushPopAcrossMov.ProcessAssembly(fatstage1);
                IL2Asm.Optimizer.x86_RealMode.SimplifyConstants.ProcessAssembly(fatstage1);
                IL2Asm.Optimizer.x86_RealMode.RemoveRedundantMoves.ProcessAssembly(fatstage1);
                IL2Asm.Optimizer.x86_RealMode.ReplaceEquivalentInstructions.ProcessAssembly(fatstage1);
                IL2Asm.Optimizer.RemoveDuplicateInstructions.ProcessAssembly(fatstage1);
                IL2Asm.Optimizer.x86_RealMode.MergePushPopAcrossMoves.ProcessAssembly(fatstage1);
                File.WriteAllLines("fatstage1.asm", fatstage1.ToArray());


                assembler16 = new IL2Asm.Assembler.x86_RealMode.Assembler();
                assembler16.AddAssembly(bootloaders);
                assembler16.Assemble(bootloaders, bootloader2);

                var stage2 = assembler16.WriteAssembly(0x9000 + 512, 0);
                IL2Asm.Optimizer.RemoveUnneededLabels.ProcessAssembly(stage2);
                IL2Asm.Optimizer.MergePushPop.ProcessAssembly(stage2);
                IL2Asm.Optimizer.MergePushPopAcrossMov.ProcessAssembly(stage2);
                IL2Asm.Optimizer.x86_RealMode.SimplifyConstants.ProcessAssembly(stage2);
                IL2Asm.Optimizer.x86_RealMode.RemoveRedundantMoves.ProcessAssembly(stage2);
                IL2Asm.Optimizer.x86_RealMode.ReplaceEquivalentInstructions.ProcessAssembly(stage2);
                IL2Asm.Optimizer.RemoveDuplicateInstructions.ProcessAssembly(stage2);
                IL2Asm.Optimizer.x86_RealMode.SimplifyConstants.ProcessAssembly(stage2);
                IL2Asm.Optimizer.x86_RealMode.MergePushPopAcrossMoves.ProcessAssembly(stage2);
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

                var methodsToCompile = assembler32._methodsToCompile;
                while (methodsToCompile.Count > 0)
                {
                    var m = methodsToCompile[0];
                    methodsToCompile.RemoveAt(0);
                    assembler32.Assemble(m.PEFile, m.Method);
                }

                var pm = assembler32.WriteAssembly(0xA000, 90112);

                IL2Asm.Optimizer.RemoveUnneededLabels.ProcessAssembly(pm);
                IL2Asm.Optimizer.MergePushPop.ProcessAssembly(pm);
                IL2Asm.Optimizer.MergePushPopAcrossMov.ProcessAssembly(pm);
                IL2Asm.Optimizer.x86.SimplifyConstants.ProcessAssembly(pm);
                IL2Asm.Optimizer.x86.RemoveRedundantMoves.ProcessAssembly(pm);
                IL2Asm.Optimizer.RemoveDuplicateInstructions.ProcessAssembly(pm);

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

                stopwatch.Stop();
                Console.WriteLine($"IL2Asm took {stopwatch.ElapsedMilliseconds} ms");
                stopwatch.Restart();

                // process the assembly
                Console.WriteLine();
                Console.WriteLine("* Assembling OS!");
                RunNASM("stage1.asm", "stage1.bin");
                RunNASM("fatstage1.asm", "fatstage1.bin");
                RunNASM("stage2.asm", "stage2.bin");
                RunNASM("pm.asm", "pm.bin");

                stopwatch.Stop();
                Console.WriteLine($"NASM took {stopwatch.ElapsedMilliseconds} ms");
                stopwatch.Restart();

                GenerateSymbols("stage2.asm", "stage2.elf");
                File.Copy("symbols.txt", "stage2_symbols.txt", true);
                File.Copy("symbols.bin", "stage2_symbols.bin", true);
                CreateMethodSizeBreakdown("stage2_symbols.txt", File.ReadAllBytes("stage2.bin").Length, "stage2_breakdown.txt");
                GenerateSymbols("pm.asm", "pm.elf");

                stopwatch.Stop();
                Console.WriteLine($"GenerateSymbols {stopwatch.ElapsedMilliseconds} ms");
                stopwatch.Restart();

                // copy kernel and symbols to the boot directory
                if (!Directory.Exists("../../../disk/boot")) Directory.CreateDirectory("../../../disk/boot");
                File.Copy("pm.bin", "../../../disk/boot/kernel.bin", true);
                File.Copy("symbols.bin", "../../../disk/boot/symbols.bin", true);

                DiskMaker.DiskInfo disk = new DiskMaker.DiskInfo();
                DiskMaker.PartitionInfo bootablePartition = new DiskMaker.PartitionInfo()
                {
                    FirstSectorLBA = 2,
                    NumberOfSectors = 32 * 1024 / 512 * 1024 - 2,   // 32MiB minus the FirstSectorLBA
                    //NumberOfSectors = 2878,   // 1.44MiB floppy disk
                    Bootable = true,
                    PartitionType = 0x0B    // FAT32 CHS/LBA
                };
                disk.Partitions.Add(bootablePartition);
                DiskMaker.MakeBootableDisk(disk, "disk.bin", "stage1.bin", "stage2.bin");
                //CopyToVDI("disk.bin", "disk.vdi");
                File.Copy("disk.bin", "disk.hdd", true);

                stopwatch.Stop();
                Console.WriteLine($"Building disk image took {stopwatch.ElapsedMilliseconds} ms");

                // then boot qemu
                Console.WriteLine();
                Console.WriteLine("* Booting OS!");
                //var qemu = Process.Start("qemu-system-x86_64", "-drive format=raw,file=boot.bin -usb -rtc clock=host -smp cores=2,sockets=1,threads=1 -device usb-audio,audiodev=alsa -audiodev alsa,id=alsa -device ich9-intel-hda -device hda-duplex,audiodev=alsa");
                //var qemu = Process.Start("qemu-system-x86_64", "-m 48M -device piix4-ide,id=ide -drive id=disk,file=disk.bin,format=raw,if=none -device ide-hd,drive=disk,bus=ide.0 -usb -rtc clock=host -smp cores=2,sockets=1,threads=1 -device usb-audio,audiodev=alsa -audiodev alsa,id=alsa -device ich9-intel-hda -device hda-duplex,audiodev=alsa -serial stdio");
                //var qemu = Process.Start("qemu-system-x86_64", "-drive format=raw,file=boot.img,if=floppy -usb -rtc clock=host -smp cores=2,sockets=1,threads=1 -device usb-audio,audiodev=alsa -audiodev alsa,id=alsa -device ich9-intel-hda -device hda-duplex,audiodev=alsa");
                var qemu = Process.Start("qemu-system-x86_64", "-m 48M -drive id=disk,file=disk.bin,format=raw,if=none -device ide-hd,drive=disk,bus=ide.0 -usb -rtc clock=host -smp cores=2,sockets=1,threads=1 -serial stdio");
                qemu.WaitForExit();

                /*var pmBytes = File.ReadAllBytes("pm.bin");
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
                        stream.Write(new byte[512 - (stream.Length % 512)]);

                        byte[] temp = new byte[512];
                        while (stream.Length < 4096 + 512)
                        {
                            for (int i = 0; i < 512; i++) temp[i] = (byte)(stream.Length / 512);
                            stream.Write(temp);
                        }

                        stream.Write(File.ReadAllBytes("pm.bin"));

                        Console.WriteLine($"Stage 1 is using {(440 - stage1Zeros) / 390.0 * 100}% of available space.");
                        Console.WriteLine($"Kernel is using {pmBytes.Length / 94720.0 * 100}% of available space.");

                        stream.Write(new byte[512 - (pmBytes.Length % 512)]);
                        
                        while (stream.Length < 94720)
                        {
                            for (int i = 0; i < 512; i++) temp[i] = (byte)(stream.Length / 512);
                            stream.Write(temp);
                        }

                        stream.Write(File.ReadAllBytes("symbols.bin"));
                        stream.Write(new byte[512 - (stream.Length % 512)]);

                        // create a high density floppy disk for now
                        while (stream.Length < 1.44 * 1024 * 1024)
                        {
                            Array.Clear(temp, 0, temp.Length);
                            stream.Write(temp);
                        }
                    }

                    File.Copy("boot.bin", "boot.img", true);

                    stopwatch.Stop();
                    Console.WriteLine($"Building disk image took {stopwatch.ElapsedMilliseconds} ms");

                    // then boot qemu
                    Console.WriteLine();
                    Console.WriteLine("* Booting OS!");
                    //var qemu = Process.Start("qemu-system-x86_64", "-drive format=raw,file=boot.bin -usb -rtc clock=host -smp cores=2,sockets=1,threads=1 -device usb-audio,audiodev=alsa -audiodev alsa,id=alsa -device ich9-intel-hda -device hda-duplex,audiodev=alsa");
                    var qemu = Process.Start("qemu-system-x86_64", "-device piix3-ide,id=ide -drive id=disk,file=boot.bin,format=raw,if=none -device ide-hd,drive=disk,bus=ide.0 -usb -rtc clock=host -smp cores=2,sockets=1,threads=1 -device usb-audio,audiodev=alsa -audiodev alsa,id=alsa -device ich9-intel-hda -device hda-duplex,audiodev=alsa");
                    //var qemu = Process.Start("qemu-system-x86_64", "-drive format=raw,file=boot.img,if=floppy -usb -rtc clock=host -smp cores=2,sockets=1,threads=1 -device usb-audio,audiodev=alsa -audiodev alsa,id=alsa -device ich9-intel-hda -device hda-duplex,audiodev=alsa");
                    qemu.WaitForExit();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }*/
            }

            Console.WriteLine("Press key to exit...");
            Console.ReadKey();
        }

        private static void CopyToVDI(string inputFile, string outputFile)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("qemu-img", $"convert -O vdi {inputFile} {outputFile}");
            startInfo.RedirectStandardOutput = true;
            startInfo.UseShellExecute = false;

            Process qemuImg = new Process();
            qemuImg.StartInfo = startInfo;
            qemuImg.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data)) Console.WriteLine(args.Data);
            };

            qemuImg.Start();
            qemuImg.BeginOutputReadLine();
            qemuImg.WaitForExit();
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

        private static void CreateMethodSizeBreakdown(string symbolsFile, int asmFileSize, string outputFile)
        {
            // process these symbols so they can be drawn in excel or similar
            var lines = File.ReadAllLines(symbolsFile);
            Dictionary<string, int> moduleSizes = new Dictionary<string, int>();
            for (int i = 0; i < lines.Length - 1; i++)
            {
                var split = lines[i].Split(' ');
                var nsplit = lines[i + 1].Split(' ');

                if (split[1].StartsWith("DB_"))
                {
                    moduleSizes.Add("Constants", asmFileSize - int.Parse(split[0], System.Globalization.NumberStyles.HexNumber));
                    break;
                }

                moduleSizes.Add(split[1], int.Parse(nsplit[0], System.Globalization.NumberStyles.HexNumber) - int.Parse(split[0], System.Globalization.NumberStyles.HexNumber));
            }
            using (StreamWriter breakdown = new StreamWriter(outputFile))
            {
                foreach (var module in moduleSizes)
                    breakdown.WriteLine($"{module.Key}\t{module.Value}");
            }
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
                            if (symbol.Name.StartsWith("IL_")) continue;
                            if (symbol.Name.StartsWith("BLOB_")) break;
                            if (symbol.Name.StartsWith("SKIP_PUSH_")) continue;

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

                                output.WriteLine($"{(entry.Value + 0xA000).ToString("X")} {entry.Name}");
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

        private static void RunLinker(string inputFile, string outputFile, string offset)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("wsl", $"ld -m elf_i386 -Ttext={offset} --oformat binary -o {outputFile} {inputFile}");
            startInfo.RedirectStandardOutput = true;
            startInfo.UseShellExecute = false;

            Process ld = new Process();
            ld.StartInfo = startInfo;
            ld.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data)) Console.WriteLine(args.Data);
            };

            ld.Start();
            ld.BeginOutputReadLine();
            ld.WaitForExit();
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
