using CPUHelper;

namespace Kernel.Devices
{
    public static class PIT
    {
        public static void Init(int frequency)
        {
            int divisor = Runtime.Math32.Divide(1193180, frequency);

            CPU.OutDxAl(0x43, 0x36);
            CPU.OutDxAl(0x40, (byte)divisor);
            CPU.OutDxAl(0x40, (byte)(divisor >> 8));
        }

        private static uint _tickCount = 0;

        public static void Tick()
        {
            _tickCount += 1;

            /*if (Runtime.Math32.Modulo(_tickCount, 50) == 0)
            {
                VGA.WriteVideoMemoryString("Tick ");
                VGA.WriteHex(_tickCount);
                VGA.WriteLine();
            }*/
        }
    }
}
