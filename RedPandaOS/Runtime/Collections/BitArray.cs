namespace Runtime.Collections
{
    public class BitArray
    {
        public uint[] _array;

        public BitArray(int capacity)
        {
            // store 32 bits per uint
            _array = new uint[Math32.Ceiling(capacity, 32)];
        }

        public bool this[int index]
        {
            get
            {
                uint t = _array[index >> 5];
                int i = index & 0x1f;
                return ((t >> i) & 0x01) != 0;
            }
            set
            {
                uint t = _array[index >> 5];
                int i = index & 0x1f;

                if (value) t |= (1U << i);
                else t &= ~(1U << i);

                _array[index >> 5] = t;
            }
        }

        public int IndexOfFirstOne()
        {
            for (int i = 0; i < _array.Length; i++)
            {
                var t = _array[i];
                if (t == 0) continue;

                for (int j = 0; j < 32; j++)
                {
                    if ((t & (1U << j)) != 0) return (i * 32 + j);
                }
            }

            return -1;
        }

        public int IndexOfFirstZero()
        {
            for (int i = 0; i < _array.Length; i++)
            {
                var t = _array[i];
                if (t == 0xffffffff) continue;

                for (int j = 0; j < 32; j++)
                {
                    if ((t & (1U << j)) == 0) return (i * 32 + j);
                }
            }

            return -1;
        }

        public int IndexOfNextZero(int startIndex)
        {
            for (int i = (startIndex >> 5); i < _array.Length; i++)
            {
                var t = _array[i];
                if (t == 0xffffffff) continue;

                int j = (i == (startIndex >> 5) ? startIndex & 0x1f : 0);

                for (; j < 32; j++)
                {
                    if ((t & (1U << j)) == 0) return (i * 32 + j);
                }

            }

            return -1;
        }
    }
}
