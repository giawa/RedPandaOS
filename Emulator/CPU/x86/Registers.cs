using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Emulator.CPU.x86
{
    public partial class CPU
    {
        //private ulong rax, rcx, rdx, rbx, rsp, rbp, rsi, rdi;
        //public ushort es, cs, ss, ds, fs, gs;
        private ulong[] _registers = new ulong[8] { 1, 2, 3, 4, 5, 6, 7, 8 };
        private ushort[] _segmentRegisters = new ushort[6];

        public ulong RAX { get { return _registers[0]; } }
        public ulong RCX { get { return _registers[1]; } }
        public ulong RDX { get { return _registers[2]; } set { _registers[2] = value; } }
        public ulong RBX { get { return _registers[3]; } }
        public ulong RSP { get { return _registers[4]; } }
        public ulong RBP { get { return _registers[5]; } }
        public ulong RSI { get { return _registers[6]; } }
        public ulong RDI { get { return _registers[7]; } }

        public ushort ES { get { return _segmentRegisters[0]; } }
        public ushort CS { get { return _segmentRegisters[1]; } }
        public ushort SS { get { return _segmentRegisters[2]; } }
        public ushort DS { get { return _segmentRegisters[3]; } }
        public ushort FS { get { return _segmentRegisters[4]; } }
        public ushort GS { get { return _segmentRegisters[5]; } }

        public void SetReg8(byte reg, byte value)
        {
            if (reg < 4) _registers[reg] = (_registers[reg] & ~0xffUL) | value;
            else throw new Exception("Unsure what to do");
        }

        public void SetReg16(byte reg, ushort value)
        {
            _registers[reg] = (_registers[reg] & ~0xffffUL) | value;
        }

        public byte GetReg8(byte reg)
        {
            if (reg < 4) return (byte)_registers[reg];
            else throw new Exception("Unsure what to do");
        }

        public ushort GetReg16(byte reg)
        {
            return (ushort)_registers[reg];
        }

        /*public byte AL
        {
            get { return (byte)(rax & 0xff); }
            set { rax = (rax & ~0xffUL) | value; }
        }

        public byte CL
        {
            get { return (byte)(rcx & 0xff); }
            set { rcx = (rcx & ~0xffUL) | value; }
        }

        public byte DL
        {
            get { return (byte)(rdx & 0xff); }
            set { rdx = (rdx & ~0xffUL) | value; }
        }

        public byte BL
        {
            get { return (byte)(rbx & 0xff); }
            set { rbx = (rbx & ~0xffUL) | value; }
        }

        public byte AH
        {
            get { return (byte)((rax >> 8) & 0xff); }
            set { rax = (rax & ~0xff00UL) | ((ulong)value << 8); }
        }

        public byte CH
        {
            get { return (byte)((rcx >> 8) & 0xff); }
            set { rcx = (rcx & ~0xff00UL) | ((ulong)value << 8); }
        }

        public byte DH
        {
            get { return (byte)((rdx >> 8) & 0xff); }
            set { rdx = (rdx & ~0xff00UL) | ((ulong)value << 8); }
        }

        public byte BH
        {
            get { return (byte)((rbx >> 8) & 0xff); }
            set { rbx = (rbx & ~0xff00UL) | ((ulong)value << 8); }
        }

        public ushort AX
        {
            get { return (ushort)(rax & 0xffff); }
            set { rax = (rax & ~0xffffUL) | value; }
        }

        public ushort CX
        {
            get { return (ushort)(rcx & 0xffff); }
            set { rcx = (rcx & ~0xffffUL) | value; }
        }

        public ushort DX
        {
            get { return (ushort)(rdx & 0xffff); }
            set { rdx = (rdx & ~0xffffUL) | value; }
        }

        public ushort BX
        {
            get { return (ushort)(rbx & 0xffff); }
            set { rbx = (rbx & ~0xffffUL) | value; }
        }

        public ushort SP
        {
            get { return (ushort)(rsp & 0xffff); }
            set { rsp = (rsp & ~0xffffUL) | value; }
        }

        public ushort BP
        {
            get { return (ushort)(rbp & 0xffff); }
            set { rbp = (rbp & ~0xffffUL) | value; }
        }

        public ushort SI
        {
            get { return (ushort)(rsi & 0xffff); }
            set { rsi = (rsi & ~0xffffUL) | value; }
        }

        public ushort DI
        {
            get { return (ushort)(rdi & 0xffff); }
            set { rdi = (rdi & ~0xffffUL) | value; }
        }*/
    }
}
