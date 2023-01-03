using System.Runtime.InteropServices;

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
        private IDT _idt;

        [StructLayout(LayoutKind.Sequential)]
        public struct IDT_Entry
        {
            public ushort base_lo;
            public ushort sel;
            public byte always0;
            public byte flags;
            public ushort base_hi;
        }

        // the lengths we go to so that we can stick with 'safe' code >.>
        [StructLayout(LayoutKind.Sequential)]
        public struct IDT
        {
            // software ISRs
            public IDT_Entry entry0;
            public IDT_Entry entry1;
            public IDT_Entry entry2;
            public IDT_Entry entry3;
            public IDT_Entry entry4;
            public IDT_Entry entry5;
            public IDT_Entry entry6;
            public IDT_Entry entry7;
            public IDT_Entry entry8;
            public IDT_Entry entry9;
            public IDT_Entry entry10;
            public IDT_Entry entry11;
            public IDT_Entry entry12;
            public IDT_Entry entry13;
            public IDT_Entry entry14;
            public IDT_Entry entry15;
            public IDT_Entry entry16;
            public IDT_Entry entry17;
            public IDT_Entry entry18;
            public IDT_Entry entry19;
            public IDT_Entry entry20;
            public IDT_Entry entry21;
            public IDT_Entry entry22;
            public IDT_Entry entry23;
            public IDT_Entry entry24;
            public IDT_Entry entry25;
            public IDT_Entry entry26;
            public IDT_Entry entry27;
            public IDT_Entry entry28;
            public IDT_Entry entry29;
            public IDT_Entry entry30;
            public IDT_Entry entry31;

            // hardware IRQs
            public IDT_Entry entry32;
            public IDT_Entry entry33;
            public IDT_Entry entry34;
            public IDT_Entry entry35;
            public IDT_Entry entry36;
            public IDT_Entry entry37;
            public IDT_Entry entry38;
            public IDT_Entry entry39;
            public IDT_Entry entry40;
            public IDT_Entry entry41;
            public IDT_Entry entry42;
            public IDT_Entry entry43;
            public IDT_Entry entry44;
            public IDT_Entry entry45;
            public IDT_Entry entry46;
            public IDT_Entry entry47;

            public IDT_Entry this[int a]
            {
                get
                {
                    switch (a)
                    {
                        case 0: return entry0;
                        case 1: return entry1;
                        case 2: return entry2;
                        case 3: return entry3;
                        case 4: return entry4;
                        case 5: return entry5;
                        case 6: return entry6;
                        case 7: return entry7;
                        case 8: return entry8;
                        case 9: return entry9;
                        case 10: return entry10;
                        case 11: return entry11;
                        case 12: return entry12;
                        case 13: return entry13;
                        case 14: return entry14;
                        case 15: return entry15;
                        case 16: return entry16;
                        case 17: return entry17;
                        case 18: return entry18;
                        case 19: return entry19;
                        case 20: return entry20;
                        case 21: return entry21;
                        case 22: return entry22;
                        case 23: return entry23;
                        case 24: return entry24;
                        case 25: return entry25;
                        case 26: return entry26;
                        case 27: return entry27;
                        case 28: return entry28;
                        case 29: return entry29;
                        case 30: return entry30;
                        case 31: return entry31;
                        case 32: return entry32;
                        case 33: return entry33;
                        case 34: return entry34;
                        case 35: return entry35;
                        case 36: return entry36;
                        case 37: return entry37;
                        case 38: return entry38;
                        case 39: return entry39;
                        case 40: return entry40;
                        case 41: return entry41;
                        case 42: return entry42;
                        case 43: return entry43;
                        case 44: return entry44;
                        case 45: return entry45;
                        case 46: return entry46;
                        case 47: return entry47;
                        default: throw new IndexOutOfRangeException();
                    }
                }
            }
        }
    }
}
