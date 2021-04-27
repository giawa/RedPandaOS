namespace TestIL
{
    public static class MathHelper
    {
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

        public static int Modulo(int source, int div)
        {
            int remainder = source;
            while (remainder >= div) remainder -= div;
            return remainder;
        }
    }
}
