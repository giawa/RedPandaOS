namespace Kernel.Devices
{
    public static class COM
    {
        private static bool _initialized;

        public static int Initialize()
        {
            CPUHelper.CPU.OutDxAl(0x3f8 + 1, 0x00); // disable interrupts
            CPUHelper.CPU.OutDxAl(0x3f8 + 3, 0x80); // enable DLAB (baud rate divisor)
            CPUHelper.CPU.OutDxAl(0x3f8 + 0, 0x03); // set divisor to 0x0003 (low byte)
            CPUHelper.CPU.OutDxAl(0x3f8 + 1, 0x00); // set divisor to 0x0003 (high byte)
            CPUHelper.CPU.OutDxAl(0x3f8 + 3, 0x03); // 8 bits, no parity, one stop bit
            CPUHelper.CPU.OutDxAl(0x3f8 + 2, 0xC7); // enable FIFO
            CPUHelper.CPU.OutDxAl(0x3f8 + 4, 0x0B); // IRQs enabled
            CPUHelper.CPU.OutDxAl(0x3f8 + 4, 0x1E); // set in loopback mode
            CPUHelper.CPU.OutDxAl(0x3f8 + 0, 0xAE); // test serial chip (send a byte and check below to ensure we receive it)

            if (CPUHelper.CPU.InDxByte(0x3f8) != 0xAE) return 1;

            CPUHelper.CPU.OutDxAl(0x3f8 + 4, 0x0F); // disable loopback mode
            _initialized = true;

            return 0;
        }

        public static void Write(byte c)
        {
            while ((CPUHelper.CPU.InDxByte(0x3f8 + 4) & 0x20) != 0) ;

            CPUHelper.CPU.OutDxAl(0x3f8, c);
        }

        public static void WriteFormattedString(string s, uint u1, uint u2, uint u3, ushort effect = 0x0f00)
        {
            if (!_initialized) return;  // not initialized yet

            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c == '{' && (s[i + 2] == '}' || s[i + 4] == '}'))
                {
                    uint value = 0;
                    if (s[i + 1] == '0') value = u1;
                    else if (s[i + 1] == '1') value = u2;
                    else if (s[i + 1] == '2') value = u3;

                    if (s[i + 2] == ':' && s[i + 3] == 'X')
                    {
                        WriteHex(value);
                        i += 2;
                    }
                    else WriteInt(value);

                    i += 2;
                    continue;
                }

                if (c > 255) c = '?';
                Write((byte)c);
            }
        }

        public static void WriteInt(uint value)
        {
            if (!_initialized) return;  // not initialized yet

            uint divisor = 1;

            while (divisor * 10 <= value)
            {
                divisor *= 10;
            }

            while (divisor > 0)
            {
                uint c = value / divisor;// Runtime.Math32.Divide(value, divisor);
                //Bios.WriteByte((byte)((c % 10) + 48));
                Write((byte)((c % 10) + 48));

                divisor = divisor / 10;// Runtime.Math32.Divide(divisor, 10);
            }
        }

        public static void WriteInt(int value)
        {
            if (!_initialized) return;  // not initialized yet

            if (value < 0)
            {
                Write((byte)'-');
                WriteInt((uint)(-value));
            }
            else WriteInt((uint)value);
        }

        public static void WriteString(string s, ushort effect = 0x0f00)
        {
            if (!_initialized) return;  // not initialized yet

            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c == 0) break;
                if (c > 255) c = '?';
                Write((byte)c);
            }
        }

        public static void WriteLine()
        {
            Write((byte)'\n');
        }

        public static void WriteHex(uint value)
        {
            WriteHexChar((byte)(value >> 28));
            WriteHexChar((byte)(value >> 24));
            WriteHexChar((byte)(value >> 20));
            WriteHexChar((byte)(value >> 16));
            WriteHexChar((byte)(value >> 12));
            WriteHexChar((byte)(value >> 8));
            WriteHexChar((byte)(value >> 4));
            WriteHexChar((byte)(value));
        }

        public static void WriteHex(int value)
        {
            WriteHexChar(value >> 28);
            WriteHexChar(value >> 24);
            WriteHexChar(value >> 20);
            WriteHexChar(value >> 16);
            WriteHexChar(value >> 12);
            WriteHexChar(value >> 8);
            WriteHexChar(value >> 4);
            WriteHexChar(value);
        }

        public static void WriteHex(short value)
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
            if (!_initialized) return;  // not initialized yet

            value &= 0x0f;
            if (value >= 10) Write((byte)(value + 55));
            else Write((byte)(value + 48));
        }
    }
}
