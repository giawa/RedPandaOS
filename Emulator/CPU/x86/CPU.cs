using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    public partial class CPU
    {
        public Mode Mode { get; private set; } = Mode.RealMode;

        private ulong IP;
        private byte[] _memory;

        public RFLAGS FLAGS = 0;

        public void LoadProgram(byte[] memory)
        {
            _memory = memory;
            IP = 0;
        }

        public void Jump(ulong ip)
        {
            IP = ip;
        }

        public void Tick()
        {
            var opcode = _memory[IP++];

            switch (opcode)
            {
                case 0x06:
                    if (Mode == Mode.RealMode)
                    {
                        var bytes = BitConverter.GetBytes((ushort)ES);
                        _registers[4] -= 2;
                        Array.Copy(bytes, 0, _memory, (int)RSP, 2);
                    }
                    else throw new NotImplementedException();
                    break;

                case 0x09:
                    if (Mode == Mode.RealMode) DoAction16WithModRM(CommonAction.OR);
                    else throw new NotImplementedException();
                    break;

                case 0x0F:
                    SecondaryOpcode();
                    break;

                case 0x25:  // AND rAX Ivds
                    if (Mode == Mode.LongMode) throw new Exception("TODO: Must sign extend a 32 bit value");
                    _registers[0] = _registers[0] & ReadWordFromIP();
                    break;

                case 0x29:  // SUB
                    if (Mode == Mode.RealMode) DoAction16WithModRM(CommonAction.SUB);
                    else throw new NotImplementedException();
                    break;

                case 0x31:  // XOR Evqp Gvqp
                    DoAction16WithModRM(CommonAction.XOR);   // xor destination (register or memory) with the contents of a register and store in destination
                    break;

                case 0x39:
                    DoAction16WithModRM(CommonAction.CMP);
                    break;

                case 0x3D:  // CMP rAX Ivds
                    CMP(RAX, Read16FromIP(), false);
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
                    if (Mode == Mode.RealMode) _registers[opcode & 0x0f] = DoAction16(CommonAction.ADD, (ushort)_registers[opcode & 0x0f], 1);
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
                    if (Mode == Mode.RealMode)
                    {
                        var bytes = BitConverter.GetBytes((ushort)_registers[opcode & 0x07]);
                        _registers[4] -= 2;
                        Array.Copy(bytes, 0, _memory, (int)RSP, 2);
                    }
                    else throw new Exception("Unsupported mode");
                    break;

                case 0x58:
                case 0x59:
                case 0x5A:
                case 0x5B:
                case 0x5C:
                case 0x5D:
                case 0x5E:
                case 0x5F:
                    if (RSP > int.MaxValue) throw new Exception("64b mode unsupported");
                    if (Mode == Mode.RealMode)
                    {
                        _registers[opcode & 0x07] = BitConverter.ToUInt16(_memory, (int)RSP);
                        _registers[4] += 2;
                    }
                    else throw new Exception("Unsupported mode");
                    break;

                case 0x68:  // PUSH imm
                    if (Mode == Mode.RealMode)
                    {
                        var bytes = BitConverter.GetBytes((ushort)Read16FromIP());
                        _registers[4] -= 2;
                        Array.Copy(bytes, 0, _memory, (int)RSP, 2);
                    }
                    else throw new Exception("Unsupported mode");
                    break;

                case 0x6A:  // PUSH imm8
                    if (Mode == Mode.RealMode)
                    {
                        var bytes = BitConverter.GetBytes((ushort)_memory[IP++]);
                        _registers[4] -= 2;
                        Array.Copy(bytes, 0, _memory, (int)RSP, 2);
                    }
                    else throw new NotImplementedException();
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

                case 0x83:  // XOR Evqp Ibs
                    DoAction16WithEvqp();
                    break;

                case 0x88:  // MOV
                    DoAction8WithModRM(CommonAction.MOV);
                    break;

                case 0x89:  // MOV Evqp Gvqp
                    DoAction16WithModRM(CommonAction.MOV, swap: true);
                    break;

                case 0x8B:
                    DoAction16WithModRM(CommonAction.MOV);//, dereferenceAddress: true);
                    break;

                case 0x8D:  // LEA
                    DoAction16WithModRM(CommonAction.LEA);
                    break;

                case 0x8E:  // MOV Sw Ew
                    DoAction16WithModRM(CommonAction.MOV, true);   // mov src (register or memory) into a segment register
                    break;

                case 0xA1:
                    if (Mode == Mode.RealMode)
                    {
                        var a1offset = Read16FromIP();
                        _registers[0] = BitConverter.ToUInt16(_memory, (int)a1offset);
                    }
                    else throw new NotImplementedException();
                    break;

                // MOV [imm16], AX
                case 0xA3:
                    var a3offset = Read16FromIP();
                    var a3bytes = BitConverter.GetBytes((ushort)_registers[0]);
                    Array.Copy(a3bytes, 0, _memory, (int)a3offset, a3bytes.Length);
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
                    _registers[opcode & 0x07] = ReadWordFromIP();
                    break;

                // ret
                case 0xC2:
                    byte toPop = _memory[IP++];
                    if (Mode == Mode.RealMode)
                    {
                        IP = BitConverter.ToUInt16(_memory, (int)RSP);
                        _registers[4] += (ulong)(toPop + 2);   // +2 for the IP we just popped
                    }
                    else throw new NotImplementedException();
                    break;

                // interrupt!
                case 0xCD:
                    byte interruptType = _memory[IP++];
                    if (Mode == Mode.RealMode) BiosHandleInterrupt(interruptType);
                    else throw new NotImplementedException();
                    break;

                // shift Evqp
                case 0xD3:
                    DoRotation16WithEvqp(); 
                    break;

                case 0xEB:  // JMP imm
                    JMP(true, (sbyte)_memory[IP++]);
                    break;

                case 0xE8:  // CALL imm
                    if (Mode == Mode.RealMode)
                    {
                        var callOffset = Read16SignedFromIP();
                        var bytes = BitConverter.GetBytes((ushort)IP);
                        _registers[4] -= 2;
                        Array.Copy(bytes, 0, _memory, (int)RSP, 2);

                        IP = (ulong)((long)IP + callOffset);
                    }
                    else throw new NotImplementedException();
                    break;

                case 0xF7:
                    if (Mode == Mode.RealMode) DoAction16WithModRM(CommonAction.MUL);
                    else throw new NotImplementedException();
                    break;

                default: throw new Exception(string.Format("Unknown opcode 0x{0:X}", opcode));
            }
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
                else throw new NotImplementedException();
                return stack.ToArray();
            }
        }

        private void SecondaryOpcode()
        {
            var secondaryOpcode = _memory[IP++];

            switch (secondaryOpcode)
            {
                case 0xb6:  // MOVZX Gvqp Eb
                    DoAction16WithModRM(CommonAction.MOVZX8);
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
            ulong temp = _memory[IP++];
            temp |= ((ulong)_memory[IP++]) << 8;
            return temp;
        }

        private ulong ReadWordFromIP()
        {
            if (Mode == Mode.RealMode) return Read16FromIP();
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
            MUL,
            LEA,
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

        private void DoRotation16WithEvqp()
        {
            var modrm = _memory[IP++];
            var mod = (modrm & 0xC0) >> 6;  // dest in EvGv
            var action = (RotationAction)((modrm & 0x38) >> 3);
            var rm = (modrm & 0x07);

            if (mod == 3)
            {
                ushort initial = (ushort)_registers[rm];
                _registers[rm] &= ~0xffffUL;
                switch (action)
                {
                    case RotationAction.SHR:
                        _registers[rm] |= (ushort)(initial >> (byte)(_registers[1] & 0xff));
                        FLAGS &= ~RFLAGS.CF;
                        if (initial >> (byte)((_registers[1] & 0xff) - 1) != 0) FLAGS |= RFLAGS.CF;
                        break;
                        
                    default: throw new NotImplementedException();
                }
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

        private void DoAction8WithModRM(CommonAction action)
        {
            if (action != CommonAction.MOV) throw new NotImplementedException();

            var modrm = _memory[IP++];
            var mod = (modrm & 0xC0) >> 6;
            var reg = (modrm & 0x38) >> 3;
            var rm = (modrm & 0x07);

            ulong addr = 0;

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
            /*else if (mod == 1)
            {
                addr = _memory[IP++];

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
            }
            else if (mod == 0)
            {
                switch (rm)
                {
                    case 0: addr = RBX + RSI; break;
                    case 1: addr = RBX + RDI; break;
                    case 2: addr = RBP + RSI; break;
                    case 3: addr = RBP + RDI; break;
                    case 4: addr = RSI; break;
                    case 5: addr = RDI; break;
                    case 6: throw new Exception("Use disp16");
                    case 7: addr = RBX; break;
                }
            }*/
            else throw new Exception("Unsupported modrm");

            short contents = BitConverter.ToInt16(_memory, (int)addr);
            _registers[reg] = DoAction16(action, (ushort)_registers[reg], (ushort)contents);
        }

        private void DoAction16WithModRM(CommonAction action, bool segmentRegister = false, bool swap = false)
        {
            var modrm = _memory[IP++];
            var mod = (modrm & 0xC0) >> 6;
            var reg = (modrm & 0x38) >> 3;
            var rm = (modrm & 0x07);

            ulong addr = 0;

            if (mod == 3)
            {
                if (segmentRegister) _segmentRegisters[reg] = DoAction16(action, _segmentRegisters[reg], (ushort)_registers[rm]);
                else _registers[rm] = DoAction16(action, (ushort)_registers[rm], (ushort)_registers[reg]);

                return;
            }
            else if (mod == 1)
            {
                addr = _memory[IP++];

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
            }
            else if (mod == 0)
            {
                switch (rm)
                {
                    case 0: addr = RBX + RSI; break;
                    case 1: addr = RBX + RDI; break;
                    case 2: addr = RBP + RSI; break;
                    case 3: addr = RBP + RDI; break;
                    case 4: addr = RSI; break;
                    case 5: addr = RDI; break;
                    case 6: addr = Read16FromIP(); break;
                    case 7: addr = RBX; break;
                }
            }
            else throw new Exception("Unsupported modrm");

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
    }
}
