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
            public ushort reserved1;
            public ushort reserved2;
            public ushort reserved3;
            public ushort reserved4;

            public GDTSegment CodeSegment;
            public GDTSegment DataSegment;
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
    }
}
