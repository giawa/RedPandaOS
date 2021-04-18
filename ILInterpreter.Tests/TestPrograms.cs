using Microsoft.VisualStudio.TestTools.UnitTesting;
using ILInterpreter;

namespace ILInterpreter.Tests
{
    [TestClass]
    public class TestPrograms
    {
        [TestMethod]
        public void IsPrime()
        {
            // the following CLI is the (debug) compiled version of this C# code:
            //int check = 13;
            //bool isPrime = true;

            //if (check <= 1) isPrime = false;
            //else if (check == 2) isPrime = true;
            //else if ((check % 2) == 0) isPrime = false;
            //else
            //{
            //    for (int i = 3; i < check / 2; i += 2)
            //    {
            //        if ((check % i) == 0) isPrime = false;
            //    }
            //}

            //return isPrime ? 1 : 0;

            // checks if 13 is prime
            byte[] cli = new byte[]
            { 
                0, 31, 13, 10, 0, 23, 11, 6, 23, 254, 2, 22, 254, 1, 12, 8, 44, 4, 22, 11, 43, 71, 6, 
                24, 254, 1, 13, 9, 44, 4, 23, 11, 43, 59, 6, 24, 93, 22, 254, 1, 19, 4, 17, 4, 44, 4, 
                22, 11, 43, 43, 0, 25, 19, 5, 43, 23, 0, 6, 17, 5, 93, 22, 254, 1, 19, 6, 17, 6, 44, 
                2, 22, 11, 0, 17, 5, 24, 88, 19, 5, 17, 5, 6, 24, 91, 254, 4, 19, 7, 17, 7, 45, 220, 
                0, 7, 45, 3, 22, 43, 1, 23, 19, 8, 43, 0, 17, 8, 42 
            };

            Interpreter interpreter = new Interpreter();
            interpreter.LoadMethod(null, new PELoader.MethodHeader(cli));
            interpreter.Execute();

            Assert.IsTrue(interpreter.ReturnValue is int && (int)interpreter.ReturnValue == 1);

            // check if 14 is prime
            cli[2] = 14;

            interpreter.LoadMethod(null, new PELoader.MethodHeader(cli));
            interpreter.Execute();

            Assert.IsTrue(interpreter.ReturnValue is int && (int)interpreter.ReturnValue == 0);
        }
    }
}
