using System.Runtime.InteropServices;
using CPUHelper;

namespace TestIL
{
    class Program
    {
        private const string _welcomeMessage = "Welcome to C#!";

        [BootEntryPoint]
        [RealMode]
        static void Main()
        {
            for (int i = 1; i < 80; i++)
            {
                int prime = IsPrime(i);
                if (prime != 0)
                {
                    WriteHex(prime);
                    Bios.WriteByte('\n');
                    Bios.WriteByte('\r');
                }
            }

            while (true) ;
        }

        [RealMode]
        public static int IsPrime(int num)
        {
            if (num == 1) return 0;
            else
            {
                for (int i = 2; i < num; i++)
                {
                    if (Modulo(num, i) == 0) return 0;
                }
                return num;
            }
        }

        [RealMode]
        public static int Modulo(int source, int div)
        {
            int remainder = source;
            while (remainder >= div) remainder -= div;
            return remainder;
        }

        [RealMode]
        public static void WriteHex(int value)
        {
            WriteHexChar(value >> 4);
            WriteHexChar(value & 0x0f);
        }

        [RealMode]
        public static void WriteHexChar(int value)
        {
            if (value >= 10) Bios.WriteByte(value + 55);
            else Bios.WriteByte(value + 48);
        }

        [RealMode]
        public static void WriteLine(string s)
        {
            Write(s);
            Bios.WriteByte((byte)'\n');
            Bios.WriteByte((byte)'\r');
        }

        [RealMode]
        public static void Write(string s)
        {
            int l = s.Length;

            for (int i = 0; i < l; i++)
            {
                Bios.WriteByte((byte)s[i]);
            }
        }

        [RealMode]
        public static void WriteInt(int value)
        {
            int divisor = 1;

            while (divisor * 10 <= value)
            {
                divisor *= 10;
            }

            while (divisor > 0)
            {
                int c = Divide(value, divisor);
                Bios.WriteByte(Modulo(c, 10) + 48);

                divisor = Divide(divisor, 10);
            }
        }

        [RealMode]
        public static int Divide(int source, int div)
        {
            int remainder = source;
            int quotient = 0;

            while (remainder >= div)
            {
                remainder -= div;
                quotient++;
            }

            return quotient;
        }
    }
}
