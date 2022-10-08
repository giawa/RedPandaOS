using CPUHelper;

namespace Kernel.Devices
{
    public static class PIT
    {
        public static void Init(int frequency)
        {
            int divisor = Runtime.Math32.Divide(1193180, frequency);
            Frequency = (uint)frequency;

            CPU.OutDxAl(0x43, 0x36);
            CPU.OutDxAl(0x40, (byte)divisor);
            CPU.OutDxAl(0x40, (byte)(divisor >> 8));
        }

        private static uint _tickCount = 0;

        public static uint TickCount { get { return _tickCount; } }

        public static uint Frequency { get; private set; }

        //private static Runtime.Collections.List<uint> _profiler;

        //public static bool Profile { get; set; }

        //public static Runtime.Collections.List<uint> Profiler { get { return _profiler; } }

        public static void Tick()
        {
            _tickCount += 1;

            Scheduler.Tick();

            /*if (Profile)
            {
                if (_profiler == null) _profiler = new Runtime.Collections.List<uint>(800);

                uint ebp = CPUHelper.CPU.ReadEBP();
                //for (uint i = 4; i < 128; i += 4)
                //    Logging.WriteLine(LogLevel.Warning, "{0:X} {1:X}", i, CPU.ReadMemInt(ebp + i));
                //Logging.WriteLine(LogLevel.Warning, "{0:X} {1:X}", 60, CPU.ReadMemInt(ebp + 60));
                /*Logging.WriteLine(LogLevel.Warning, "EBP {0:X}", CPUHelper.CPU.ReadEBP());
                Exceptions.PrintStackTrace();*/
                /*_profiler.Add(CPU.ReadMemInt(ebp + 60));
                //Logging.WriteLine(LogLevel.Warning, "{0:X} {1:X}", ebp, CPU.ReadMemInt(ebp + 60));

                //Exceptions.PrintStackTrace();
                //while (true) ;

                if (_profiler.Count > 795) Profile = false;
            }*/

            /*if ((_tickCount % 50) == 0)
            {
                Logging.WriteLine(LogLevel.Trace, "Tick {0}", _tickCount);
            }*/
        }
    }
}
