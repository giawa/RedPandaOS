

namespace Emulator
{
    class Program
    {
        public static void Main()
        {
            var disk = File.ReadAllBytes("E:\\Git\\giawaos\\GiawaOS\\RedPandaOS\\bin\\Debug\\netcoreapp3.1\\disk.bin");

            // the BIOS will load the first 512 bytes into location 0x7C00
            byte[] memory = new byte[0x80000];
            Array.Copy(disk, 0, memory, 0x7C00, 512);

            CPU.x86.CPU cpu = new CPU.x86.CPU();
            cpu.LoadProgram(memory);
            cpu.Jump(0x7C00);

            while (true)
            {
                cpu.Tick();
            }
        }
    }
}