using Kernel.Memory;

namespace Runtime.Collections
{
    public class ObjectPool<T> where T : class
    {
        private T[] _array;

        public ObjectPool(int capacity)
        {
            _array = new T[capacity];
        }

        public bool TryAlloc(out T item)
        {
            int index = -1;
            for (int i = 0; i < _array.Length; i++)
            {
                if (_array[i] == null)
                {
                    index = i;
                    break;
                }
            }

            if (index == -1)
            {
                item = null;
                return false;
            }

            _array[index] = KernelHeap.KernelAllocator.Malloc<T>((uint)System.Runtime.InteropServices.Marshal.SizeOf<T>());
            item = _array[index];

            return true;
        }

        public void Free(T item)
        {
            for (int i = 0; i < _array.Length; i++)
            {
                if (_array[i] == item)
                {
                    _array[i] = null;
                }
            }
        }
    }
}
