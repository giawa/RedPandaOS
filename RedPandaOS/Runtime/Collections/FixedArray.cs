using Kernel.Memory;
using System;
using System.Runtime.InteropServices;

namespace Runtime.Collections
{
    public class FixedArrayPtr<T>
    {
        private uint _array;
        private int _count;

        public int Count { get { return _count; } }

        public FixedArrayPtr(int size, int pageAligned)
        {
            uint sizeInBytes = (uint)size * (uint)Marshal.SizeOf<IntPtr>();

            if (pageAligned != 0) _array = KernelHeap.KernelAllocator.MallocPageAligned(sizeInBytes);
            else _array = KernelHeap.KernelAllocator.Malloc(sizeInBytes);

            _count = size;
        }

        public void Free()
        {
            uint sizeInBytes = (uint)_count * (uint)Marshal.SizeOf<IntPtr>();
            KernelHeap.KernelAllocator.Free(_array, sizeInBytes);
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
                uint addr = _array + (uint)index * (uint)Marshal.SizeOf<IntPtr>();
                return Utilities.PtrToObject<T>(CPUHelper.CPU.ReadMemInt(addr));
            }
            set
            {
                uint addr = _array + (uint)index * (uint)Marshal.SizeOf<IntPtr>();
                CPUHelper.CPU.WriteMemInt(addr, Utilities.ObjectToPtr(value));
            }
        }

        public T this[uint index]
        {
            get
            {
                uint addr = _array + index * (uint)Marshal.SizeOf<IntPtr>();
                return Utilities.PtrToObject<T>(CPUHelper.CPU.ReadMemInt(addr));
            }
            set
            {
                uint addr = _array + index * (uint)Marshal.SizeOf<IntPtr>();
                CPUHelper.CPU.WriteMemInt(addr, Utilities.ObjectToPtr(value));
            }
        }
    }

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

        public void Free()
        {
            uint sizeInBytes = (uint)_count * (uint)Marshal.SizeOf<T>();
            KernelHeap.KernelAllocator.Free(_array, sizeInBytes);
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
