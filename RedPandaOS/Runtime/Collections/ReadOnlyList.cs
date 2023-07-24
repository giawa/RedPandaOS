namespace Runtime.Collections
{
    public class ReadOnlyList<T>
    {
        private List<T> _list;

        public T this[int a] => _list[a];

        public int Count => _list.Count;

        public ReadOnlyList(List<T> list)
        {
            _list = list;
        }
    }
}
