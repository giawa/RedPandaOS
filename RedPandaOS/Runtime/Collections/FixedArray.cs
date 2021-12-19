using Kernel.Memory;
using System.Runtime.InteropServices;

namespace Runtime.Collections
{
    public class FixedArray<T>
    {
        private uint _array;
        private int _count;

        public int Count { get { return _count; } }

        public FixedArray(int size, int pageAligned)
        {
            uint sizeInBytes = (uint)size * (uint)Marshal.SizeOf<T>();

            if (pageAligned != 0) _array = KernelHeap.KernelAllocator.MallocPageAligned(sizeInBytes);
            else _array = KernelHeap.KernelAllocator.Malloc(sizeInBytes);

            _count = size;
        }

        public uint AddressOfArray
        {
            get
            {
                return Utilities.ObjectToPtr(_array);
            }
        }

        public T this[int index]
        {
            get
            {
                uint addr = _array + (uint)index * (uint)Marshal.SizeOf<T>();
                return Utilities.PtrToObject<T>(CPUHelper.CPU.ReadMemInt(addr));
            }
            set
            {
                uint addr = _array + (uint)index * (uint)Marshal.SizeOf<T>();
                CPUHelper.CPU.WriteMemInt(addr, Utilities.ObjectToPtr(value));
            }
        }

        public T this[uint index]
        {
            get
            {
                uint addr = _array + index * (uint)Marshal.SizeOf<T>();
                return Utilities.PtrToObject<T>(CPUHelper.CPU.ReadMemInt(addr));
            }
            set
            {
                uint addr = _array + index * (uint)Marshal.SizeOf<T>();
                CPUHelper.CPU.WriteMemInt(addr, Utilities.ObjectToPtr(value));
            }
        }
    }
}
