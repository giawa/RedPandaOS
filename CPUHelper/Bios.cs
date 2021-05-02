using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CPUHelper
{
    /*public interface ICanBeAssembled
    {
        void Emit(IAssembler assembler);
    }

    public class ASCIIString : ICanBeAssembled
    {
        private string _label;
        private string _data;

        public ASCIIString(string label, string data)
        {
            _label = label;
            _data = data;
        }

        public void Emit(IAssembler assembler)
        {
            assembler.AddToData(_label, _data);
        }
    }*/

    public static class Bios
    {
        public static void WriteByte(byte c)
        {
            Console.Write((char)c);
        }

        public static void WriteByte(int c)
        {
            Console.Write((char)(c & 0xff));
        }

        public static void EnterProtectedMode(ref CPU.GDT gdt)
        {

        }

        public static ushort LoadDisk(ushort highAddr, ushort lowAddr, byte drive, byte sectors)
        {
            return 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SMAP_ret
        {
            public ushort sig1;
            public ushort sig2;
            public ushort contId1;
            public ushort contId2;
        }

        public static ushort DetectMemory(ushort address, ref SMAP_ret value)
        {
            return 0;
        }

        public static bool EnableA20()
        {
            return true;
        }
    }
}
