using CPUHelper;
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

    public static class Interrupts
    {
        private static IDT_Entry[] _idt_entries;
        private static CPU.IDTPointer _idt_ptr;

        public static void Init()
        {
            _idt_entries = Memory.BumpHeap.MallocArray<IDT_Entry>(256);

            _idt_ptr.limit = (ushort)(Marshal.SizeOf<IDT_Entry>() * 256 - 1);
            _idt_ptr.address = Memory.BumpHeap.ObjectToPtr(_idt_entries);

            for (int i = 0; i < 32; i++)
            {
                _idt_entries[i].base_lo = (ushort)ISR_ADDRESSES[i];
                _idt_entries[i].sel = 0x08;
                _idt_entries[i].flags = 0x8E;
                _idt_entries[i].base_hi = (ushort)(ISR_ADDRESSES[i] >> 16);
            }

            CPU.LoadIDT(_idt_ptr);
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

        // this is automatically populated by the assembler with the addresses of the ISR labels
        private static uint[] ISR_ADDRESSES;
    }
}
