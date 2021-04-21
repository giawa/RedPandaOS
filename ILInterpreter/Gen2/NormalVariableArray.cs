namespace ILInterpreter.Gen2
{
    public class NormalVariableArray : IVariableArray
    {
        private Variable[] _array = new Variable[256];

        public NormalVariableArray(int maxSize)
        {
            _array = new Variable[maxSize];
        }

        public Variable this[int a] { get { return _array[a]; } set { _array[a] = value; } }

        public Variable Get(int a)
        {
            return _array[a];
        }
    }
}
