namespace Runtime
{
    public static class Math32
    {
        public static int Divide(int source, int div, out int remainder)
        {
            remainder = source;
            int quotient = 0;

            while (remainder >= div)
            {
                remainder -= div;
                quotient++;
            }

            return quotient;
        }

        public static int Divide(int source, int div)
        {
            int remainder = source;
            int quotient = 0;

            while (remainder >= div)
            {
                remainder -= div;
                quotient++;
            }

            return quotient;
        }

        /*public static uint Modulo(uint source, uint div)
        {
            uint remainder = source;
            while (remainder >= div) remainder -= div;
            return remainder;
        }

        public static int Modulo(int source, int div)
        {
            int remainder = source;
            while (remainder >= div) remainder -= div;
            return remainder;
        }*/

        public static int Ceiling(int source, int div)
        {
            var result = Divide(source, div, out int remainder);

            if (remainder > 0) result++;

            return result;
        }

        public static int Floor(int source, int div)
        {
            return Divide(source, div);
        }
    }
}
