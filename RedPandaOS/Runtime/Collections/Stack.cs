namespace Runtime.Collections
{
    public class Stack<T>
    {
        private T[] _array;
        private int _count;

        public int Count { get { return _count; } }

        public Stack(int capacity)
        {
            _array = new T[capacity];
        }

        public bool TryPush(T item)
        {
            if (_count >= _array.Length) return false;

            _array[_count++] = item;
            return true;
        }

        public T Pop()
        {
            if (_count == 0) return _array[0];

            return _array[_count--];
        }

        public T Peek()
        {
            return _array[_count];
        }
    }
}
