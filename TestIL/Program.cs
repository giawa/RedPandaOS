using System;

namespace TestIL
{
    class Program
    {
        static int Main(string[] args)
        {
            for (int check = 1; check < 10000; check++)
            {
                bool isPrime = true;

                if (check <= 1) isPrime = false;
                else if (check == 2) isPrime = true;
                else if ((check % 2) == 0) isPrime = false;
                else
                {
                    for (int i = 3; i < check / 2; i += 2)
                    {
                        if ((check % i) == 0) isPrime = false;
                    }
                }

                if (isPrime)
                {
                    Console.Write(check.ToString());
                    Console.WriteLine(" is Prime!");
                }
            }

            //Console.ReadKey();
            return 1;
        }
    }
}
