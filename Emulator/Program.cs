

namespace Emulator
{
    class Program
    {
        public static void Main()
        {
            var disk = File.ReadAllBytes("E:\\Git\\giawaos\\GiawaOS\\RedPandaOS\\bin\\Debug\\netcoreapp3.1\\disk.bin");

            CPU.x86.CPU cpu = new CPU.x86.CPU();
            cpu.LoadDisk(disk);
            cpu.InitBios();

            while (true)
            {
                cpu.Tick();
            }
        }
    }
}