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

        public static ushort ReadMem(ushort addr)
        {
            return 0;
        }
    }
}
