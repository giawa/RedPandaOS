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

        public static void DisableInterrupts()
        {

        }

        public static void LoadGDT(GDT gdt)
        {

        }

        public static void LoadIDT(IDTPointer idt)
        {

        }

        public static void WriteMemory(int addr, ushort c)
        {

        }

        public static void OutDxAl(ushort dx, byte al)
        {

        }

        public static void OutDxEax(ushort edx, uint eax)
        {

        }

        public static byte InDxByte(ushort dx)
        {
            return 0;
        }

        public static uint InDxDword(ushort dx)
        {
            return 0;
        }

        public static ushort ReadDX()
        {
            return 0;
        }

        public static ushort ReadAX()
        {
            return 0;
        }

        public static uint ReadCR0()
        {
            return 0;
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

        public static void ReadCPUID(CPUIDLeaf id, ref CPUIDValue value)
        {
        }

        public static void WriteMemInt(uint addr, uint data)
        {

        }

        public static uint ReadMemInt(uint addr)
        {
            return 0;
        }

        public static ushort ReadMemShort(ushort addr)
        {
            return 0;
        }

        public static byte ReadMemByte(ushort addr)
        {
            return 0;
        }

        public static void CopyByte<T>(uint source, uint sourceOffset, ref T destination, uint destinationOffset)
        {
            
        }

        public static void Jump(uint addr)
        {

        }

        public static void FastA20()
        {

        }

        public static void Interrupt3()
        {

        }

        public static void Interrupt4()
        {

        }
    }
}
