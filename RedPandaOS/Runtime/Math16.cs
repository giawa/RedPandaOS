namespace Runtime
{
    public static class Math16
    {
        public static short Divide(short source, short div)
        {
            short remainder = source;
            short quotient = 0;

            while (remainder >= div)
            {
                remainder -= div;
                quotient++;
            }

            return quotient;
        }

        public static short Modulo(short source, short div)
        {
            short remainder = source;
            while (remainder >= div) remainder -= div;
            return remainder;
        }
    }
}
