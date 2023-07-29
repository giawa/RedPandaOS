namespace Runtime.Collections
{
    public class Queue<T>
    {
        private T[] _array;
        private int _head, _count;

        public int Count { get { return _count; } }

        public Queue(int capacity)
        {
            _array = new T[capacity];

            _head = 0;
            _count = 0;
        }

        public bool TryEnqueue(T item)
        {
            if (_count == _array.Length) return false;

            var index = (_head + _count) % _array.Length;
            _array[index] = item;
            _count++;

            return true;
        }

        public T Dequeue()
        {
            if (_count == 0) return _array[0];

            T item = _array[_head];
            _head = (_head + 1) % _array.Length;
            _count--;

            return item;
        }

        public T Peek()
        {
            return _array[_head];
        }
    }
}
