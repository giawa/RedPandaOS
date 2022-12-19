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
            if (interrupt == 0x10)  // write character to screen
            {
                Console.Write((char)(byte)RAX);
            }
            else if (interrupt == 0x13) // get disk geometry
            {
                // 0x3f into CL
                _registers[1] &= ~0xffUL;
                _registers[1] |= 0x3f;

                // 0x0f into DH
                _registers[2] &= ~0xff00UL;
                _registers[2] |= 0x0f00UL;
            }
            else throw new NotImplementedException();
        }
    }
}
