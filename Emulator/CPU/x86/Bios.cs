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
            else throw new NotImplementedException();
        }
    }
}
