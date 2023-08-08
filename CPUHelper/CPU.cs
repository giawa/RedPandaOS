using IL2Asm.BaseTypes;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace CPUHelper
{
    public static class CPU
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct GDTSegment
        {
            public ushort segmentLength;
            public ushort segmentBase1;
            public byte segmentBase2;
            public byte flags1;
            public byte flags2;
            public byte segmentBase3;
        }

        [Flags]
        public enum GDTFlags1 : byte
        {
            Accessed = 0x01,
            ReadWrite = 0x02,
            ConfirmingExpandDown = 0x04,
            Code = 0x08,
            CodeDataSegment = 0x10,
            DPL = 0x20 | 0x40,  // the privilege level (ring level)
            Present = 0x80,

            // breaking the privilege levels apart
            Ring0 = 0,
            Ring1 = 0x20,
            Ring2 = 0x40,
            Ring3 = 0x60
        }

        [Flags]
        public enum GDTFlags2 : byte
        {
            LimitHigh = 0x0F,
            Available = 0x10,
            LongMode = 0x20,
            Big = 0x40,
            Gran = 0x80
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GDT
        {
            public GDTSegment Reserved;
            public GDTSegment KernelCodeSegment;
            public GDTSegment KernelDataSegment;
            public GDTSegment UserCodeSegment;
            public GDTSegment UserDataSegment;
            public GDTSegment TaskStateSegment;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GDTPointer
        {
            public ushort size;
            public uint offset;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IDTPointer
        {
            public ushort limit;
            public uint address;
        }

        // this struct came from an example at https://wiki.osdev.org/Getting_to_Ring_3
        [StructLayout(LayoutKind.Sequential)]
        public struct TSSEntry
        {
            public uint prev_tss;  // The previous TSS - with hardware task switching these form a kind of backward linked list.
            public uint esp0;      // The stack pointer to load when changing to kernel mode.
            public uint ss0;       // The stack segment to load when changing to kernel mode.
                                   // Everything below here is unused.
            public uint esp1; // esp and ss 1 and 2 would be used when switching to rings 1 or 2.
            public uint ss1;
            public uint esp2;
            public uint ss2;
            public uint cr3;
            public uint eip;
            public uint eflags;
            public uint eax;
            public uint ecx;
            public uint edx;
            public uint ebx;
            public uint esp;
            public uint ebp;
            public uint esi;
            public uint edi;
            public uint es;
            public uint cs;
            public uint ss;
            public uint ds;
            public uint fs;
            public uint gs;
            public uint ldt;
            public ushort trap;
            public ushort iomap_base;
        }

        public class TSSEntryWrapper
        {
            public uint prev_tss;  // The previous TSS - with hardware task switching these form a kind of backward linked list.
            public uint esp0;      // The stack pointer to load when changing to kernel mode.
            public uint ss0;       // The stack segment to load when changing to kernel mode.
                                   // Everything below here is unused.
            public uint esp1; // esp and ss 1 and 2 would be used when switching to rings 1 or 2.
            public uint ss1;
            public uint esp2;
            public uint ss2;
            public uint cr3;
            public uint eip;
            public uint eflags;
            public uint eax;
            public uint ecx;
            public uint edx;
            public uint ebx;
            public uint esp;
            public uint ebp;
            public uint esi;
            public uint edi;
            public uint es;
            public uint cs;
            public uint ss;
            public uint ds;
            public uint fs;
            public uint gs;
            public uint ldt;
            public ushort trap;
            public ushort iomap_base;
        }

        [AsmMethod]
        public static void FastCopyBytes(uint src, uint dest, uint length)
        {
        }

        [AsmPlug("CPUHelper.CPU.FastCopyBytes_Void_U4_U4_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void FastCopyBytesAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop ecx");
            assembly.AddAsm("pop edi");
            assembly.AddAsm("pop esi");
            assembly.AddAsm("rep movsb");
        }

        [AsmMethod]
        public static void FastCopyDWords(uint src, uint dest, uint length)
        {
            for (uint i = 0; i < length; i++)
            {
                var data = ReadMemInt(src + (i << 2));
                WriteMemInt(dest + (i << 2), data);
            }
        }

        [AsmPlug("CPUHelper.CPU.FastCopyDWords_Void_U4_U4_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void FastCopyDWordsAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop ecx");
            assembly.AddAsm("pop edi");
            assembly.AddAsm("pop esi");
            assembly.AddAsm("rep movsd");
        }

        [AsmMethod]
        public static void DisableInterrupts()
        {

        }

        [AsmMethod]
        public static void LoadIDT(IDTPointer idt)
        {

        }

        [AsmPlug("CPUHelper.CPU.LoadIDT_Void_ValueType", IL2Asm.BaseTypes.Architecture.X86)]
        private static void LoadIDTAsm(IAssembledMethod assembly)
        {
            /*assembly.AddAsm("pop eax");
            assembly.AddAsm("lidt [eax]");*/
            assembly.AddAsm("lidt [esp+0]");
            assembly.AddAsm("pop eax");
            assembly.AddAsm("pop eax");
        }

        [AsmMethod]
        public static void WriteMemory(uint addr, uint c)
        {

        }

        [AsmPlug("CPUHelper.CPU.WriteMemory_Void_U4_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void WriteMemoryUnsignedAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax"); // character
            assembly.AddAsm("pop ebx"); // address
            assembly.AddAsm("mov [ebx], eax");
        }

        [AsmMethod]
        public static void WriteMemory(int addr, ushort c)
        {

        }

        [AsmPlug("CPUHelper.CPU.WriteMemory_Void_I4_U2", IL2Asm.BaseTypes.Architecture.X86)]
        private static void WriteMemoryAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax"); // character
            assembly.AddAsm("pop ebx"); // address
            assembly.AddAsm("mov [ebx], ax");
        }

        [AsmPlug("CPUHelper.CPU.WriteMemory_Void_I4_U2", IL2Asm.BaseTypes.Architecture.X86_Real)]
        private static void WriteMemoryAsmReal(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop ax");
            assembly.AddAsm("pop bx");
            assembly.AddAsm("mov [bx], ax");
        }

        [AsmMethod]
        public static void OutDxAl(ushort dx, byte al)
        {

        }

        [AsmPlug("CPUHelper.CPU.OutDxAl_Void_U2_U1", IL2Asm.BaseTypes.Architecture.X86)]
        private static void OutDxAlAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax"); // al
            assembly.AddAsm("pop ebx"); // dx
            assembly.AddAsm("push edx");
            assembly.AddAsm("mov edx, ebx");
            assembly.AddAsm("out dx, al");
            assembly.AddAsm("pop edx");
        }

        [AsmMethod]
        public static void OutDxAx(ushort dx, ushort ax)
        {

        }

        [AsmPlug("CPUHelper.CPU.OutDxAx_Void_U2_U2", IL2Asm.BaseTypes.Architecture.X86)]
        private static void OutDxAxAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax"); // ax
            assembly.AddAsm("pop ebx"); // dx
            assembly.AddAsm("push edx");
            assembly.AddAsm("mov edx, ebx");
            assembly.AddAsm("out dx, ax");
            assembly.AddAsm("pop edx");
        }

        [AsmMethod]
        public static void OutDxEax(ushort edx, uint eax)
        {

        }

        [AsmPlug("CPUHelper.CPU.OutDxEax_Void_U2_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void OutDxEaxAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax"); // al
            assembly.AddAsm("pop ebx"); // dx
            assembly.AddAsm("push edx");
            assembly.AddAsm("mov edx, ebx");
            assembly.AddAsm("out dx, eax");
            assembly.AddAsm("pop edx");
        }

        [AsmMethod]
        public static byte InDxByte(ushort dx)
        {
            return 0;
        }

        [AsmPlug("CPUHelper.CPU.InDxByte_U1_U2", IL2Asm.BaseTypes.Architecture.X86)]
        private static void InDxByteAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax"); // address
            assembly.AddAsm("push edx");
            assembly.AddAsm("mov edx, eax");
            assembly.AddAsm("xor eax, eax");
            assembly.AddAsm("in al, dx");
            assembly.AddAsm("pop edx");
            assembly.AddAsm("push eax");
        }

        [AsmMethod]
        public static ushort InDxWord(ushort dx)
        {
            return 0;
        }

        [AsmPlug("CPUHelper.CPU.InDxWord_U2_U2", IL2Asm.BaseTypes.Architecture.X86)]
        private static void InDxWordAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax"); // address
            assembly.AddAsm("push edx");
            assembly.AddAsm("mov edx, eax");
            assembly.AddAsm("xor eax, eax");
            assembly.AddAsm("in ax, dx");
            assembly.AddAsm("pop edx");
            assembly.AddAsm("push eax");
        }

        [AsmMethod]
        public static uint InDxDword(ushort dx)
        {
            return 0;
        }

        [AsmPlug("CPUHelper.CPU.InDxDword_U4_U2", IL2Asm.BaseTypes.Architecture.X86)]
        private static void InDxDwordAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax"); // address
            assembly.AddAsm("push edx");
            assembly.AddAsm("mov edx, eax");
            assembly.AddAsm("in eax, dx");
            assembly.AddAsm("pop edx");
            assembly.AddAsm("push eax");
        }

        public static void InDxMultiDword(ushort dx, uint address, int count)
        {
        }

        [AsmPlug("CPUHelper.CPU.InDxMultiDword_Void_U2_U4_I4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void InDxMultiDwordAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax"); // count
            assembly.AddAsm("pop edi"); // data location
            assembly.AddAsm("pop ebx"); // dx
            assembly.AddAsm("push ecx");    // will get clobbered in a moment
            assembly.AddAsm("push edx");    // will get clobbered in a moment
            assembly.AddAsm("mov ecx, eax");
            assembly.AddAsm("mov edx, ebx");
            assembly.AddAsm("rep insd");
            assembly.AddAsm("pop edx");
            assembly.AddAsm("pop ecx");
        }

        public static void OutDxMultiDword(ushort dx, uint address, int count)
        {
        }

        [AsmPlug("CPUHelper.CPU.OutDxMultiDword_Void_U2_U4_I4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void OutDxMultiDwordAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax"); // count
            assembly.AddAsm("pop esi"); // data location
            assembly.AddAsm("pop ebx"); // dx
            assembly.AddAsm("push ecx");    // will get clobbered in a moment
            assembly.AddAsm("push edx");    // will get clobbered in a moment
            assembly.AddAsm("mov ecx, eax");
            assembly.AddAsm("mov edx, ebx");
            assembly.AddAsm("rep outsd");
            assembly.AddAsm("pop edx");
            assembly.AddAsm("pop ecx");
        }

        [AsmMethod]
        public static void Halt()
        {
        }

        [AsmPlug("CPUHelper.CPU.Halt_Void", IL2Asm.BaseTypes.Architecture.X86)]
        private static void HaltAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("hlt");
        }

        [AsmMethod]
        public static ushort ReadDX()
        {
            return 0;
        }

        [AsmPlug("CPUHelper.CPU.ReadDX_U2", IL2Asm.BaseTypes.Architecture.X86_Real)]
        private static void ReadDXAsmReal(IAssembledMethod assembly)
        {
            assembly.AddAsm("push dx");
        }

        [AsmMethod]
        public static ushort ReadAX()
        {
            return 0;
        }

        [AsmMethod]
        public static uint ReadCR0()
        {
            return 0;
        }

        [AsmPlug("CPUHelper.CPU.ReadCR0_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void ReadCR0Asm(IAssembledMethod assembly)
        {
            assembly.AddAsm("mov eax, cr0");
            assembly.AddAsm("push eax");
        }

        [AsmMethod]
        public static uint ReadEAX()
        {
            return 0;
        }

        [AsmPlug("CPUHelper.CPU.ReadEAX_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void ReadEAXAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("push eax");
        }

        [AsmMethod]
        public static uint ReadEBX()
        {
            return 0;
        }

        [AsmPlug("CPUHelper.CPU.ReadEBX_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void ReadEBXAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("push ebx");
        }

        [AsmMethod]
        public static uint ReadESI()
        {
            return 0;
        }

        [AsmPlug("CPUHelper.CPU.ReadESI_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void ReadESIAsm(IAssembledMethod assembly)
        {
            //assembly.AddAsm("mov eax, esi");
            assembly.AddAsm("push esi");
        }

        [AsmMethod]
        public static void WriteESP(uint value)
        {
        }

        [AsmPlug("CPUHelper.CPU.WriteESP_Void_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void WriteESPAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop esp");
        }

        [AsmMethod]
        public static uint ReadESP()
        {
            return 0;
        }

        [AsmPlug("CPUHelper.CPU.ReadESP_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void ReadESPAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("push esp");
        }

        [AsmMethod]
        public static void WriteEBP(uint value)
        {
        }

        [AsmPlug("CPUHelper.CPU.WriteEBP_Void_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void WriteEBPAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop ebp");
        }

        [AsmMethod]
        public static uint ReadEBP()
        {
            return 0;
        }

        [AsmMethod]
        public static void Pop()
        {
        }

        [AsmPlug("CPUHelper.CPU.Pop_Void", IL2Asm.BaseTypes.Architecture.X86)]
        private static void PopAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax");
        }

        [AsmPlug("CPUHelper.CPU.ReadEBP_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void ReadEBPAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("push ebp");
        }

        [AsmMethod]
        public static uint ReadEIP()
        {
            return 0;
        }

        [AsmPlug("CPUHelper.CPU.ReadEIP_U4", IL2Asm.BaseTypes.Architecture.X86, AsmFlags.None)]
        private static void ReadEIPAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax"); // EIP was the first thing on the stack in prep for ret, so pop it
            assembly.AddAsm("push eax");// push EIP back on the stack so ret will work
            assembly.AddAsm("push eax");// and push EIP on the stack as the return call of this method
            assembly.AddAsm("ret");
        }

        [AsmMethod]
        public static uint ReadCR2()
        {
            return 0;
        }

        [AsmPlug("CPUHelper.CPU.ReadCR2_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void ReadCR2Asm(IAssembledMethod assembly)
        {
            assembly.AddAsm("mov eax, cr2");
            assembly.AddAsm("push eax");
        }

        public enum CPUIDLeaf : uint
        {
            HighestFunctionParameterAndManufacturerId = 0,
            ProcessorInfoAndFeatureBits = 1,
            CacheAndTLBDescriptorInformation = 2,
            ProcessorSerialNumber = 3,
            TheadCoreAndCacheTopology = 4,
            ThermalAndPowerManagerment = 6,
            ExtendedFeatures = 7,
            ExtendedProcessorInfoAndFeatureBits = 0x80000001U,
            VirtualAndPhysicalAddressSizes = 0x80000008U
        }

        public struct CPUIDValue
        {
            public uint eax;
            public uint ebx;
            public uint ecx;
            public uint edx;
        }

        [AsmMethod]
        public static void ReadCPUID(CPUIDLeaf id, ref CPUIDValue value)
        {
        }

        [AsmPlug("CPUHelper.CPU.ReadCPUID_Void_ValueType_ByRefValueType", IL2Asm.BaseTypes.Architecture.X86)]
        private static void ReadCPUID(IAssembledMethod assembly)
        {
            assembly.AddAsm("push ecx");
            assembly.AddAsm("push edx");
            assembly.AddAsm("push ebp");
            assembly.AddAsm("mov eax, [esp+16]");
            assembly.AddAsm("cpuid");
            assembly.AddAsm("mov ebp, [esp+12]");
            assembly.AddAsm("mov [ebp+12], edx");
            assembly.AddAsm("mov [ebp+8], ecx");
            assembly.AddAsm("mov [ebp+4], ebx");
            assembly.AddAsm("mov [ebp], eax");
            assembly.AddAsm("pop ebp");
            assembly.AddAsm("pop edx");
            assembly.AddAsm("pop ecx");
            assembly.AddAsm("pop eax; pop arg 2");
            assembly.AddAsm("pop eax; pop arg 1");
        }

        [AsmMethod]
        public static void WriteMemByte(uint addr, byte data)
        {

        }

        [AsmPlug("CPUHelper.CPU.WriteMemByte_Void_U4_U1", IL2Asm.BaseTypes.Architecture.X86)]
        private static void WriteMemByteAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax");
            assembly.AddAsm("pop ebx");
            assembly.AddAsm("mov byte [ebx], al");
        }

        [AsmMethod]
        public static void WriteMemShort(uint addr, ushort data)
        {

        }

        [AsmPlug("CPUHelper.CPU.WriteMemShort_Void_U4_U2", IL2Asm.BaseTypes.Architecture.X86)]
        private static void WriteMemShortAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax");
            assembly.AddAsm("pop ebx");
            assembly.AddAsm("mov word [ebx], ax");
        }

        [AsmMethod]
        public static void WriteMemInt(uint addr, uint data)
        {

        }

        [AsmPlug("CPUHelper.CPU.WriteMemInt_Void_U4_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void WriteMemIntAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax");
            assembly.AddAsm("pop ebx");
            assembly.AddAsm("mov [ebx], eax");
        }

        [AsmMethod]
        public static uint ReadMemInt(uint addr)
        {
            return 0;
        }

        [AsmPlug("CPUHelper.CPU.ReadMemInt_U4_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void ReadMemIntAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop ebx");
            assembly.AddAsm("mov eax, [ebx]");
            assembly.AddAsm("push eax");
        }

        [AsmMethod]
        public static ushort ReadMemShort(uint addr)
        {
            return 0;
        }

        [AsmPlug("CPUHelper.CPU.ReadMemShort_U2_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void ReadMemShortAsm32(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop ebx");
            assembly.AddAsm("mov ax, [ebx]");
            assembly.AddAsm("push eax");
        }

        [AsmMethod]
        public static ushort ReadMemShort(ushort addr)
        {
            return 0;
        }

        [AsmPlug("CPUHelper.CPU.ReadMemShort_U2_U2", IL2Asm.BaseTypes.Architecture.X86)]
        private static void ReadMemShortAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop ebx");
            assembly.AddAsm("mov ax, [bx]");
            assembly.AddAsm("and eax, 65535");
            assembly.AddAsm("push eax");
        }

        [AsmPlug("CPUHelper.CPU.ReadMemShort_U2_U2", IL2Asm.BaseTypes.Architecture.X86_Real)]
        private static void ReadMemShortAsmReal(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop bx");
            assembly.AddAsm("mov ax, [bx]");
            assembly.AddAsm("push ax");
        }

        [AsmMethod]
        public static byte ReadMemByte(ushort addr)
        {
            return 0;
        }

        [AsmPlug("CPUHelper.CPU.ReadMemByte_U1_U2", IL2Asm.BaseTypes.Architecture.X86)]
        private static void ReadMemByteAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop ebx");
            assembly.AddAsm("mov ax, [bx]");
            assembly.AddAsm("and eax, 255");
            assembly.AddAsm("push eax");
        }

        [AsmPlug("CPUHelper.CPU.ReadMemByte_U1_U2", IL2Asm.BaseTypes.Architecture.X86_Real)]
        private static void ReadMemByteAsmReal(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop bx");
            assembly.AddAsm("xor ax, ax");
            assembly.AddAsm("mov al, [bx]");
            assembly.AddAsm("push ax");
        }

        [AsmMethod]
        public static byte ReadMemByte(uint addr)
        {
            return 0;
        }

        [AsmPlug("CPUHelper.CPU.ReadMemByte_U1_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void ReadMemByteAsm32(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop ebx");
            assembly.AddAsm("mov ax, [ebx]");
            assembly.AddAsm("and eax, 255");
            assembly.AddAsm("push eax");
        }

        [AsmMethod]
        public static void CopyByte<T>(uint source, uint sourceOffset, ref T destination, uint destinationOffset)
        {

        }

        [AsmPlug("CPUHelper.CPU.CopyByte<SMAP_entry>_Void_U4_U4_ByRef", IL2Asm.BaseTypes.Architecture.X86)]
        private static void CopyByteTAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("push ecx");

            assembly.AddAsm("mov eax, [esp + 16]");
            assembly.AddAsm("add eax, [esp + 12]"); // source + sourceOffset
            assembly.AddAsm("mov ebx, eax");
            assembly.AddAsm("mov al, [ebx]");       // read source
            assembly.AddAsm("mov cl, al");

            assembly.AddAsm("mov eax, [esp + 8]");
            assembly.AddAsm("add eax, [esp + 4]"); // dest + destOffset
            assembly.AddAsm("mov ebx, eax");
            assembly.AddAsm("mov [ebx], cl");       // copy source to destination

            assembly.AddAsm("pop ecx");
            assembly.AddAsm("pop eax");
            assembly.AddAsm("pop eax");
            assembly.AddAsm("pop eax");
            assembly.AddAsm("pop eax");
        }

        [AsmMethod]
        public static void Jump(uint addr)
        {

        }

        [AsmPlug("CPUHelper.CPU.Jump_Void_U4", IL2Asm.BaseTypes.Architecture.X86_Real)]
        private static void JumpAsmReal(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop ax");
            assembly.AddAsm("jmp ax");
        }

        [AsmPlug("CPUHelper.CPU.Jump_Void_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void JumpAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax");
            assembly.AddAsm("jmp eax");
        }

        [AsmMethod]
        public static void FastA20()
        {

        }

        [AsmPlug("CPUHelper.CPU.FastA20_Void", IL2Asm.BaseTypes.Architecture.X86)]
        private static void FastA20Asm(IAssembledMethod assembly)
        {
            // from https://wiki.osdev.org/A20_Line
            assembly.AddAsm("in al, 0x92");
            assembly.AddAsm("test al, 2");
            assembly.AddAsm("jnz fasta20_enabled");
            assembly.AddAsm("or al, 2");
            assembly.AddAsm("and al, 0xFE");
            assembly.AddAsm("out 0x92, al");
            assembly.AddAsm("fasta20_enabled:");
        }

        [AsmMethod]
        public static void Interrupt3()
        {

        }

        [AsmPlug("CPUHelper.CPU.Interrupt3_Void", IL2Asm.BaseTypes.Architecture.X86)]
        private static void Interrupt3Asm(IAssembledMethod assembly)
        {
            assembly.AddAsm("int 3");
        }

        [AsmMethod]
        public static void Interrupt4()
        {

        }

        [AsmPlug("CPUHelper.CPU.Interrupt4_Void", IL2Asm.BaseTypes.Architecture.X86)]
        private static void Interrupt4Asm(IAssembledMethod assembly)
        {
            assembly.AddAsm("int 4");
        }

        [AsmMethod]
        public static void Interrupt30(char c)
        {

        }

        [AsmPlug("CPUHelper.CPU.Interrupt30_Void_Char", IL2Asm.BaseTypes.Architecture.X86)]
        private static void Interrupt30Asm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax"); // store c in eax
            assembly.AddAsm("int 30");
        }

        [AsmMethod]
        public static void Cli()
        {

        }

        [AsmPlug("CPUHelper.CPU.Cli_Void", IL2Asm.BaseTypes.Architecture.X86)]
        private static void CliAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("cli");
        }

        [AsmMethod]
        public static void Sti()
        {

        }

        [AsmPlug("CPUHelper.CPU.Sti_Void", IL2Asm.BaseTypes.Architecture.X86)]
        private static void StiAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("sti");
        }

        [AsmMethod]
        public static void SetPageDirectory(uint physicalAddresses)
        {

        }

        [AsmPlug("CPUHelper.CPU.SetPageDirectory_Void_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void SetPageDirectoryAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax");         // pop address of uint[]
            assembly.AddAsm("mov cr3, eax");    // move address of the start of the uint[] into cr3
            assembly.AddAsm("mov eax, cr0");    // load cr0
            assembly.AddAsm("or eax, 0x80000000");  // enable page bit
            assembly.AddAsm("mov cr0, eax");    // load cr0 with the page bit now set
        }

        [AsmMethod]
        public static void SetPageDirectoryFast(uint physicalAddresses)
        {

        }

        [AsmPlug("CPUHelper.CPU.SetPageDirectoryFast_Void_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void SetPageDirectoryFastAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax");         // pop address of uint[]
            assembly.AddAsm("mov cr3, eax");    // move address of the start of the uint[] into cr3
        }

        [AsmMethod]
        public static uint Rdtsc()
        {
            return 0;
        }

        [AsmPlug("CPUHelper.CPU.Rdtsc_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void RdtscAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("rdtsc");
            assembly.AddAsm("push eax");
        }

        [AsmMethod]
        public static uint ReadEDX()
        {
            return 0;
        }

        [AsmPlug("CPUHelper.CPU.ReadEDX_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void ReadEDXAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("push edx");
        }

        [AsmMethod]
        public static void FlushTSS()
        {
            return;
        }

        [AsmPlug("CPUHelper.CPU.FlushTSS_Void", IL2Asm.BaseTypes.Architecture.X86)]
        private static void FlushTSSAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("mov ax, (5 * 8) | 0");
            assembly.AddAsm("ltr ax");
        }

        [AsmMethod]
        public static void JumpKernelMode(uint addr)
        {
            return;
        }

        [AsmPlug("CPUHelper.CPU.JumpKernelMode_Void_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void JumpKernelModeAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop edi"); // grab the address to jump to

            assembly.AddAsm("mov ax, (2 * 8) | 0 ; kernel segment");
            assembly.AddAsm("mov ds, ax");
            assembly.AddAsm("mov es, ax");
            assembly.AddAsm("mov fs, ax");
            assembly.AddAsm("mov gs, ax ; SS is handled by iret");

            //set up the stack frame iret expects
            assembly.AddAsm("mov eax, esp");
            assembly.AddAsm("push (2 * 8) | 0 ; kernel data selector");
            assembly.AddAsm("push eax ; current esp");
            assembly.AddAsm("pushf ; eflags");
            assembly.AddAsm("push (1 * 8) | 0 ; kernel code selector");
            assembly.AddAsm("push edi ; instruction address to return to");

            assembly.AddAsm("iret");
        }

        [AsmMethod]
        public static void JumpUserMode(uint addr)
        {
            return;
        }

        [AsmPlug("CPUHelper.CPU.JumpUserMode_Void_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void JumpUserModeAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop edi"); // grab the address to jump to

            assembly.AddAsm("mov ax, (4 * 8) | 3 ; ring 3 data with bottom 2 bits set for ring 3");
            assembly.AddAsm("mov ds, ax");
            assembly.AddAsm("mov es, ax");
            assembly.AddAsm("mov fs, ax");
            assembly.AddAsm("mov gs, ax ; SS is handled by iret");

            //set up the stack frame iret expects
            assembly.AddAsm("mov eax, esp");
            assembly.AddAsm("push (4 * 8) | 3 ; data selector");
            assembly.AddAsm("push eax ; current esp");
            assembly.AddAsm("pushf ; eflags");
            assembly.AddAsm("push (3 * 8) | 3 ; code selector (ring 3 code with bottom 2 bits set for ring 3)");
            assembly.AddAsm("push edi ; instruction address to return to");

            assembly.AddAsm("iret");
        }
    }
}
