using System;
using System.Runtime.InteropServices;

namespace ILInterpreter.Gen3
{
    public unsafe class UnsafeVariableArray
    {
        private Variable* _array;

        public Variable* this[int a]
        {
            get { return _array + a; }
            set
            {
                (_array + a)->Type = value->Type;
                (_array + a)->Integer = value->Integer;
            }
        }

        public UnsafeVariableArray(int maxSize)
        {
            _array = (Variable*)Marshal.AllocHGlobal(sizeof(Variable) * maxSize);
        }

        ~UnsafeVariableArray()
        {
            Marshal.FreeHGlobal((IntPtr)_array);
        }
    }
}
