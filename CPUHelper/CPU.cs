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

        [StructLayout(LayoutKind.Sequential)]
        public struct GDT
        {
            public GDTSegment Reserved;
            public GDTSegment KernelCodeSegment;
            public GDTSegment KernelDataSegment;
            //public GDTSegment UserCodeSegment;
            //public GDTSegment UserDataSegment;
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

        [AsmMethod]
        public static void DisableInterrupts()
        {

        }

        [AsmMethod]
        public static void LoadGDT(GDT gdt)
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
            assembly.AddAsm("in al, dx");
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
    }
}
