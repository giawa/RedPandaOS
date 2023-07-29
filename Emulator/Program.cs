

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

    class MemoryByte
    {
        private byte[] _memory;

        public MemoryByte(int size)
        {
            _memory = new byte[size];
        }

        public byte Get(int address)
        {
            var value = _memory[address];
            return _memory[address];
        }

        public void Set(int address, byte value)
        {
            _memory[address] = value;
        }

        private void MyFunction()
        {
            DoSomething(4, 5);
            Console.WriteLine("Made it!");
        }

        private int DoSomething(int a, int b)
        {
            return a + b;
        }
    }
}