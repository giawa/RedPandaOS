namespace Runtime.Collections
{
    public class PtrDictionary<T>
    {
        private List<uint> _ptrs;
        private List<T> _objects;

        public PtrDictionary(int capacity)
        {
            _ptrs = new List<uint>(capacity);
            _objects = new List<T>(capacity);
        }

        public bool Contains(uint ptr)
        {
            for (int i = 0; i < _ptrs.Count; i++)
            {
                if (_ptrs[i] == ptr) return true;
            }
            return false;
        }

        public void Add(uint ptr, T obj)
        {
            if (Contains(ptr)) return;

            _ptrs.Add(ptr);
            _objects.Add(obj);
        }

        public void Remove(uint ptr)
        {
            for (int i = 0; i < _ptrs.Count; i++)
            {
                if (_ptrs[i] == ptr)
                {
                    _ptrs.RemoveAt(i);
                    _objects.RemoveAt(i);
                    return;
                }
            }
        }

        public T this[uint ptr]
        {
            get
            {
                for (int i = 0; i < _ptrs.Count; i++)
                {
                    if (_ptrs[i] == ptr) return _objects[i];
                }
                return Kernel.Memory.Utilities.PtrToObject<T>(0);
            }
        }
    }
}
