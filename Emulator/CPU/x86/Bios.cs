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
            if (interrupt == 0x10) Console.Write((char)(byte)RAX);
            else throw new NotImplementedException();
        }
    }
}
