using System;
using System.Collections.Generic;

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
    }
}
