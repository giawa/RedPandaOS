using CPUHelper;
using Kernel.Memory;
using System;
using System.Runtime.InteropServices;

namespace Kernel.Devices
{
    [StructLayout(LayoutKind.Sequential)]
    public struct IDT_Entry
    {
        public ushort base_lo;
        public ushort sel;
        public byte always0;
        public byte flags;
        public ushort base_hi;
    }

    public static class PIC
    {
        private static IDT_Entry[] _idt_entries;
        private static CPU.IDTPointer _idt_ptr;

        private static Action[] _irqHandlers = new Action[16];
        private static Action[] _isrHandlers = new Action[32];

        public static void Init()
        {
            _idt_entries = new IDT_Entry[256];
            //_irqHandlers = new Action[16];
            //_isrHandlers = new Action[32];

            _idt_ptr.limit = (ushort)(Marshal.SizeOf<IDT_Entry>() * 256 - 1);
            _idt_ptr.address = Utilities.ObjectToPtr(_idt_entries) + 8; // plus 8 bytes to find start of actual array data

            // remap the IRQ table
            // this moves the interrupts the come from the PICs from 0->0x0f up to 0x20->0x2f
            // this is to deal with the software interrupts (ISRs) being mapped to 0->0x1f, which
            // conflicts with the default IRQ mapping.  If left as-is we wouldn't be able to tell
            // software interrupts and PIC interrupts apart
            CPU.OutDxAl(0x20, 0x11);
            CPU.OutDxAl(0xA0, 0x11);
            CPU.OutDxAl(0x21, 0x20);
            CPU.OutDxAl(0xA1, 0x28);
            CPU.OutDxAl(0x21, 0x04);
            CPU.OutDxAl(0xA1, 0x02);
            CPU.OutDxAl(0x21, 0x01);
            CPU.OutDxAl(0xA1, 0x01);
            CPU.OutDxAl(0x21, 0x0);
            CPU.OutDxAl(0xA1, 0x0);

            // set up CPU interrupts
            for (int i = 0; i < 32; i++)
            {
                _idt_entries[i].base_lo = (ushort)ISR_ADDRESSES[i];
                _idt_entries[i].sel = 0x08;
                _idt_entries[i].flags = 0x8E;
                _idt_entries[i].base_hi = (ushort)(ISR_ADDRESSES[i] >> 16);
            }

            // set up IRQ interrupts
            for (int i = 0; i < 16; i++)
            {
                int entry = i + 32;
                _idt_entries[entry].base_lo = (ushort)IRQ_ADDRESSES[i];
                _idt_entries[entry].sel = 0x08;
                _idt_entries[entry].flags = 0x8E;
                _idt_entries[entry].base_hi = (ushort)(IRQ_ADDRESSES[i] >> 16);
            }

            CPU.LoadIDT(_idt_ptr);
            CPU.Sti();
        }

        public static void SetIsrCallback(int idt, Action callback)
        {
            if (_isrHandlers == null || idt < 0 || idt >= _isrHandlers.Length) return;

            _isrHandlers[idt] = callback;
        }

        public static void SetIrqCallback(int irq, Action callback)
        {
            if (_irqHandlers == null || irq < 0 || irq >= _irqHandlers.Length) return;

            _irqHandlers[irq] = callback;
        }

        public static void IsrHandler(
            uint ss, uint useresp, uint eflags, uint cs, uint eip,
            uint err_code, uint int_no,
            uint eax, uint ebx, uint ecx, uint edx, uint esp, uint ebp, uint esi, uint edi,
            uint ds)
        {
            if (_isrHandlers[int_no] != null) _isrHandlers[int_no]();
            else
            {
                Logging.WriteLine(LogLevel.Panic, "Unhandled interrupt {0}", int_no);

                while (true) ;
            }
        }

        public static void IrqHandler(
            uint ss, uint useresp, uint eflags, uint cs, uint eip,
            uint err_code, uint int_no,
            uint eax, uint ebx, uint ecx, uint edx, uint esp, uint ebp, uint esi, uint edi,
            uint ds)
        {
            if (int_no >= 40)
            {
                CPU.OutDxAl(0xA0, 0x20);
            }
            CPU.OutDxAl(0x20, 0x20);

            if (_irqHandlers[int_no - 32] != null) _irqHandlers[int_no - 32]();
            else
            {
                Logging.WriteLine(LogLevel.Panic, "Unhandled IRQ {0}", int_no);

                while (true) ;
            }

            if (int_no == 32)
            {
                Scheduler.Tick();
            }
        }

#pragma warning disable CS0649
        // these are automatically populated by the assembler with the addresses of the ISR labels
        private static uint[] ISR_ADDRESSES;
        private static uint[] IRQ_ADDRESSES;
#pragma warning restore CS0649
    }
}
