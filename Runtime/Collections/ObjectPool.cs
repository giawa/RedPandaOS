namespace Runtime.Collections
{
    public class ObjectPool<T> where T : class
    {
        private T[] _array;
        private BitArray _inUse;

        public ObjectPool(T[] array)
        {
            _array = array;
            _inUse = new BitArray(Math32.Divide(array.Length, 32));
        }

        public bool Borrow(out T? item)
        {
            int index = _inUse.IndexOfFirstZero();

            if (index >= 0)
            {
                _inUse[index] = true;
                item = _array[index];
                return true;
            }
            else
            {
                item = null;
                return false;
            }
        }

        public void Return(T item)
        {
            for (int i = 0; i < _array.Length; i++)
            {
                if (_array[i] == item)
                {
                    _inUse[i] = false;
                    return;
                }
            }

            throw new System.Exception("Item did not exist in ObjectPool");
        }
    }
}
