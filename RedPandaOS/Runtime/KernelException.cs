using System;

namespace Runtime
{
    public class KernelException : Exception
    {
        private string _message;

        public string Message {  get { return _message; } }

        public KernelException(string message)
            : base(message)
        {

        }
    }
}
