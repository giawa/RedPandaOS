using IL2Asm;

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
            int tens = 0;
            int scratch = value;

            while (scratch > 0)
            {
                tens++;
                scratch = Divide(scratch, 10);
            }

            while (tens > 0)
            {
                scratch = 1;
                for (int i = 0; i < tens - 1; i++) scratch *= 10;

                int c = Divide(value, scratch);
                c = Modulo(c, 10);
                c += 48;

                Bios.WriteByte((byte)c);

                tens--;
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
