using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace Emulator.CPU.x86
{
    public enum Mode : uint
    {
        RealMode,
        ProtectedMode,
        LongMode
    }

    [Flags]
    public enum RFLAGS
    {
        CF = (1 << 0),
        PF = (1 << 2),
        AF = (1 << 4),
        ZF = (1 << 6),
        SF = (1 << 7),
        RF = (1 << 8),
        IF = (1 << 9),
        DF = (1 << 10),
        OF = (1 << 11)
    }

    public enum Segment
    {
        Code,
        Data,
        Stack,
        Extra
    }

    public partial class CPU
    {
        public Mode Mode { get; private set; } = Mode.RealMode;

        private ulong IP;
        private byte[] _memory;
        private byte[] _disk;

        public RFLAGS FLAGS = 0;

        private bool _interruptsEnabled = true;

        public void LoadDisk(byte[] disk)
        {
            _disk = disk;
        }

        public void InitBios()
        {
            // the BIOS will load the first 512 bytes into location 0x7C00
            byte[] memory = new byte[0x4000000];    // 64MB of RAM
            Array.Copy(_disk, 0, memory, 0x7C00, 512);
            _memory = memory;

            Jump(0x7C00);
            RDX = 0x80; // BIOS sets disk in DX on boot
        }

        public void Jump(ulong ip)
        {
            IP = ip;
        }

        public void Tick()
        {
            var opcode = _memory[IP++];

            Segment currentSegment = Segment.Data;
            if (currentSegment == Segment.Data && opcode == 0x26)
            {
                currentSegment = Segment.Extra;
                opcode = _memory[IP++];
            }

            Mode currentMode = this.Mode;
            if (opcode == 0x66)
            {
                switch (this.Mode)
                {
                    case Mode.RealMode:
                        currentMode = Mode.ProtectedMode;
                        break;
                    case Mode.ProtectedMode:
                        currentMode = Mode.RealMode;
                        break;
                    default:
                        throw new NotImplementedException();
                }

                opcode = _memory[IP++];
            }

            switch (opcode)
            {
                case 0x01:  // ADD
                    if (currentMode == Mode.RealMode) DoAction16WithModRM(CommonAction.ADD);
                    else if (currentMode == Mode.ProtectedMode) DoAction32WithModRM(CommonAction.ADD);
                    else throw new NotImplementedException();
                    break;

                case 0x06:  // PUSH ES
                    if (currentMode == Mode.RealMode) Push16(ES);
                    else throw new NotImplementedException();
                    break;

                case 0x07:  // POP ES
                    if (currentMode == Mode.RealMode)
                    {
                        _segmentRegisters[0] = BitConverter.ToUInt16(_memory, (int)RSP);
                        _registers[4] += 2;
                    }
                    else throw new Exception("Unsupported mode");
                    break;

                case 0x09:
                    if (currentMode == Mode.RealMode) DoAction16WithModRM(CommonAction.OR);
                    else throw new NotImplementedException();
                    break;

                case 0x0F:
                    SecondaryOpcode(currentMode);
                    break;

                case 0x25:  // AND rAX Ivds
                    if (currentMode == Mode.LongMode) throw new Exception("TODO: Must sign extend a 32 bit value");
                    _registers[0] = _registers[0] & ReadWordFromIP(currentMode);
                    break;

                case 0x29:  // SUB
                    if (currentMode == Mode.RealMode) DoAction16WithModRM(CommonAction.SUB);
                    else throw new NotImplementedException();
                    break;

                case 0x31:  // XOR Evqp Gvqp
                    if (currentMode == Mode.RealMode) DoAction16WithModRM(CommonAction.XOR);   // xor destination (register or memory) with the contents of a register and store in destination
                    else if (currentMode == Mode.ProtectedMode) DoAction32WithModRM(CommonAction.XOR);
                    else throw new NotImplementedException();
                    break;

                case 0x39:
                    if (currentMode == Mode.RealMode) DoAction16WithModRM(CommonAction.CMP);
                    else if (currentMode == Mode.ProtectedMode) DoAction32WithModRM(CommonAction.CMP);
                    else throw new NotImplementedException();
                    break;

                case 0x3C:  // CMP AL
                    CMP((byte)RAX, _memory[IP++], false);
                    break;

                case 0x3D:  // CMP rAX Ivds
                    if (currentMode == Mode.RealMode) CMP((ushort)RAX, Read16FromIP(), false);
                    else throw new NotImplementedException();
                    break;

                // INC
                case 0x40:
                case 0x41:
                case 0x42:
                case 0x43:
                case 0x44:
                case 0x45:
                case 0x46:
                case 0x47:
                    if (currentMode == Mode.RealMode) _registers[opcode & 0x0f] = DoAction16(CommonAction.ADD, (ushort)_registers[opcode & 0x0f], 1);
                    else throw new NotImplementedException();
                    break;

                case 0x50:  // PUSH Zv
                case 0x51:
                case 0x52:
                case 0x53:
                case 0x54:
                case 0x55:
                case 0x56:
                case 0x57:
                    if (RSP > int.MaxValue) throw new Exception("64b mode unsupported");
                    if (currentMode == Mode.RealMode) Push16((ushort)_registers[opcode & 0x07]);
                    else if (currentMode == Mode.ProtectedMode) Push32((uint)_registers[opcode & 0x07]);
                    else throw new Exception("Unsupported mode");
                    break;

                case 0x58:  // POP Zv
                case 0x59:
                case 0x5A:
                case 0x5B:
                case 0x5C:
                case 0x5D:
                case 0x5E:
                case 0x5F:
                    if (RSP > int.MaxValue) throw new Exception("64b mode unsupported");
                    if (currentMode == Mode.RealMode) _registers[opcode & 0x07] = Pop16();
                    else if (currentMode == Mode.ProtectedMode) _registers[opcode & 0x07] = Pop32();
                    else throw new Exception("Unsupported mode");
                    break;

                case 0x60:  // PUSHA
                    if (currentMode == Mode.RealMode)
                    {
                        var originalRsp = (ushort)RSP;
                        Push16((ushort)RAX);
                        Push16((ushort)RCX);
                        Push16((ushort)RDX);
                        Push16((ushort)RBX);
                        Push16(originalRsp);
                        Push16((ushort)RBP);
                        Push16((ushort)RSI);
                        Push16((ushort)RDI);
                    }
                    else throw new Exception("Unsupported mode");
                    break;

                case 0x61:  // POPA
                    if (currentMode == Mode.RealMode)
                    {
                        _registers[7] = Pop16();
                        _registers[6] = Pop16();
                        _registers[5] = Pop16();
                        var rsp = Pop16();
                        _registers[3] = Pop16();
                        _registers[2] = Pop16();
                        _registers[1] = Pop16();
                        _registers[0] = Pop16();
                        _registers[4] = rsp;
                    }
                    else throw new Exception("Unsupported mode");
                    break;

                case 0x68:  // PUSH imm
                    if (currentMode == Mode.RealMode) Push16((ushort)Read16FromIP());
                    else if (currentMode == Mode.ProtectedMode) Push32((uint)Read32FromIP());
                    else throw new Exception("Unsupported mode");
                    break;

                case 0x6A:  // PUSH imm8
                    if (currentMode == Mode.RealMode) Push16((ushort)_memory[IP++]);
                    else if (currentMode == Mode.ProtectedMode) Push32((uint)_memory[IP++]);
                    else throw new NotImplementedException();
                    break;

                case 0x72:  // JC or JNAE Jbs
                    JMP(FLAGS.HasFlag(RFLAGS.CF), (sbyte)_memory[IP++]);
                    break;

                case 0x73:  // JNB or JNC Jbs
                    JMP(FLAGS.HasFlag(RFLAGS.CF) == false, (sbyte)_memory[IP++]);
                    break;

                case 0x74:  // JZ or JE Jbs
                    JMP(FLAGS.HasFlag(RFLAGS.ZF), (sbyte)_memory[IP++]);
                    break;

                case 0x75:  // JNZ or JNE Jbs
                    JMP(FLAGS.HasFlag(RFLAGS.ZF) == false, (sbyte)_memory[IP++]);
                    break;

                case 0x7C:  // JL or JNGE Gbs
                    JMP((FLAGS.HasFlag(RFLAGS.SF) != FLAGS.HasFlag(RFLAGS.OF)), (sbyte)_memory[IP++]);
                    break;

                case 0x7D:  // JGE or JNL Jbs
                    JMP((FLAGS.HasFlag(RFLAGS.SF) == FLAGS.HasFlag(RFLAGS.OF)), (sbyte)_memory[IP++]);
                    break;

                case 0x7E:  // JLE or JNG Jbs
                    JMP(FLAGS.HasFlag(RFLAGS.ZF) || (FLAGS.HasFlag(RFLAGS.SF) != FLAGS.HasFlag(RFLAGS.OF)), (sbyte)_memory[IP++]);
                    break;

                case 0x7F:  // JG or JNLE Jbs
                    JMP(FLAGS.HasFlag(RFLAGS.ZF) == false && (FLAGS.HasFlag(RFLAGS.SF) == FLAGS.HasFlag(RFLAGS.OF)), (sbyte)_memory[IP++]);
                    break;

                case 0x80:
                    CmpWithModRm();
                    break;

                case 0x83:  // XOR Evqp Ibs
                    if (currentMode == Mode.RealMode) DoAction16WithEvqp();
                    else if (currentMode == Mode.ProtectedMode) DoAction32WithEvqp();
                    else throw new NotImplementedException();
                    break;

                case 0x88:  // MOV
                    DoAction8WithModRM(CommonAction.MOV, swap: true);
                    break;

                case 0x89:  // MOV Evqp Gvqp
                    if (currentMode == Mode.RealMode) DoAction16WithModRM(CommonAction.MOV, swap: true);
                    else if (currentMode == Mode.ProtectedMode) DoAction32WithModRM(CommonAction.MOV, swap: true);
                    else throw new NotImplementedException();
                    break;

                case 0x8A:  // MOV reg8, reg/mem8
                    if (currentMode == Mode.RealMode) DoAction8WithModRM(CommonAction.MOV);
                    else throw new NotImplementedException();
                    break;

                case 0x8B:
                    if (currentMode == Mode.RealMode) DoAction16WithModRM(CommonAction.MOV);
                    else if (currentMode == Mode.ProtectedMode) DoAction32WithModRM(CommonAction.MOV);
                    break;

                case 0x8D:  // LEA
                    if (currentMode == Mode.RealMode) DoAction16WithModRM(CommonAction.LEA);
                    else if (currentMode == Mode.ProtectedMode) DoAction32WithModRM(CommonAction.LEA);
                    else throw new NotImplementedException();
                    break;

                case 0x8E:  // MOV Sw Ew
                    if (currentMode == Mode.RealMode || currentMode == Mode.ProtectedMode) DoAction16WithModRM(CommonAction.MOV, true);   // mov src (register or memory) into a segment register
                    break;

                case 0x9B:  // FWAIT
                    FPUOpcode();
                    break;

                case 0x9F:  // LAHF
                    if (currentMode == Mode.RealMode)
                    {
                        _registers[0] &= ~0xFF00UL; // clear AH
                        _registers[0] |= (((ulong)FLAGS) & 0xff) << 8;  // load the lower 8 bits of FLAGS into AH
                    }
                    else throw new NotImplementedException();
                    break;

                case 0xA1:  // MOV AX, imm
                    if (currentMode == Mode.RealMode)
                    {
                        var a1offset = Read16FromIP();
                        _registers[0] = BitConverter.ToUInt16(_memory, (int)a1offset);
                    }
                    else if (currentMode == Mode.ProtectedMode)
                    {
                        var a1offset = Read32FromIP();
                        _registers[0] = BitConverter.ToUInt32(_memory, (int)a1offset);
                    }
                    else throw new NotImplementedException();
                    break;

                case 0xA2:  // MOV [imm], AL
                    if (currentMode == Mode.RealMode) throw new NotImplementedException();
                    else if (currentMode == Mode.ProtectedMode)
                    {
                        uint addr = BitConverter.ToUInt32(_memory, (int)IP);
                        IP += 4;
                        _memory[addr] = (byte)(RAX & 0xff);
                    }
                    else throw new NotImplementedException();
                    break;

                // MOV [imm16], AX
                case 0xA3:
                    if (currentMode == Mode.RealMode)
                    {
                        var a3offset = Read16FromIP();
                        var a3bytes = BitConverter.GetBytes((ushort)_registers[0]);
                        Array.Copy(a3bytes, 0, _memory, (int)a3offset, a3bytes.Length);
                    }
                    else if (currentMode == Mode.ProtectedMode)
                    {
                        var a3offset = Read32FromIP();
                        var a3bytes = BitConverter.GetBytes((uint)_registers[0]);
                        Array.Copy(a3bytes, 0, _memory, (int)a3offset, a3bytes.Length);
                    }
                    else throw new NotImplementedException();
                    break;

                // MOV reg8, imm8
                case 0xB0:
                case 0xB1:
                case 0xB2:
                case 0xB3:
                    _registers[opcode & 0x03] &= ~0xffUL;
                    _registers[opcode & 0x03] |= _memory[IP++];
                    break;

                // MOV reg8H, imm8
                case 0xB4:
                case 0xB5:
                case 0xB6:
                case 0xB7:
                    _registers[opcode & 0x03] &= ~0xff00UL;
                    _registers[opcode & 0x03] |= ((ulong)_memory[IP++] << 8);
                    break;

                // MOV reg16, im16 or reg32, imm32 or reg64, imm64
                case 0xB8:
                case 0xB9:
                case 0xBA:
                case 0xBB:
                case 0xBC:
                case 0xBD:
                case 0xBE:
                case 0xBF:
                    _registers[opcode & 0x07] = ReadWordFromIP(currentMode);
                    break;

                case 0xC1:
                    if (currentMode == Mode.RealMode) DoRotation16WithEvqp(_memory[IP++], _memory[IP++]);
                    else throw new NotImplementedException();
                    break;

                // ret
                case 0xC2:
                    ushort toPop = BitConverter.ToUInt16(_memory, (int)IP);
                    IP += 2;
                    if (currentMode == Mode.RealMode)
                    {
                        IP = BitConverter.ToUInt16(_memory, (int)RSP);
                        _registers[4] += (ulong)(toPop + 2);   // +2 for the IP we just popped
                    }
                    else if (currentMode == Mode.ProtectedMode)
                    {
                        IP = BitConverter.ToUInt32(_memory, (int)RSP);
                        _registers[4] += (ulong)(toPop + 4);   // +4 for the IP we just popped
                    }
                    else throw new NotImplementedException();
                    break;

                // mov reg/mem16, imm16
                case 0xC7:
                    if (currentMode == Mode.RealMode) Mov16WithModRM(_memory[IP++]);//, (ushort)Read16FromIP());
                    else if (currentMode == Mode.ProtectedMode) Mov32WithModRM(_memory[IP++]);//, (ushort)Read16FromIP());
                    else throw new NotImplementedException();
                    break;

                // interrupt!
                case 0xCD:
                    byte interruptType = _memory[IP++];
                    if (currentMode == Mode.RealMode) BiosHandleInterrupt(interruptType);
                    else throw new NotImplementedException();
                    break;

                // shift Evqp
                case 0xD3:
                    if (currentMode == Mode.RealMode) DoRotation16WithEvqp(_memory[IP++]);
                    else throw new NotImplementedException();
                    break;

                case 0xE9:  // JMP imm
                    if (currentMode == Mode.RealMode) JMP(true, Read16SignedFromIP());
                    else throw new NotImplementedException();
                    break;

                case 0xEB:  // JMP imm
                    JMP(true, (sbyte)_memory[IP++]);
                    break;

                case 0xE8:  // CALL imm
                    if (currentMode == Mode.RealMode)
                    {
                        var callOffset = Read16SignedFromIP();
                        Push16((ushort)IP);

                        IP = (ulong)((long)IP + callOffset);
                    }
                    else if (currentMode == Mode.ProtectedMode)
                    {
                        var callOffset = Read32SignedFromIP();
                        Push32((uint)IP);

                        IP = (ulong)((long)IP + callOffset);
                    }
                    else throw new NotImplementedException();
                    break;

                case 0xEA:  // JMPF
                    ushort offsetea = (ushort)Read16FromIP();
                    ushort segmentea = (ushort)Read16FromIP();

                    if (_gdt.KernelCodeSegment.flags1 == 0)
                    {
                        ulong addrea = (ulong)(offsetea | (segmentea << 4));
                        IP = addrea;
                    }
                    else
                    {
                        IP = offsetea;
                    }
                    break;

                case 0xEC:  // IN
                    In();
                    break;

                case 0xEE:  // OUT
                    Out();
                    break;

                case 0xF7:
                    if (currentMode == Mode.RealMode) DoAction16WithModRM(CommonAction.MUL);
                    else throw new NotImplementedException();
                    break;

                case 0xFA:  // CLI
                    _interruptsEnabled = false;
                    break;

                case 0xFF:
                    if (currentMode == Mode.RealMode) DoFF16WithEvqp();
                    else throw new NotImplementedException();
                    break;

                default: throw new Exception(string.Format("Unknown opcode 0x{0:X}", opcode));
            }
        }

        public void Push16(ushort value)
        {
            var bytes = BitConverter.GetBytes(value);
            _registers[4] -= 2;
            Array.Copy(bytes, 0, _memory, (int)RSP, 2);
        }

        public void Push32(uint value)
        {
            var bytes = BitConverter.GetBytes(value);
            _registers[4] -= 4;
            Array.Copy(bytes, 0, _memory, (int)RSP, 4);
        }

        public ushort Pop16()
        {
            var temp = BitConverter.ToUInt16(_memory, (int)RSP);
            _registers[4] += 2;
            return temp;
        }

        public uint Pop32()
        {
            var temp = BitConverter.ToUInt32(_memory, (int)RSP);
            _registers[4] += 4;
            return temp;
        }

        public ulong[] Stack
        {
            get
            {
                List<ulong> stack = new List<ulong>();
                if (Mode == Mode.RealMode)
                {
                    int Count = Math.Min(100, (int)(0x9000 - RSP) / 2);
                    for (int i = 0; i < Count; i++) stack.Add(BitConverter.ToUInt16(_memory, (int)RSP + i * 2));
                }
                else if (Mode == Mode.ProtectedMode)
                {
                    int Count = Math.Min(100, (int)(0x9000 - RSP) / 4);
                    for (int i = 0; i < Count; i++) stack.Add(BitConverter.ToUInt32(_memory, (int)RSP + i * 4));
                }
                else throw new NotImplementedException();
                return stack.ToArray();
            }
        }

        public ulong[] GDTContents
        {
            get
            {
                List<ulong> stack = new List<ulong>();
                if (Mode == Mode.RealMode)
                {
                    int Count = 24;// Math.Min(100, (int)(0x9000 - RSP) / 2);
                    for (int i = 0; i < Count; i++) stack.Add(_memory[0x9601 + i]);
                }
                else throw new NotImplementedException();
                return stack.ToArray();
            }
        }

        private void FPUOpcode()
        {
            var fpuOpcode = _memory[IP++];

            switch (fpuOpcode)
            {
                case 0xDB:
                    var modrmDB = _memory[IP++];
                    if (modrmDB == 0xE3) break;     // FINIT
                    else throw new NotImplementedException();
                default:
                    throw new NotImplementedException();
            }
        }

        private void SecondaryOpcode(Mode currentMode)
        {
            var secondaryOpcode = _memory[IP++];

            switch (secondaryOpcode)
            {
                case 0x01:  // memory and virtual machine stuff
                    var modrm01 = _memory[IP++];
                    var opcode01 = ((modrm01 & 0x38) >> 3);

                    if (opcode01 == 2)  // LGDT
                    {
                        var lgdtPtr = Marshal.AllocHGlobal(Marshal.SizeOf<GDT>());
                        try
                        {
                            // IP contains the address of the lgdt object, which is a u16, u16
                            var addr = BitConverter.ToUInt16(_memory, (int)IP);
                            IP += 2;

                            // grab the first u16, which is the size
                            var size = BitConverter.ToUInt16(_memory, addr);
                            // then grab the next u16, which is the actual address of the GDT object
                            addr = BitConverter.ToUInt16(_memory, addr + 2);

                            // copy the contents of the GDT data to the _gdt structure
                            Marshal.Copy(_memory, addr, lgdtPtr, Marshal.SizeOf<GDT>());
                            _gdt = Marshal.PtrToStructure<GDT>(lgdtPtr);
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(lgdtPtr);
                        }
                    }
                    else throw new NotImplementedException();

                    break;

                case 0x20:  // move 32b control register to a 32b register
                    var modrm20 = _memory[IP++];
                    var reg20 = (modrm20 & 0x38) >> 3;
                    var rm20 = (modrm20 & 0x07);

                    _registers[rm20] = _controlRegisters[reg20];

                    break;

                case 0x22:  // move 32b register to a 32b control register
                    var modrm22 = _memory[IP++];
                    var reg22 = (modrm22 & 0x38) >> 3;
                    var rm22 = (modrm22 & 0x07);

                    _controlRegisters[reg22] = _registers[rm22];

                    if ((_controlRegisters[0] & 1) == 1) Mode = Mode.ProtectedMode;
                    else Mode = Mode.RealMode;

                    break;

                case 0x82:  // JC or JNAE Jbs
                    if (currentMode == Mode.RealMode) JMP(FLAGS.HasFlag(RFLAGS.CF), Read16SignedFromIP());
                    else throw new NotImplementedException();
                    break;

                case 0x83:  // JNB or JNC Jbs
                    if (currentMode == Mode.RealMode) JMP(FLAGS.HasFlag(RFLAGS.CF) == false, Read16SignedFromIP());
                    else throw new NotImplementedException();
                    break;

                case 0x84:  // JZ or JE Jbs
                    if (currentMode == Mode.RealMode) JMP(FLAGS.HasFlag(RFLAGS.ZF), Read16SignedFromIP());
                    else throw new NotImplementedException();
                    break;

                case 0x85:  // JNZ or JNE Jbs
                    if (currentMode == Mode.RealMode) JMP(FLAGS.HasFlag(RFLAGS.ZF) == false, Read16SignedFromIP());
                    else throw new NotImplementedException();
                    break;

                case 0x8C:  // JL or JNGE Gbs
                    if (currentMode == Mode.RealMode) JMP((FLAGS.HasFlag(RFLAGS.SF) != FLAGS.HasFlag(RFLAGS.OF)), Read16SignedFromIP());
                    else throw new NotImplementedException();
                    break;

                case 0x8D:  // JGE or JNL Jbs
                    if (currentMode == Mode.RealMode) JMP((FLAGS.HasFlag(RFLAGS.SF) == FLAGS.HasFlag(RFLAGS.OF)), Read16SignedFromIP());
                    else throw new NotImplementedException();
                    break;

                case 0x8E:  // JLE or JNG Jbs
                    if (currentMode == Mode.RealMode) JMP(FLAGS.HasFlag(RFLAGS.ZF) || (FLAGS.HasFlag(RFLAGS.SF) != FLAGS.HasFlag(RFLAGS.OF)), Read16SignedFromIP());
                    else throw new NotImplementedException();
                    break;

                case 0x8F:  // JG or JNLE Jbs
                    if (currentMode == Mode.RealMode) JMP(FLAGS.HasFlag(RFLAGS.ZF) == false && (FLAGS.HasFlag(RFLAGS.SF) == FLAGS.HasFlag(RFLAGS.OF)), Read16SignedFromIP());
                    else throw new NotImplementedException();
                    break;

                case 0xB6:  // MOVZX Gvqp Eb
                    if (currentMode == Mode.RealMode) DoAction16WithModRM(CommonAction.MOVZX8);
                    else if (currentMode == Mode.ProtectedMode) DoAction32WithModRM(CommonAction.MOVZX8);
                    else throw new NotImplementedException();
                    break;

                case 0xB7:  // MOVZX Gvqp Eb
                    if (currentMode == Mode.ProtectedMode) DoAction32WithModRM(CommonAction.MOVZX16);
                    else throw new NotImplementedException();
                    break;

                default: throw new Exception(string.Format("Unknown opcode 0x{0:X}", secondaryOpcode));
            }
        }

        private short Read16SignedFromIP()
        {
            short temp = BitConverter.ToInt16(_memory, (int)IP);
            IP += 2;
            return temp;
        }

        private ulong Read16FromIP()
        {
            ulong temp = BitConverter.ToUInt16(_memory, (int)IP);
            IP += 2;
            return temp;
        }

        private int Read32SignedFromIP()
        {
            int temp = BitConverter.ToInt32(_memory, (int)IP);
            IP += 4;
            return temp;
        }

        private ulong Read32FromIP()
        {
            ulong temp = BitConverter.ToUInt32(_memory, (int)IP);
            IP += 4;
            return temp;
        }

        private ulong ReadWordFromIP(Mode mode)
        {
            if (mode == Mode.RealMode) return Read16FromIP();
            else if (mode == Mode.ProtectedMode) return Read32FromIP();
            else throw new Exception("Unsupported word size");
        }

        private void JMP(bool doJump, long offset)
        {
            if (doJump) IP = (ulong)((long)IP + offset);
        }

        private void CMP(ulong dest, ulong src, bool signed)
        {
            if (signed)
            {
                throw new Exception("SF must be implemented first");

                FLAGS &= ~(RFLAGS.CF | RFLAGS.ZF);

                if (dest > src && FLAGS.HasFlag(RFLAGS.SF)) FLAGS |= RFLAGS.OF;
                else if (dest == src) FLAGS |= RFLAGS.ZF;
                else if (dest < src && !FLAGS.HasFlag(RFLAGS.SF)) FLAGS |= RFLAGS.OF;
            }
            else
            {
                FLAGS &= ~(RFLAGS.CF | RFLAGS.ZF | RFLAGS.SF | RFLAGS.OF);

                if ((long)dest - (long)src < 0) FLAGS |= RFLAGS.SF;
                if (dest + src > ushort.MaxValue) FLAGS |= RFLAGS.OF;

                if (dest == src) FLAGS |= RFLAGS.ZF;
                else if (dest < src) FLAGS |= RFLAGS.CF;
            }
        }

        private enum CommonAction
        {
            // note these are in order of 'o' opcode field for instructions 0x80, 0x81, 0x82, 0x83
            ADD = 0,
            OR = 1,
            ADC = 2,
            SBB = 3,
            AND = 4,
            SUB = 5,
            XOR = 6,
            CMP = 7,
            MOV,
            MOVZX8,
            MOVZX16,
            MUL,
            LEA,
        }

        private enum FFAction
        {
            INC = 0,
            DEC = 1,
            CALL = 2,
            CALLF = 3,
            JMP = 4,
            JMPF = 5,
            PUSH = 6,
        }

        private enum RotationAction
        {
            // note these are in order of 'o' opcode field for instructions 0xC0, 0xC1, 0xD0, 0xD1, 0xD2, 0xD3
            ROL = 0,
            ROR = 1,
            RCL = 2,
            RCR = 3,
            SHL = 4,
            SHR = 5,
            SAL = 6,
            SAR = 7
        }

        private enum DestSize
        {
            R8,
            R16,
            R32,
            R64,
            MMX,
            XMM,
            YMM,
            SREG,
            CREG,
            DREG
        }

        private void DoFF16WithEvqp()
        {
            var modrm = _memory[IP++];
            var mod = (modrm & 0xC0) >> 6;  // dest in EvGv
            var action = (FFAction)((modrm & 0x38) >> 3);
            var rm = (modrm & 0x07);

            if (mod == 3)
            {
                switch (action)
                {
                    /*case RotationAction.SHR:
                        _registers[rm] |= (ushort)(initial >> (byte)(_registers[1] & 0xff));
                        FLAGS &= ~RFLAGS.CF;
                        if (initial >> (byte)((_registers[1] & 0xff) - 1) != 0) FLAGS |= RFLAGS.CF;
                        break;*/
                    case FFAction.JMP:
                        if (mod == 3)
                        {
                            IP = _registers[rm];
                        }
                        else throw new NotImplementedException();
                        break;

                    default: throw new NotImplementedException();
                }
            }
            else throw new Exception("Unsupported modrm");
        }

        private void DoRotation16WithEvqp(byte modrm, int amount = -1)
        {
            var mod = (modrm & 0xC0) >> 6;  // dest in EvGv
            var action = (RotationAction)((modrm & 0x38) >> 3);
            var rm = (modrm & 0x07);

            if (amount == -1) amount = (int)(_registers[1] & 0xff);

            if (mod == 3)
            {
                ushort initial = (ushort)_registers[rm];
                _registers[rm] &= ~0xffffUL;
                switch (action)
                {
                    case RotationAction.SHR:
                        _registers[rm] |= (ushort)(initial >> amount);
                        FLAGS &= ~RFLAGS.CF;
                        if (((initial >> (byte)(amount - 1)) & 0x0001) != 0) FLAGS |= RFLAGS.CF;
                        break;

                    case RotationAction.SHL:
                        _registers[rm] |= (ushort)(initial << amount);
                        FLAGS &= ~RFLAGS.CF;
                        if (((initial << (byte)(amount - 1)) & 0x8000) != 0) FLAGS |= RFLAGS.CF;
                        break;

                    default: throw new NotImplementedException();
                }
            }
            else throw new Exception("Unsupported modrm");
        }

        private void CmpWithModRm()
        {
            var modrm = _memory[IP++];
            var mod = (modrm & 0xC0) >> 6;  // dest in EvGv
            //var action = (CommonAction)((modrm & 0x38) >> 3);
            var rm = (modrm & 0x07);

            if (mod == 3)
            {
                if (rm < 4) CMP((byte)_registers[rm], _memory[IP++], false);
                else CMP((byte)(_registers[rm - 4] >> 8), _memory[IP++], false);
            }
            else throw new Exception("Unsupported modrm");
        }

        private void DoAction32WithEvqp()
        {
            var modrm = _memory[IP++];
            var mod = (modrm & 0xC0) >> 6;  // dest in EvGv
            var action = (CommonAction)((modrm & 0x38) >> 3);
            var rm = (modrm & 0x07);

            if (mod == 3)
            {
                if (action == CommonAction.CMP) CMP((ushort)_registers[rm], _memory[IP++], false);
                else _registers[rm] = DoAction32(action, (ushort)_registers[rm], _memory[IP++]);
            }
            else throw new Exception("Unsupported modrm");
        }

        private void DoAction16WithEvqp()
        {
            var modrm = _memory[IP++];
            var mod = (modrm & 0xC0) >> 6;  // dest in EvGv
            var action = (CommonAction)((modrm & 0x38) >> 3);
            var rm = (modrm & 0x07);

            if (mod == 3)
            {
                if (action == CommonAction.CMP) CMP((ushort)_registers[rm], _memory[IP++], false);
                else _registers[rm] = DoAction16(action, (ushort)_registers[rm], _memory[IP++]);
            }
            else throw new Exception("Unsupported modrm");
        }

        private byte RegContentsFromReg8(int reg)
        {
            switch (reg)
            {
                case 0:
                case 1:
                case 2:
                case 3: return (byte)_registers[reg];

                case 4:
                case 5:
                case 6:
                case 7: return (byte)(_registers[reg - 4] >> 8);

                default: throw new Exception("Invalid reg");
            }
        }

        private void DoAction8WithModRM(CommonAction action, bool swap = false)
        {
            if (action != CommonAction.MOV) throw new NotImplementedException();

            var modrm = _memory[IP++];
            var mod = (modrm & 0xC0) >> 6;
            var reg = (modrm & 0x38) >> 3;
            var rm = (modrm & 0x07);

            if (mod == 3)
            {
                if (rm < 4)
                {
                    _registers[rm] &= ~0xffUL;
                    _registers[rm] |= RegContentsFromReg8(reg);//DoAction16(action, (ushort)_registers[rm], (ushort)_registers[reg]);
                }
                else
                {
                    _registers[rm - 4] &= ~0xff00UL;
                    _registers[rm - 4] |= ((ulong)RegContentsFromReg8(reg) << 8);
                }

                return;
            }

            ulong addr = AddressFromModRM(Mode.RealMode, mod, reg, rm);

            if (swap && action == CommonAction.MOV)
            {
                _memory[addr] = (byte)_registers[reg];
            }
            else if (action == CommonAction.MOV)
            {
                byte contents = _memory[addr];

                if (reg < 4)
                {
                    _registers[reg] &= ~0xffUL;
                    _registers[reg] |= contents;
                }
                else
                {
                    _registers[reg - 4] &= ~0xff00UL;
                    _registers[reg - 4] |= ((ulong)contents << 8);
                }
                //_registers[reg] = DoAction16(action, (ushort)_registers[reg], contents);
            }
            else throw new NotImplementedException();
        }

        private void Mov32WithModRM(byte modrm)
        {
            var mod = (modrm & 0xC0) >> 6;
            var reg = (modrm & 0x38) >> 3;
            var rm = (modrm & 0x07);

            if (mod == 3)
            {
                _registers[rm] = Read32FromIP();

                return;
            }

            ulong addr = AddressFromModRM(Mode.ProtectedMode, mod, reg, rm);
            var bytes = BitConverter.GetBytes((uint)Read32FromIP());
            Array.Copy(bytes, 0, _memory, (int)addr, 4);
        }

        private void Mov16WithModRM(byte modrm)
        {
            var mod = (modrm & 0xC0) >> 6;
            var reg = (modrm & 0x38) >> 3;
            var rm = (modrm & 0x07);

            if (mod == 3)
            {
                _registers[rm] = Read16FromIP();

                return;
            }

            ulong addr = AddressFromModRM(Mode.RealMode, mod, reg, rm);
            var bytes = BitConverter.GetBytes((ushort)Read16FromIP());
            Array.Copy(bytes, 0, _memory, (int)addr, 2);
        }

        private ulong AddressFromModRM(Mode mode, int mod, int reg, int rm)
        {
            ulong addr = 0;

            if (mod == 1)
            {
                if (mode == Mode.RealMode)
                {
                    switch (rm)
                    {
                        case 0: addr += RBX + RSI; break;
                        case 1: addr += RBX + RDI; break;
                        case 2: addr += RBP + RSI; break;
                        case 3: addr += RBP + RDI; break;
                        case 4: addr += RSI; break;
                        case 5: addr += RDI; break;
                        case 6: addr += RBP; break;
                        case 7: addr += RBX; break;
                    }

                    addr = (ulong)((long)addr + (sbyte)_memory[IP++]);
                }
                else if (mode == Mode.ProtectedMode)
                {
                    switch (rm)
                    {
                        case 0: addr += RAX; break;
                        case 1: addr += RCX; break;
                        case 2: addr += RDX; break;
                        case 3: addr += RBX; break;
                        case 4: addr += AddressFromSIB(mode, mod, reg, rm, _memory[IP++]); break;
                        case 5: addr += RBP; break;
                        case 6: addr += RSI; break;
                        case 7: addr += RDI; break;
                    }

                    addr = (ulong)((long)addr + (sbyte)_memory[IP++]);
                }
            }
            else if (mod == 0)
            {
                if (mode == Mode.RealMode)
                {
                    switch (rm)
                    {
                        case 0: addr = RBX + RSI; break;
                        case 1: addr = RBX + RDI; break;
                        case 2: addr = RBP + RSI; break;
                        case 3: addr = RBP + RDI; break;
                        case 4: addr = RSI; break;
                        case 5: addr = RDI; break;
                        case 6:
                            if (mode == Mode.RealMode) addr = Read16FromIP();
                            else if (mode == Mode.ProtectedMode) addr = Read32FromIP();
                            else throw new NotImplementedException();
                            break;
                        case 7: addr = RBX; break;
                    }
                }
                else if (mode == Mode.ProtectedMode)
                {
                    switch (rm)
                    {
                        case 0: addr = RAX; break;
                        case 1: addr = RCX; break;
                        case 2: addr = RDX; break;
                        case 3: addr = RBX; break;
                        case 4: addr = AddressFromSIB(mode, mod, reg, rm, _memory[IP++]); break;
                        case 5: if (mode == Mode.LongMode) throw new NotImplementedException();
                            addr = Read32FromIP();
                            break;
                        case 6: addr = RSI; break;
                        case 7: addr = RDI; break;
                        default: throw new NotImplementedException();
                    }
                }
            }
            else throw new Exception("Unsupported modrm");

            return addr;
        }

        private ulong AddressFromSIB(Mode mode, int mod, int reg, int rm, byte sib)
        {
            int scale = (sib >> 6) & 0x03;
            int index = (sib >> 3) & 0x07;

            ulong addr = 0;

            switch (index)
            {
                case 0: addr = RAX; break;
                case 1: addr = RCX; break;
                case 2: addr = RDX; break;
                case 3: addr = RBX; break;
                case 4: break;
                case 5: addr = RBP; break;
                case 6: addr = RSI; break;
                case 7: addr = RDI; break;
            }

            ulong @base = 0;
            switch (sib & 0x07)
            {
                case 0: @base = RAX; break;
                case 1: @base = RCX; break;
                case 2: @base = RDX; break;
                case 3: @base = RBX; break;
                case 4: @base = RSP; break;
                case 5:
                    switch (mod)
                    {
                        case 0: @base = (ulong)Read32SignedFromIP(); break;
                        case 1: @base = (ulong)((long)RBP + (sbyte)_memory[IP++]); break;
                        case 2: @base = (ulong)((long)RBP + Read32SignedFromIP()); break;
                        default: throw new Exception("Shouldn't be possible see Table A-36 in AMD tech docs");
                    }
                    break;
                case 6: @base = RSI; break;
                case 7: @base = RDI; break;
            }

            addr = (addr << scale) + @base;   // addr = REG * (1 << scale) + base
            return addr;
        }

        private void DoAction32WithModRM(CommonAction action, bool swap = false)
        {
            var modrm = _memory[IP++];
            var mod = (modrm & 0xC0) >> 6;
            var reg = (modrm & 0x38) >> 3;
            var rm = (modrm & 0x07);

            if (mod == 3)
            {
                _registers[rm] = DoAction32(action, (uint)_registers[rm], (uint)_registers[reg]);

                return;
            }

            ulong addr = AddressFromModRM(Mode.ProtectedMode, mod, reg, rm);

            if (action == CommonAction.LEA) _registers[reg] = (uint)addr;
            else if (swap && action == CommonAction.MOV)
            {
                var bytes = BitConverter.GetBytes((uint)_registers[reg]);
                Array.Copy(bytes, 0, _memory, (int)addr, bytes.Length);
            }
            else
            {
                int contents = BitConverter.ToInt32(_memory, (int)addr);
                _registers[reg] = DoAction32(action, (uint)_registers[reg], (uint)contents);
            }
        }

        private void DoAction16WithModRM(CommonAction action, bool segmentRegister = false, bool swap = false)
        {
            var modrm = _memory[IP++];
            var mod = (modrm & 0xC0) >> 6;
            var reg = (modrm & 0x38) >> 3;
            var rm = (modrm & 0x07);

            if (mod == 3)
            {
                if (segmentRegister) _segmentRegisters[reg] = DoAction16(action, _segmentRegisters[reg], (ushort)_registers[rm]);
                else _registers[rm] = DoAction16(action, (ushort)_registers[rm], (ushort)_registers[reg]);

                return;
            }

            ulong addr = AddressFromModRM(Mode.RealMode, mod, reg, rm);

            if (segmentRegister)
            {
                short contents = BitConverter.ToInt16(_memory, (int)addr);
                _segmentRegisters[reg] = DoAction16(action, (ushort)_segmentRegisters[reg], (ushort)contents);
            }
            else if (action == CommonAction.LEA) _registers[reg] = (ushort)addr;
            else if (swap && action == CommonAction.MOV)
            {
                //short contents = BitConverter.ToInt16(_memory, (int)addr);
                //_registers[reg] = DoAction16(action, (ushort)_registers[reg], (ushort)contents);
                var bytes = BitConverter.GetBytes((ushort)_registers[reg]);
                Array.Copy(bytes, 0, _memory, (int)addr, bytes.Length);
            }
            else
            {
                short contents = BitConverter.ToInt16(_memory, (int)addr);
                _registers[reg] = DoAction16(action, (ushort)_registers[reg], (ushort)contents);
            }
        }

        private ushort DoAction16(CommonAction action, ushort dest, ushort src)
        {
            switch (action)
            {
                case CommonAction.XOR: return (ushort)(dest ^ src);
                case CommonAction.AND: return (ushort)(dest & src);
                case CommonAction.OR: return (ushort)(dest | src);
                case CommonAction.ADD: return (ushort)(dest + src);
                case CommonAction.SUB:
                    FLAGS &= ~(RFLAGS.CF | RFLAGS.ZF | RFLAGS.SF | RFLAGS.OF);

                    if ((long)dest - (long)src < 0) FLAGS |= RFLAGS.SF;
                    if (dest + src > ushort.MaxValue) FLAGS |= RFLAGS.OF;

                    return (ushort)(dest - src);
                case CommonAction.MOV: return src;
                case CommonAction.MOVZX8: return (ushort)(src & 0xff);
                case CommonAction.MUL:
                    uint mul = (uint)(RAX & 0xffff) * (uint)dest;
                    _registers[0] = mul & 0xffff;
                    _registers[2] = (mul >> 16) & 0xffff;
                    if ((mul >> 16) > 0) FLAGS |= (RFLAGS.CF | RFLAGS.OF);
                    else FLAGS &= ~(RFLAGS.CF | RFLAGS.OF);
                    return dest;    // we do not store the value back into the register, so just return the original value
                case CommonAction.CMP:
                    CMP(dest, src, false);
                    return dest;    // we do not store the value back into the register, so just return the original value
                default: throw new Exception("Unsupported action " + action.ToString());
            }
        }

        private uint DoAction32(CommonAction action, uint dest, uint src)
        {
            switch (action)
            {
                case CommonAction.XOR: return (dest ^ src);
                case CommonAction.AND: return (dest & src);
                case CommonAction.OR: return (dest | src);
                case CommonAction.ADD: return (dest + src);
                case CommonAction.SUB:
                    FLAGS &= ~(RFLAGS.CF | RFLAGS.ZF | RFLAGS.SF | RFLAGS.OF);

                    if ((long)dest - (long)src < 0) FLAGS |= RFLAGS.SF;
                    if (dest + src > ushort.MaxValue) FLAGS |= RFLAGS.OF;

                    return dest - src;
                case CommonAction.MOV: return src;
                case CommonAction.MOVZX8: return (uint)(src & 0xff);
                case CommonAction.MOVZX16: return (uint)(src & 0xffff);
                /*case CommonAction.MUL:
                    uint mul = (uint)(RAX & 0xffff) * (uint)dest;
                    _registers[0] = mul & 0xffff;
                    _registers[2] = (mul >> 16) & 0xffff;
                    if ((mul >> 16) > 0) FLAGS |= (RFLAGS.CF | RFLAGS.OF);
                    else FLAGS &= ~(RFLAGS.CF | RFLAGS.OF);
                    return dest;*/    // we do not store the value back into the register, so just return the original value
                case CommonAction.CMP:
                    CMP(dest, src, false);
                    return dest;    // we do not store the value back into the register, so just return the original value
                default: throw new Exception("Unsupported action " + action.ToString());
            }
        }
    }
}
