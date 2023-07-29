namespace Kernel.Memory
{
    public class SMAP_Entry
    {
        public uint BaseL;
        public uint BaseH;
        public uint LengthL;
        public uint LengthH;
        public uint Type;
        public uint ACPI;

        public bool ContainsFrame(uint frame)
        {
            uint baseFrame = (BaseL >> 12) | (BaseH << 20);
            uint lengthFrame = (LengthL >> 12) | (LengthH << 20);

            return frame >= baseFrame && frame < (baseFrame + lengthFrame);
        }
    }

    public static class Fast
    {
        public static T Clone<T>(T original, uint size) where T : class
        {
            var addr = KernelHeap.Malloc(size);

            CPUHelper.CPU.FastCopyBytes(Runtime.Memory.Utilities.ObjectToPtr(original), addr, size);

            return Runtime.Memory.Utilities.PtrToObject<T>(addr);
        }
    }
}
