using Kernel.Memory;
using System;
using System.Runtime.InteropServices;

namespace Runtime
{
    public class SafeArray<T>
    {
        private T[] _array;
        private IHeapAllocator _heapAllocator;

        public T this[int a]
        {
            get
            {
                if (a < 0 || a >= _array.Length) throw new IndexOutOfRangeException();
                return _array[a];
            }
            set
            {
                if (a < 0 || a >= _array.Length) throw new IndexOutOfRangeException();
                _array[a] = value;
            }
        }

        public SafeArray(int size)
        {
            _heapAllocator = KernelHeap.KernelAllocator;
            _array = new T[size];
        }

        ~SafeArray()
        {
            Free();
        }

        public void Free()
        {
            // + 8 bytes for array type and array length
            if (_heapAllocator != null && _array != null) _heapAllocator.Free(Utilities.ObjectToPtr(_array), 8 + (uint)(_array.Length * Marshal.SizeOf<T>()));
            _array = null;
        }
    }
}
