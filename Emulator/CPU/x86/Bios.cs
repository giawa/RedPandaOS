using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Emulator.CPU.x86
{
    public partial class CPU
    {
        public void BiosHandleInterrupt(byte interrupt)
        {
            const int heads = 16;
            const int sectorsPerTrack = 63;
            const int bytesPerSector = 512;

            if (interrupt == 0x10)  // write character to screen
            {
                Console.Write((char)(byte)RAX);
            }
            else if (interrupt == 0x13) // disk access
            {
                if ((_registers[0] & 0xff00UL) == 0x0800)       // get disk geometry
                {
                    // 0x3f into CL
                    _registers[1] &= ~0xffUL;
                    _registers[1] |= 0x3f;

                    // 0x0f into DH
                    _registers[2] &= ~0xff00UL;
                    _registers[2] |= 0x0f00UL;
                }
                else if ((_registers[0] & 0xff00UL) == 0x0200)  // read from disk
                {
                    byte sectorsToRead = (byte)RAX;
                    byte cylinder = (byte)(RCX >> 8);
                    byte sector = (byte)RCX;
                    byte head = (byte)(RDX >> 8);
                    byte disk = (byte)RDX;

                    if (disk != 0x80) throw new NotImplementedException();

                    int dest = (ushort)RBX;
                    dest |= (int)ES << 4;

                    // map CHS to LBA
                    int src = (cylinder * heads + head) * sectorsPerTrack + (sector - 1);
                    src *= bytesPerSector;

                    try
                    {
                        Array.Copy(_disk, src, _memory, dest, sectorsToRead * bytesPerSector);

                        // no error clears CF and sets RAX to sectors read
                        FLAGS &= ~RFLAGS.CF;
                        _registers[0] = sectorsToRead;
                    }
                    catch (Exception e)
                    {
                        FLAGS |= RFLAGS.CF;
                        _registers[0] = 0;
                    }
                }
                else if ((_registers[0] & 0xff00UL) == 0x0000) ;    // reset disk system
                else throw new NotImplementedException();
            }
            else if (interrupt == 0x15)  // memory stuff
            {
                if ((_registers[0] & 0xffff) == 0xE820)
                {
                    // clear error flag
                    FLAGS &= ~RFLAGS.CF;

                    // get the continuation so we can tell which memory entry to provide
                    ulong continuation = _registers[3];

                    // if we're outside the range then return an error
                    if (continuation >= (ulong)_memoryEntries.Length)
                    {
                        FLAGS |= RFLAGS.CF;
                        return;
                    }

                    // copy the entry to the requested memory location
                    CopyEntryToMemory(_memoryEntries[continuation], RDI);

                    // increment the continuation
                    if ((int)continuation + 1 >= _memoryEntries.Length) _registers[3] = 0;
                    else _registers[3] = continuation + 1;
                }
                else if ((_registers[0] & 0xffff) == 0x2403)
                {
                    // clear error flag
                    FLAGS &= ~RFLAGS.CF;

                    _registers[0] &= ~0xff00UL;   // clear AH
                }
                else if ((_registers[0] & 0xffff) == 0x2402)
                {
                    // clear error flag
                    FLAGS &= ~RFLAGS.CF;

                    _registers[0] &= ~0xffffUL;     // clear AH and AL
                    _registers[0] |= 0x01;          // mark AL as 1 for success, we've enabled the A20 line
                }
                else throw new NotImplementedException();
            }
            else throw new NotImplementedException();
        }

        internal void CopyEntryToMemory(MemoryMapEntry entry, ulong address)
        {
            Array.Copy(BitConverter.GetBytes(entry.Offset), 0, _memory, (int)address, 8);
            Array.Copy(BitConverter.GetBytes(entry.Length), 0, _memory, (int)address + 8, 8);
            Array.Copy(BitConverter.GetBytes(entry.Type), 0, _memory, (int)address + 16, 4);
            Array.Copy(BitConverter.GetBytes(entry.ACPI), 0, _memory, (int)address + 20, 4);
        }

        internal class MemoryMapEntry
        {
            public ulong Offset;
            public ulong Length;
            public uint Type;
            public uint ACPI;

            public MemoryMapEntry(ulong offset, ulong length, uint type, uint acpi)
            {
                Offset = offset;
                Length = length;
                Type = type;
                ACPI = acpi;
            }
        }

        internal MemoryMapEntry[] _memoryEntries = new MemoryMapEntry[]
        {
            new MemoryMapEntry(0, 0x9fC00, 1, 1),       // first chunk of memory available to all x86 CPUs
            new MemoryMapEntry(0x9FC00, 0x400, 2, 1),   // reserved memory
            new MemoryMapEntry(0xF0000, 0x10000, 2, 1), // reserved memory
            new MemoryMapEntry(0x100000, 0x4000000 - 0x100000, 1, 1), // 63MB of memory
        };
    }
}
