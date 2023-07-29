namespace Emulator.CPU.x86
{
    public partial class CPU
    {
        private List<IIODevice> _peripherals = new List<IIODevice>();

        internal void InitPeripherals()
        {
            // COM1
            _peripherals.Add(new COM(0x3f8));

            // both PICs
            _peripherals.Add(new PIC(0x20));
            _peripherals.Add(new PIC(0xA0));
        }

        private void Out()
        {
            if (_peripherals.Count == 0) InitPeripherals();

            ushort dx = (ushort)(RDX & 0xffff);
            byte al = (byte)(RAX & 0xff);
            bool foundDevice = false;

            foreach (var device in _peripherals)
            {
                if (device.HasRegister(dx))
                {
                    foundDevice = true;
                    device.Out(dx, al);
                    break;
                }
            }

            if (!foundDevice) throw new NotImplementedException();
        }

        private void In()
        {
            if (_peripherals.Count == 0) InitPeripherals();

            ushort dx = (ushort)(RDX & 0xffff);
            bool foundDevice = false;

            foreach (var device in _peripherals)
            {
                if (device.HasRegister(dx))
                {
                    foundDevice = true;
                    _registers[0] &= ~0xffUL;
                    _registers[0] |= device.In(dx);
                    break;
                }
            }

            if (!foundDevice) throw new NotImplementedException();
        }
    }

    public interface IIODevice
    {
        bool HasRegister(ushort dx);
        void Out(ushort dx, byte al);
        byte In(ushort dx);
    }

    public class PIC : IIODevice
    {
        private int _baseAddr = 0;
        private byte[] _registers = new byte[2];

        public PIC(int baseAddr)
        {
            _baseAddr = baseAddr;

            //if (_baseAddr == 0x20) _irqs[0] = true; // set PIT by default
        }

        public void Out(ushort dx, byte al)
        {
            _registers[dx - _baseAddr] = al;
        }

        public byte In(ushort dx)
        {
            return 0;
        }

        public bool HasRegister(ushort dx)
        {
            if (dx < _baseAddr) return false;
            if (dx >= _baseAddr + _registers.Length) return false;

            return true;
        }

        public bool Interrupted
        {
            get
            {
                foreach (var irq in _irqs) if (irq) return true;
                return false;
            }
        }

        private bool[] _irqs = new bool[8];

        public int FirstInterrupt()
        {
            int offset = (_baseAddr == 0x20 ? 0x20 : 0x28);

            for (int i = 0; i < _irqs.Length; i++) if (_irqs[i]) return i + offset;
            return -1;
        }

        public void Interrupt(int interruptNumber)
        {
            _irqs[interruptNumber] = true;
        }
    }

    public class COM : IIODevice
    {
        private int _baseAddr = 0;
        private byte[] _registers = new byte[5];

        public COM(int baseAddr)
        {
            _baseAddr = baseAddr;
        }

        private byte _pendingIn = 0;

        public void Out(ushort dx, byte al)
        {
            if (dx == _baseAddr && (_registers[3] & 0x80) == 0) // DLAB disabled, then this is the data register
            {
                if (_registers[4] == 0x1E)  // loopback mode
                    _pendingIn = al;
                else Console.Write((char)al);
            }

            _registers[dx - _baseAddr] = al;
        }

        public byte In(ushort dx)
        {
            if (dx == _baseAddr) return _pendingIn;
            else if (dx == _baseAddr + 4) return 0; // never busy
            else throw new NotImplementedException();
        }

        public bool HasRegister(ushort dx)
        {
            if (dx < _baseAddr) return false;
            if (dx >= _baseAddr + _registers.Length) return false;

            return true;
        }
    }
}
