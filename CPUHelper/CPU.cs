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

        public static void DisableInterrupts()
        {

        }

        public static void LoadGDT(GDT gdt)
        {

        }

        public static void WriteMemory(int addr, int c)
        {

        }

        public static void OutDxAl(ushort dx, byte al)
        {

        }

        public static byte InDx(ushort dx)
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
    }
}
