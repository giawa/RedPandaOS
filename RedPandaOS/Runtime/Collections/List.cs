using Kernel.Memory;

namespace Runtime.Collections
{
    public class List<T>
    {
        private T[] _array;
        private int _index = 0;

        private const int DefaultCapacity = 5;

        public List()
        {
            Initialize(DefaultCapacity);
        }

        public List(int capacity)
        {
            Initialize(capacity);
        }

        public void Initialize(int capacity)
        {
            _array = new T[capacity];
        }

        public void Dispose()
        {
            Kernel.Memory.KernelHeap.KernelAllocator.Free(_array);
            _array = null;
        }

        public void Clear()
        {
            System.Array.Clear(_array, 0, _index);
            _index = 0;
        }

        private void EnsureCapacity()
        {
            var oldArray = _array;

            _array = new T[_array.Length * 2];

            for (int i = 0; i < oldArray.Length; i++)
                _array[i] = oldArray[i];

            // dispose of the old array
            Kernel.Memory.KernelHeap.KernelAllocator.Free(oldArray);
        }

        public void Add(T item)
        {
            if (_index >= _array.Length) EnsureCapacity();

            _array[_index] = item;
            _index++;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _index) throw new System.ArgumentOutOfRangeException("Index was out of range");

            for (int i = index; i < _index - 1; i++)
            {
                _array[i] = _array[i + 1];
            }

            _index--;
        }

        /// <summary>
        /// Removes an item from the list if their memory locations match.
        /// Note:  This is different behaviour from .NET, which can use overrides/etc
        /// </summary>
        /// <param name="item">The item to remove from the List</param>
        /// <returns>True if the item was in the List and was removed, false if the item was not in the List</returns>
        public bool Remove(T item)
        {
            for (int i = 0; i < _array.Length; i++)
            {
                if (Utilities.ObjectToPtr(item) == Utilities.ObjectToPtr(_array[i]))
                {
                    RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public int Count
        {
            get { return _index; }
        }

        public T this[int i]
        {
            get
            {
                if (i < 0 || i >= _index) throw new System.ArgumentOutOfRangeException("Index was out of range");
                return _array[i];
            }
            set
            {
                if (i < 0 || i >= _index) throw new System.ArgumentOutOfRangeException("Index was out of range");
                _array[i] = value;
            }
        }
    }
}
