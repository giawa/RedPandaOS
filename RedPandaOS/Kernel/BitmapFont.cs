namespace Kernel
{
    public class BitmapFont
    {
        public struct Character
        {
            public byte Char;
            public byte X;
            public byte Y;
            public byte Width;
            public byte Height;
            public sbyte XOffset;
            public sbyte YOffset;
            public byte Advance;
        }

        private Character[] _data;
        private byte[] _texture;
        private uint _height, _width;
        private uint tHeight = 0;

        public BitmapFont(Character[] data, byte[] fontTexture, uint width, uint height)
        {
            _data = data;
            _texture = fontTexture;
            _width = width;
            _height = height;
        }

        private void DrawChar(int cAddress, uint x, uint y, uint framebuffer, uint frameWidth, uint frameHeight)
        {
            x = (uint)((int)x + _data[cAddress].XOffset);
            y = (uint)((int)y + _data[cAddress].YOffset);
            var cHeight = _data[cAddress].Height;
            var cWidth = _data[cAddress].Width;
            var tX = _data[cAddress].X;
            var tY = _data[cAddress].Y;

            for (uint _y = 0; _y < cHeight; _y++)
            {
                for (uint _x = 0; _x < cWidth; _x++)
                {
                    uint address = framebuffer + (_y + y) * frameWidth * 4 + (_x + x) * 4;

                    byte color = _texture[_x + tX + (_y + tY) * _width];
                    //uint pixel = (color << 16) | (color << 8) | color;

                    var existing = CPUHelper.CPU.ReadMemInt(address);
                    CPUHelper.CPU.WriteMemory(address, Lerp(existing, 0x00aaccffU, color));
                }
            }
        }

        private uint Lerp(uint back, uint front, byte amount)
        {
            byte r1 = (byte)(back >> 16);
            byte g1 = (byte)(back >> 8);
            byte b1 = (byte)(back);

            byte r2 = (byte)(front >> 16);
            byte g2 = (byte)(front >> 8);
            byte b2 = (byte)(front);

            byte r = (byte)((r1 * (256U - amount) + (uint)r2 * amount) >> 8);
            byte g = (byte)((g1 * (256U - amount) + (uint)g2 * amount) >> 8);
            byte b = (byte)((b1 * (256U - amount) + (uint)b2 * amount) >> 8);

            return ((uint)r << 16) | ((uint)g << 8) | b;
        }

        public void DrawString(string s, uint x, uint y, uint framebuffer, uint frameWidth, uint frameHeight)
        {
            if (framebuffer != 0)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    for (int j = 0; j < _data.Length; j++)
                    {
                        if (_data[j].Char == (byte)s[i])
                        {
                            DrawChar(j, x, y, framebuffer, frameWidth, frameHeight);
                            x += _data[j].Advance;
                        }
                    }
                }
            }
        }
    }
}
