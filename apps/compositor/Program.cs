using IL2Asm.BaseTypes;
using Runtime;

namespace compositor
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //uint width = 1280, height = 720;
            _firstBuffer = Syscalls.InitBGASysCall(_width, _height);
            _secondBuffer = _firstBuffer + _width * _height * 4;
            _currentBuffer = false;

            string uname = "Compositor!";
            uint i = 0;

            for (i = 0; i < (uint)uname.Length; i++)
                Syscalls.WriteCharToStdOutSysCall(uname[(int)i]);

            Syscalls.MapMemorySysCall(_firstBuffer, _width * _height * 4 * 2);  // 32b, x2 for double buffering

            i = 100;
            while (true)
            {
                DrawBackground();
                //WriteFast(frameBufferAddress, 0x00004a7f, width * height);

                //WriteFast(_framebuffer, 0x00004a7f, _width * _height);

                DrawWindow(i, i, 400, 250);
                i = i + 2;
                if (i > 200) i = 100;

                if (_currentBuffer) Syscalls.SetBGAYOffsetSysCall(_height);
                else Syscalls.SetBGAYOffsetSysCall(0);

                _currentBuffer = !_currentBuffer;

                for (uint j = 0; j < 10000000; j++) ;
            }

            while (true) ;
        }

        private static uint _firstBuffer, _secondBuffer;

        private static bool _currentBuffer;

        private static void DrawBackground()
        {
            uint end = _width * _height;
            uint activeBuffer = (_currentBuffer ? _secondBuffer : _firstBuffer);

            uint[] framebuffer = Runtime.Memory.Utilities.PtrToObject<uint[]>(activeBuffer - 8);
            for (uint i = 0; i < end; i++) framebuffer[i] = 0x00004a7f;
        }

        private static uint _width = 1280, _height = 720;

        private static void DrawWindow(uint x, uint y, uint width, uint height)
        {
            uint activeBuffer = (_currentBuffer ? _secondBuffer : _firstBuffer);

            // height should be 28 pixels higher than the content
            // width should be 12 pixels wider than the content
            uint framebuffer = activeBuffer + _width * y * 4 + x * 4;
            uint i; // drop on stack a single time to be more likely to get picked up by a hardware register

            // draw the title bar first
            WriteFast(framebuffer, 0, width);
            framebuffer += _width * 4;

            for (i = 0; i < 20; i++)
            {
                DrawHorizontalWithBevel(framebuffer, width);
                framebuffer += _width * 4;
            }

            DrawLeftEdge(framebuffer);
            WriteFast(framebuffer + 24, 0, width - 12);
            DrawLeftEdge(framebuffer + (width - 6) * 4);
            framebuffer += _width * 4;

            for (i = 20; i < height - 22 - 6; i++) // 22 for top bar, 6 for bottom bezel
            {
                DrawLeftEdge(framebuffer);
                WriteFast(framebuffer + 24, 0x00ffffff, width - 12);    // this is the actual window content
                DrawLeftEdge(framebuffer + (width - 6) * 4);
                framebuffer += _width * 4;
            }

            DrawLeftEdge(framebuffer);
            WriteFast(framebuffer + 24, 0, width - 12);
            DrawLeftEdge(framebuffer + (width - 6) * 4);
            framebuffer += _width * 4;

            for (i = 0; i < 4; i++)
            {
                DrawHorizontalWithBevel(framebuffer, width);
                framebuffer += _width * 4;
            }

            WriteFast(framebuffer, 0, width);

            //WriteFast()
        }

        private static void DrawHorizontalWithBevel(uint address, uint width)
        {
            CPUHelper.CPU.WriteMemInt(address, 0);
            WriteFast(address + 4, 0x00CCCCCC, width - 2);
            CPUHelper.CPU.WriteMemInt(address + width * 4 - 4, 0);
        }

        private static void DrawLeftEdge(uint address)
        {
            CPUHelper.CPU.WriteMemInt(address, 0);
            WriteFast(address + 4, 0x00CCCCCC, 4);
            CPUHelper.CPU.WriteMemInt(address + 20, 0);
        }

        [AsmMethod]
        private static void WriteFast(uint address, uint value, uint count)
        {
            uint[] buffer = Runtime.Memory.Utilities.PtrToObject<uint[]>(address - 8);
            for (uint i = 0; i < count; i++) buffer[i] = value;
        }

        [AsmPlug("compositor_Program_WriteFast_Void_U4_U4_U4", Architecture.X86)]
        private static void WriteFastAsm(IAssembledMethod assembly)
        {
            assembly.AddAsm("pop ecx");
            assembly.AddAsm("pop eax");
            assembly.AddAsm("pop edi");
            assembly.AddAsm("rep stosd");
        }
    }
}