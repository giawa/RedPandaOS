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
        private static bool initialized = false;

        static VGA()
        {
            Clear();
        }

        public static void ResetPosition()
        {
            offset = VIDEO_MEMORY;
        }

        public static void SetPosition(uint x, uint y)
        {
            offset = VIDEO_MEMORY + ((y * VGA_WIDTH + x) << 1);
        }

        public static bool EnableScrolling { get; set; }

        public static void WriteFormattedString(string s, uint u1, ushort effect = 0x0f00)
        {
            WriteFormattedString(s, u1, 0, effect);
        }

        public static void WriteFormattedString(string s, uint u1, uint u2, uint u3, ushort effect = 0x0f00)
        {
            if (!initialized) return;  // not initialized yet

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
                CPU.WriteMemory((int)offset, (ushort)(c | effect));
                offset += 2;
            }

            Scroll();
            SetCursorPos((offset - VIDEO_MEMORY) >> 1);
        }

        public static void WriteInt(uint value)
        {
            if (!initialized) return;  // not initialized yet

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

            Scroll();
            SetCursorPos((offset - VIDEO_MEMORY) >> 1);
        }

        public static void WriteInt(int value)
        {
            if (!initialized) return;  // not initialized yet

            if (value < 0)
            {
                VGA.WriteChar('-');
                WriteInt((uint)(-value));
            }
            else WriteInt((uint)value);
        }

        public static void WriteString(string s, ushort effect = 0x0f00)
        {
            if (!initialized) return;  // not initialized yet

            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c == 0) break;
                if (c > 255) c = '?';
                CPU.WriteMemory((int)offset, (ushort)(c | effect));
                offset += 2;
            }

            Scroll();
            SetCursorPos((offset - VIDEO_MEMORY) >> 1);
        }

        public static void WriteLine()
        {
            if (!initialized) return;  // not initialized yet

            offset += 160;
            offset -= (offset - VIDEO_MEMORY) % 160;

            Scroll();
            SetCursorPos((offset - VIDEO_MEMORY) >> 1);
        }

        public static void Delete()
        {
            if (!initialized) return;  // not initialized yet

            offset -= 2;

            Scroll();
            SetCursorPos((offset - VIDEO_MEMORY) >> 1);
        }

        public static void Clear()
        {
            for (int i = (int)VIDEO_MEMORY; i < (int)VIDEO_MEMORY + VGA_WIDTH * VGA_HEIGHT * 2; i += 2)
            {
                CPU.WriteMemory(i, (' ' | 0x0f00));
            }
            offset = VIDEO_MEMORY;
            initialized = true;

            Scroll();
            SetCursorPos((offset - VIDEO_MEMORY) >> 1);
        }

        public static void WriteChar(int c, ushort effect = 0x0f00)
        {
            if (!initialized) return;  // not initialized yet

            CPU.WriteMemory((int)offset, (ushort)((c & 255) | effect));
            offset += 2;
            Scroll();
            SetCursorPos((offset - VIDEO_MEMORY) >> 1);
        }

        private static bool _cursorEnabled = false;

        public static void DisableCursor()
        {
            _cursorEnabled = false;

            CPU.OutDxAl(0x3D4, 0x0A);
            CPU.OutDxAl(0x3D5, 0x20);
        }

        public static void EnableCursor()
        {
            EnableCursor(14, 15);
            SetCursorPos((offset - VIDEO_MEMORY) >> 1);
        }

        public static void EnableCursor(byte start, byte end)
        {
            _cursorEnabled = true;

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
            if (!_cursorEnabled) return;

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
            if (!initialized) return;  // not initialized yet

            value &= 0x0f;
            if (value >= 10) WriteChar(value + 55);
            else WriteChar(value + 48);
        }

        public static void Scroll()
        {
            if (!EnableScrolling) return;

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
