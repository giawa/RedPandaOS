using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using IL2Asm.BaseTypes;

namespace CPUHelper
{
    public static class Bios
    {
        [AsmMethod]
        public static ushort GetGeometry(byte disk)
        {
            return 0;
        }

        [AsmPlug("CPUHelper.Bios.GetGeometry_U2_U1", IL2Asm.BaseTypes.Architecture.X86_Real)]
        public static void GetGeometryAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("mov ah, 8");
            assembly.AddAsm("mov bx, dx");  // store dx to recover later
            assembly.AddAsm("pop dx");      // store the disk in dl
            assembly.AddAsm("push cx");     // store cx to recover later
            assembly.AddAsm("int 0x13");
            assembly.AddAsm("mov ah, dh");
            assembly.AddAsm("mov al, cl");
            assembly.AddAsm("pop cx");      // recover cx
            assembly.AddAsm("mov dx, bx");  // recover dx
            assembly.AddAsm("push ax");
        }

        [AsmMethod]
        public static void WriteByte(byte c)
        {
            Console.Write((char)c);
        }

        [AsmPlug("CPUHelper.Bios.WriteByte_Void_U1", IL2Asm.BaseTypes.Architecture.X86_Real)]
        public static void WriteByteAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop ax");
            assembly.AddAsm("mov ah, 0x0e");
            assembly.AddAsm("int 0x10");
        }

        [AsmMethod]
        public static void WriteChar(char c)
        {
            Console.Write(c);
        }

        [AsmPlug("CPUHelper.Bios.WriteChar_Void_Char", IL2Asm.BaseTypes.Architecture.X86_Real)]
        public static void WriteCharAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop ax");
            assembly.AddAsm("mov ah, 0x0e");
            assembly.AddAsm("int 0x10");
        }

        [AsmMethod]
        public static void EnterProtectedMode(ref CPU.GDT gdt)
        {

        }

        [AsmPlug("CPUHelper.Bios.EnterProtectedMode_Void_ByRefValueType", IL2Asm.BaseTypes.Architecture.X86_Real)]
        public static void EnterProtectedModeAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop bx");
            assembly.AddAsm("mov [gdt_ptr + 2], bx");
            assembly.AddAsm("cli");
            assembly.AddAsm("lgdt [gdt_ptr]");
            assembly.AddAsm("mov eax, cr0");
            assembly.AddAsm("or eax, 0x1");
            assembly.AddAsm("mov cr0, eax");
            assembly.AddAsm("jmp 08h:0xA000");  // our 32 bit code starts at 0xA000, freshly loaded from the disk
            assembly.AddAsm("");
            assembly.AddAsm("gdt_ptr:");
            assembly.AddAsm("dw 23");
            assembly.AddAsm("dd 0; this gets filled in with bx, which is the address of the gdt object");
        }

        [AsmMethod]
        public static ushort LoadDisk(byte cylinder, byte head, byte sector, ushort highAddr, ushort lowAddr, byte drive, byte sectors)
        {
            return 0;
        }

        [AsmPlug("CPUHelper.Bios.LoadDisk_U2_U1_U1_U1_U2_U2_U1_U1", IL2Asm.BaseTypes.Architecture.X86_Real, AsmFlags.None)]
        public static void LoadDiskAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("; Bios.LoadDisk_U2_U2_U2_U1_U1 plug");
            assembly.AddAsm("LoadDisk_U2_U2_U2_U1_U1:");
            assembly.AddAsm("push bp");
            assembly.AddAsm("mov bp, sp");
            assembly.AddAsm("push cx");
            assembly.AddAsm("push dx");
            assembly.AddAsm("push es");

            // bp + 4 is sectors to read
            // bp + 6 is drive
            // bp + 8 is lowAddr
            // bp + 10 is hiAddr
            // bp + 12 is sector
            // bp + 14 is head
            // bp + 16 is cylinder
            assembly.AddAsm("mov es, [bp + 10]");
            assembly.AddAsm("mov bx, [bp + 8]");

            assembly.AddAsm("mov ah, 0x02");
            assembly.AddAsm("mov al, [bp + 4]");

            assembly.AddAsm("mov cl, [bp + 12]"); // starting at sector 2, sector 1 is our boot sector and already in memory
            assembly.AddAsm("mov ch, [bp + 16]"); // first 8 bits of cylinder
            assembly.AddAsm("mov dh, [bp + 14]"); // head
            assembly.AddAsm("mov dl, [bp + 6]");  // drive number from bios

            assembly.AddAsm("int 0x13");
            //assembly.AddAsm("mov al, dl");

            assembly.AddAsm("jc LoadDisk_U2_U2_U2_U1_U1_Cleanup");
            assembly.AddAsm("mov ah, 0"); // al will now contain the number of sectors read

            assembly.AddAsm("LoadDisk_U2_U2_U2_U1_U1_Cleanup:");
            assembly.AddAsm("pop es");
            assembly.AddAsm("pop dx");
            assembly.AddAsm("pop cx");
            assembly.AddAsm("pop bp");
            assembly.AddAsm("ret 14");
        }

        [AsmMethod]
        public static void ResetDisk()
        {

        }

        [AsmPlug("CPUHelper.Bios.ResetDisk_Void", IL2Asm.BaseTypes.Architecture.X86_Real)]
        public static void ResetDisk(IAssembledMethod assembly)
        {
            assembly.AddAsm("mov ah, 0x00; reset disk drive");
            assembly.AddAsm("int 0x13");
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SMAP_ret
        {
            public ushort sig1;
            public ushort sig2;
            public ushort contId1;
            public ushort contId2;
        }

        [AsmMethod]
        public static ushort DetectMemory(ushort address, ref SMAP_ret value)
        {
            return 0;
        }

        [AsmPlug("CPUHelper.Bios.DetectMemory_U2_U2_ByRefValueType", IL2Asm.BaseTypes.Architecture.X86_Real, AsmFlags.None)]
        public static void DetectMemoryAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("; Bios.DetectMemory_U2_U2_ByRef plug");
            assembly.AddAsm("DetectMemory_U2_U2_ByRef:");
            assembly.AddAsm("push bp");
            assembly.AddAsm("mov bp, sp");
            assembly.AddAsm("push cx");
            assembly.AddAsm("push dx");

            // bp + 4 is SMAP_ret
            // bp + 6 is address

            assembly.AddAsm("mov di, [bp + 6]");
            assembly.AddAsm("mov bx, [bp + 4]");
            assembly.AddAsm("mov ebx, [bx + 4]");
            assembly.AddAsm("mov edx, 0x534D4150");
            assembly.AddAsm("mov ecx, 24");
            assembly.AddAsm("mov eax, 0xE820");
            assembly.AddAsm("int 0x15");

            assembly.AddAsm("jc DetectMemory_U2_U2_ByRef_Error");
            assembly.AddAsm("push ebx"); // this is the continuation
            assembly.AddAsm("mov bx, [bp + 4]");
            assembly.AddAsm("mov [bx], eax");    // al will now contain magic number

            // now grab the continuation
            assembly.AddAsm("mov ax, bx");
            assembly.AddAsm("add ax, 4");
            assembly.AddAsm("mov bx, ax");
            assembly.AddAsm("pop eax");
            assembly.AddAsm("mov [bx], eax");    // now we have the continuation as well

            assembly.AddAsm("DetectMemory_U2_U2_ByRef_Cleanup:");
            assembly.AddAsm("pop dx");
            assembly.AddAsm("pop cx");
            assembly.AddAsm("pop bp");
            assembly.AddAsm("ret 4");

            assembly.AddAsm("DetectMemory_U2_U2_ByRef_Error:");
            assembly.AddAsm("mov ax, 0xff");
            assembly.AddAsm("jmp DetectMemory_U2_U2_ByRef_Cleanup");
        }

        [AsmMethod]
        public static bool EnableA20()
        {
            return true;
        }

        [AsmPlug("CPUHelper.Bios.EnableA20_Boolean", IL2Asm.BaseTypes.Architecture.X86_Real)]
        public static void EnableA20Asm(IAssembledMethod assembly)
        {
            // from https://wiki.osdev.org/A20_Line
            assembly.AddAsm("mov ax, 0x2403");
            assembly.AddAsm("int 0x15");
            assembly.AddAsm("jb bios_a20_failed");
            assembly.AddAsm("cmp ah, 0");
            assembly.AddAsm("jnz bios_a20_failed");

            assembly.AddAsm("mov ax, 0x2402");
            assembly.AddAsm("int 0x15");
            assembly.AddAsm("jb bios_a20_failed");
            assembly.AddAsm("cmp ah, 0");
            assembly.AddAsm("jnz bios_a20_failed");

            assembly.AddAsm("cmp al, 1");
            assembly.AddAsm("jz bios_a20_activated");

            assembly.AddAsm("mov ax, 0x2401");
            assembly.AddAsm("int 0x15");
            assembly.AddAsm("jb bios_a20_failed");
            assembly.AddAsm("cmp ah, 0");
            assembly.AddAsm("jnz bios_a20_failed");

            assembly.AddAsm("bios_a20_activated:");
            assembly.AddAsm("push 1");
            assembly.AddAsm("jmp bios_a20_complete");

            assembly.AddAsm("bios_a20_failed:");
            assembly.AddAsm("push 0");

            assembly.AddAsm("bios_a20_complete:");
        }
    }
}
