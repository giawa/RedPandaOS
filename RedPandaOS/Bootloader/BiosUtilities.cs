using CPUHelper;
using Runtime;

namespace Bootloader
{
    static class BiosUtilities
    {
        public static void WriteHex(int value)
        {
            WriteHexChar(value >> 12);
            WriteHexChar(value >> 8);
            WriteHexChar(value >> 4);
            WriteHexChar(value);
        }

        public static void WriteHex(byte value)
        {
            WriteHexChar(value >> 4);
            WriteHexChar(value);
        }

        public static void WriteHexChar(int value)
        {
            value &= 0x0f;
            if (value >= 10) Bios.WriteByte((byte)(value + 55));
            else Bios.WriteByte((byte)(value + 48));
        }

        public static void WriteLine(string s)
        {
            Write(s);
            Bios.WriteByte((byte)'\n');
            Bios.WriteByte((byte)'\r');
        }

        public static void WriteLine()
        {
            Bios.WriteByte((byte)'\n');
            Bios.WriteByte((byte)'\r');
        }

        public static void Write(string s)
        {
            int i = 0;

            while (s[i] != 0)
            {
                Bios.WriteByte((byte)s[i]);
                i += 1;
            }
        }

        public static void WriteInt(short value)
        {
            short divisor = 1;

            while (divisor * 10 <= value)
            {
                divisor *= 10;
            }

            while (divisor > 0)
            {
                short c = Math16.Divide(value, divisor);
                Bios.WriteByte((byte)(Math16.Modulo(c, 10) + 48));

                divisor = Math16.Divide(divisor, 10);
            }
        }
    }
}
