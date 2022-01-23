using System;
using System.Collections.Generic;
using System.Text;

namespace IL2Asm.Assembler.x86
{
    internal static class ISR
    {
        private static bool _addedISRMethods = false;
        private static bool _addedIRQMethods = false;

        internal static bool AddISRMethods(IAssembler assembler, bool irqStub = false)
        {
            if (irqStub && _addedIRQMethods) return false;
            if (!irqStub && _addedISRMethods) return false;

            string stubName = (irqStub ? "irq_stub" : "isr_stub");

            if ((!_addedISRMethods && !irqStub) || (!_addedIRQMethods && irqStub))
            {
                AssembledMethod isrMethods = new AssembledMethod(null, "ISR_STUB");

                isrMethods.AddAsm($"{stubName}:");
                isrMethods.AddAsm("pusha");
                isrMethods.AddAsm("mov ax, ds");
                isrMethods.AddAsm("push eax");
                isrMethods.AddAsm("mov ax, 0x10");
                isrMethods.AddAsm("mov ds, ax");
                isrMethods.AddAsm("mov es, ax");
                isrMethods.AddAsm("mov fs, ax");
                isrMethods.AddAsm("mov gs, ax");

                if (irqStub) isrMethods.AddAsm("call Kernel_Devices_PIC_IrqHandler_Void_U4_U4_U4_U4_U4_U4_U4_U4_U4_U4_U4_U4_U4_U4_U4_U4");
                else isrMethods.AddAsm("call Kernel_Devices_PIC_IsrHandler_Void_U4_U4_U4_U4_U4_U4_U4_U4_U4_U4_U4_U4_U4_U4_U4_U4");

                isrMethods.AddAsm("pop eax");
                isrMethods.AddAsm("mov ds, ax");
                isrMethods.AddAsm("mov es, ax");
                isrMethods.AddAsm("mov fs, ax");
                isrMethods.AddAsm("mov gs, ax");
                isrMethods.AddAsm("popa");
                isrMethods.AddAsm("add esp, 8");    // pop error code and interrupt number
                isrMethods.AddAsm("sti");
                isrMethods.AddAsm("iret");
                isrMethods.AddAsm("");

                if (irqStub)
                {
                    for (int i = 0; i < 16; i++)
                    {
                        isrMethods.AddAsm($"IRQ{i}:");
                        isrMethods.AddAsm("cli");
                        isrMethods.AddAsm("push byte 0; error code");
                        isrMethods.AddAsm($"push byte {i + 32}; interrupt number");
                        isrMethods.AddAsm($"jmp {stubName}");
                        isrMethods.AddAsm("");
                    }
                }
                else
                {
                    for (int i = 0; i < 32; i++)
                    {
                        isrMethods.AddAsm($"ISR{i}:");
                        isrMethods.AddAsm("cli");
                        if (!(i == 8 || (i >= 10 && i <= 14))) isrMethods.AddAsm("push byte 0; error code");
                        isrMethods.AddAsm($"push byte {i}; interrupt number");
                        isrMethods.AddAsm($"jmp {stubName}");
                        isrMethods.AddAsm("");
                    }
                }

                assembler.Methods.Add(isrMethods);

                if (irqStub) _addedIRQMethods = true;
                else _addedISRMethods = true;
            }

            return true;
        }
    }
}
