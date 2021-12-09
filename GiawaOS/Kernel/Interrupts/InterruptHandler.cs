﻿using CPUHelper;
using Kernel.Devices;
using System.Runtime.InteropServices;

namespace Kernel.Interrupts
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

    public static class InterruptHandler
    {
        private static IDT_Entry[] _idt_entries;
        private static CPU.IDTPointer _idt_ptr;

        public static void Init()
        {
            _idt_entries = Memory.BumpHeap.MallocArray<IDT_Entry>(256);

            _idt_ptr.limit = (ushort)(Marshal.SizeOf<IDT_Entry>() * 256 - 1);
            _idt_ptr.address = Memory.BumpHeap.ObjectToPtr(_idt_entries);

            // remap the IRQ table
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

        public static void IsrHandler(
            uint ss, uint useresp, uint eflags, uint cs, uint eip,
            uint err_code, uint int_no,
            uint eax, uint ebx, uint ecx, uint edx, uint esp, uint ebp, uint esi, uint edi,
            uint ds)
        {
            VGA.WriteVideoMemoryString("Interrupt ");
            VGA.WriteHex(int_no);
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

            if (int_no == 32) PIT.Tick();
            else
            {
                VGA.WriteVideoMemoryString("IRQ ");
                VGA.WriteHex(int_no);
                VGA.WriteVideoMemoryChar(' ');
            }
        }

        // this is automatically populated by the assembler with the addresses of the ISR labels
        private static uint[] ISR_ADDRESSES;
        private static uint[] IRQ_ADDRESSES;
    }
}