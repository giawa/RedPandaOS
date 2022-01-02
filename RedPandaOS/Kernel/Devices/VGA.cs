using CPUHelper;
using Runtime;

namespace Kernel.Devices
{
    public static class VGA
    {
        private const uint VIDEO_MEMORY = 0xb8000;
        private const int VGA_WIDTH = 80;
        private const int VGA_HEIGHT = 25;
        private static uint offset = VIDEO_MEMORY;

        static VGA()
        {
            Clear();
        }

        public static void ResetPosition()
        {
            offset = VIDEO_MEMORY;
        }

        public static void WriteFormattedString(string s, uint u1, ushort effect = 0x0f00)
        {
            WriteFormattedString(s, u1, 0, effect);
        }

        public static void WriteFormattedString(string s, uint u1, uint u2, uint u3, ushort effect = 0x0f00)
        {
            int i = 0;

            while (s[i] != 0)
            {
                if (s[i] == '{' && (s[i + 2] == '}' || s[i + 4] == '}'))
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

                    i += 3;
                    continue;
                }

                CPU.WriteMemory((int)offset, (ushort)(s[i] | effect));
                i++;
                offset += 2;
            }

            Scroll();
            SetCursorPos((offset - VIDEO_MEMORY) >> 1);
        }

        public static void WriteInt(uint value)
        {
            uint divisor = 1;

            while (divisor * 10 <= value)
            {
                divisor *= 10;
            }

            while (divisor > 0)
            {
                uint c = value / divisor;// Runtime.Math32.Divide(value, divisor);
                //Bios.WriteByte((byte)((c % 10) + 48));
                VGA.WriteChar((int)(c % 10) + 48);

                divisor = divisor / 10;// Runtime.Math32.Divide(divisor, 10);
            }
        }

        public static void WriteInt(int value)
        {
            if (value < 0)
            {
                VGA.WriteChar('-');
                WriteInt((uint)(-value));
            }
            else WriteInt((uint)value);
        }

        public static void WriteString(string s, ushort effect = 0x0f00)
        {
            int i = 0;

            while (s[i] != 0)
            {
                CPU.WriteMemory((int)offset, (ushort)(s[i] | effect));
                i++;
                offset += 2;
            }

            Scroll();
            SetCursorPos((offset - VIDEO_MEMORY) >> 1);
        }

        public static void WriteLine()
        {
            offset += 160;
            offset -= VIDEO_MEMORY;
            offset -= offset % 160;
            offset += VIDEO_MEMORY;

            Scroll();
        }

        public static void Delete()
        {
            offset -= 2;
        }

        public static void Clear()
        {
            /*offset = VIDEO_MEMORY;
            for (int i = 0; i < VGA_WIDTH * VGA_HEIGHT; i++)
                WriteVideoMemoryChar(' ');
            offset = VIDEO_MEMORY;*/
            for (int i = (int)VIDEO_MEMORY; i < (int)VIDEO_MEMORY + VGA_WIDTH * VGA_HEIGHT * 2; i+=2)
            {
                CPU.WriteMemory(i, ' ');
            }
            offset = VIDEO_MEMORY;
        }

        public static void WriteChar(int c, ushort effect = 0x0f00)
        {
            CPU.WriteMemory((int)offset, (ushort)((c & 255) | effect));
            offset += 2;
            Scroll();
            SetCursorPos((offset - VIDEO_MEMORY) >> 1);
        }

        public static void DisableCursor()
        {
            CPU.OutDxAl(0x3D4, 0x0A);
            CPU.OutDxAl(0x3D5, 0x20);
        }

        public static void EnableCursor(byte start, byte end)
        {
            CPU.OutDxAl(0x3D4, 0x0A);
            CPU.OutDxAl(0x3D5, (byte)((CPU.InDxByte(0x3D5) & 0xC0) | start));

            CPU.OutDxAl(0x3D4, 0x0B);
            CPU.OutDxAl(0x3D5, (byte)((CPU.InDxByte(0x3D5) & 0xE0) | end));
        }

        public static void SetCursorPos(int x, int y)
        {
            uint pos = (uint)(y * VGA_WIDTH + x);
            SetCursorPos(pos);
        }

        public static void SetCursorPos(uint pos)
        {
            CPU.OutDxAl(0x3D4, 0x0F);
            CPU.OutDxAl(0x3D5, (byte)pos);

            CPU.OutDxAl(0x3D4, 0x0E);
            CPU.OutDxAl(0x3D5, (byte)(pos >> 8));
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
            value &= 0x0f;
            if (value >= 10) WriteChar(value + 55);
            else WriteChar(value + 48);
        }

        public static void Scroll()
        {
            if (offset >= VIDEO_MEMORY + 25 * VGA_WIDTH * 2)
            {
                for (int i = 0; i < (VGA_HEIGHT - 1) * VGA_WIDTH * 2; i += 4)
                {
                    CPU.WriteMemInt((uint)((int)VIDEO_MEMORY + i), CPU.ReadMemInt((int)VIDEO_MEMORY + 160 + (uint)i));
                }

                for (int i = (VGA_HEIGHT - 1) * VGA_WIDTH * 2; i < VGA_HEIGHT * VGA_WIDTH * 2; i += 4)
                {
                    CPU.WriteMemInt((uint)((int)VIDEO_MEMORY + i), 0x00200020);
                }

                offset = VIDEO_MEMORY + 24 * VGA_WIDTH * 2;
            }
        }
    }
}
