using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Emulator.CPU.x86
{
    public partial class CPU
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

        private GDT _gdt;
    }
}
