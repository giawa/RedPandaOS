namespace Kernel.Devices
{
    public static class Keyboard
    {
        public static void OnKeyPress()
        {
            var status = CPUHelper.CPU.InDxByte(0x64);

            if ((status & 0x01) == 1)
            {
                var keycode = CPUHelper.CPU.InDxByte(0x60);

                if ((keycode & 0x80) != 0)
                {
                    // key is being released
                    if ((keycode & 0x7f) == 0x2a) _lshift = false;
                    else if ((keycode & 0x7f) == 0x36) _rshift = false;
                }
                else
                {
                    if (keycode == 0x2a) _lshift = true;
                    else if (keycode == 0x36) _rshift = true;
                    else if (keycode == 0x0e)
                    {
                        VGA.Delete();
                        VGA.WriteChar(' ');
                        VGA.Delete();
                    }
                    else if (keycode == 0x1c) VGA.WriteLine();
                    else VGA.WriteChar(ScanCodeToASCII(keycode));
                }
            }
            else
            {
                VGA.WriteString("Status was: 0x");
                VGA.WriteHex(status);
                VGA.WriteLine();
            }
        }

        private static bool _lshift = false, _rshift = false;
        private static bool Shift
        {
            get { return _lshift || _rshift; }
        }

        // scan codes for set 1 (and X(Set 2), X(Set 3))
        // see https://www.win.tue.nl/~aeb/linux/kbd/scancodes-10.html#translationtable
        private const string lowercase = "??1234567890-=??qwertyuiop[]??asdfghjkl;'`??zxcvbnm,./";
        private const string uppercase = "??!@#$%^&*()_+??QWERTYUIOP{}??ASDFGHJKL:\"~??ZXCVBNM<>?";

        private static char ScanCodeToASCII(byte c)
        {
            if (c == 0x39) return ' ';
            else if (c > 0x35) return '?';

            return Shift ? uppercase[c] : lowercase[c];
        }
    }
}
