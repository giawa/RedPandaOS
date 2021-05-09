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

        public static void ResetPosition()
        {
            offset = VIDEO_MEMORY;
        }

        public static void WriteVideoMemoryString(string s, ushort effect = 0x0f00)
        {
            int i = 0;

            while (s[i] != 0)
            {
                CPU.WriteMemory((int)offset, s[i] | effect);
                i++;
                offset += 2;
            }

            SetCursorPos((offset - VIDEO_MEMORY) >> 1);
        }

        public static void WriteLine()
        {
            offset += 160;
            offset -= VIDEO_MEMORY;
            offset -= Math32.Modulo(offset, 160);
            offset += VIDEO_MEMORY;
        }

        public static void Delete()
        {
            offset -= 2;
        }

        public static void Clear()
        {
            offset = VIDEO_MEMORY;
            for (int i = 0; i < VGA_WIDTH * VGA_HEIGHT; i++)
                WriteVideoMemoryChar(' ');
            offset = VIDEO_MEMORY;
        }

        public static void WriteVideoMemoryChar(int c, ushort effect = 0x0f00)
        {
            CPU.WriteMemory((int)offset, (c & 255) | effect);
            offset += 2;
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
            if (value >= 10) WriteVideoMemoryChar(value + 55);
            else WriteVideoMemoryChar(value + 48);
        }
    }
}
