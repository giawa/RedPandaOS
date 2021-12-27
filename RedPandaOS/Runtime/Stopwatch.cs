namespace Runtime
{
    public class Stopwatch
    {
        private uint startHigh, startLow;
        private uint stopHigh, stopLow;

        public static Stopwatch StartNew()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            return stopwatch;
        }

        public void Start()
        {
            startLow = CPUHelper.CPU.Rdtsc();
            startHigh = CPUHelper.CPU.ReadEDX();

            stopLow = startLow;
            stopHigh = startHigh;
        }

        public void Stop()
        {
            stopLow = CPUHelper.CPU.Rdtsc();
            stopHigh = CPUHelper.CPU.ReadEDX();
        }

        public uint ElapsedTicks
        {
            get
            {
                // to give us some extra room we'll knock 8 bits off the tick count
                var start = (startHigh & 0xff) << 24;
                start |= (startLow >> 8) & 0xffffff;

                var stop = (stopHigh & 0xff) << 24;
                stop |= (stopLow >> 8) & 0xffffff;

                return stop - start;
            }
        }
    }
}