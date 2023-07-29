using System;
using System.Collections.Generic;
using System.Text;

namespace Kernel.Memory
{
    public static class Stack
    {
        public static void MoveStack(uint oldStackAddr, uint newStackAddr, uint size)
        {
            /*for (uint i = newStackAddr; i >= newStackAddr - size; i -= 4096)
            {
                Paging.AllocateFrame(Paging.GetPage(i, true, Paging.CurrentDirectory), false, true);
            }

            Paging.FlushTLB();*/

            var esp = CPUHelper.CPU.ReadESP();
            var ebp = CPUHelper.CPU.ReadEBP();

            var offset = newStackAddr - oldStackAddr;
            uint newEsp = esp + offset;
            uint newEbp = ebp + offset;

            CPUHelper.CPU.FastCopyDWords(esp, newEsp, oldStackAddr - esp);

            for (uint i = newStackAddr; i > newEsp; i -= 4)
            {
                var value = CPUHelper.CPU.ReadMemInt(i);

                if (value < oldStackAddr && value >= esp)
                {
                    value = value + offset;
                    CPUHelper.CPU.WriteMemInt(i, value);
                }
            }

            //CPUHelper.CPU.WriteEBP(newEbp);
            //CPUHelper.CPU.WriteESP(newEsp);
        }
    }
}
