namespace Runtime.Collections
{
    public class List<T>
    {
        private T[] _array;
        private int _index = 0;

        public List()
        {

        }

        public List(int capacity)
        {
            Initialize(capacity);
        }

        public void Initialize(int capacity)
        {
            _array = new T[capacity];
        }

        public void Add(T item)
        {
            if (_array == null || _index >= _array.Length) return;

            _array[_index] = item;
            _index++;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _index) return;

            for (int i = index; i < _index - 1; i++)
            {
                _array[i] = _array[i + 1];
            }

            _index--;
        }

        public int Count
        {
            get { return _index; }
        }

        public T this[int i]
        {
            get
            {
                if (_array == null || i < 0 || i >= _index) return _array[0];
                return _array[i];
            }
            set
            {
                if (_array == null || i < 0 || i >= _index) return;
                _array[i] = value;
            }
        }
    }
}
