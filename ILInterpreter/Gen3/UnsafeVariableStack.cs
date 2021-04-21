using System;
using System.Runtime.InteropServices;

namespace ILInterpreter.Gen3
{
    public unsafe class UnsafeVariableStack : IVariableStack
    {
        private IntPtr _stack;
        private int _index = 0;
        private int _maxStack;

        public UnsafeVariableStack(int maxStack)
        {
            _maxStack = maxStack;
            _stack = Marshal.AllocHGlobal(sizeof(Variable) * maxStack);
        }

        ~UnsafeVariableStack()
        {
            Marshal.FreeHGlobal(_stack);
        }

        public Variable* PopFast()
        {
            if (_index == 0) throw new IndexOutOfRangeException();

            --_index;
            return (Variable*)_stack + _index;
        }

        public Variable Pop()
        {
            var existing = PopFast();

            Variable ret = new Variable()
            {
                Integer = existing->Integer,
                Type = existing->Type
            };

            return ret;
        }

        public void Push(Variable value)
        {
            if (_index >= _maxStack) throw new IndexOutOfRangeException();

            var existing = (Variable*)_stack + _index;

            existing->Type = value.Type;
            existing->Integer = value.Integer;

            _index++;
        }

        public void PushInt32(int value)
        {
            if (_index >= _maxStack) throw new IndexOutOfRangeException();

            var existing = (Variable*)_stack + _index;

            existing->Type = ObjType.Int32;
            existing->Integer = value;

            _index++;
        }

        public Variable* PeekFast()
        {
            if (_index == 0) throw new IndexOutOfRangeException();

            return (Variable*)_stack + _index - 1;
        }

        public Variable Peek()
        {
            var existing = PeekFast();

            Variable ret = new Variable()
            {
                Integer = existing->Integer,
                Type = existing->Type
            };

            return ret;
        }
    }

    public unsafe class UnsafeVariableStackVer2 : IVariableStack
    {
        private IntPtr _stack;
        private Variable* _pos;
        private int _maxStack;

        public UnsafeVariableStackVer2(int maxStack)
        {
            _maxStack = maxStack;
            _stack = Marshal.AllocHGlobal(sizeof(Variable) * maxStack);
            _pos = (Variable*)_stack;
        }

        ~UnsafeVariableStackVer2()
        {
            Marshal.FreeHGlobal(_stack);
        }

        public Variable* PopFast()
        {
            _pos--;
            return _pos;
        }

        public Variable Pop()
        {
            var existing = PopFast();

            Variable ret = new Variable()
            {
                Integer = existing->Integer,
                Type = existing->Type
            };

            return ret;
        }

        public void Push(Variable value)
        {
            _pos->Type = value.Type;
            _pos->Integer = value.Integer;

            _pos++;
        }

        public void PushFast(Variable* value)
        {
            _pos->Type = value->Type;
            _pos->Integer = value->Integer;

            _pos++;
        }

        public void PushInt32(int value)
        {
            _pos->Type = ObjType.Int32;
            _pos->Integer = value;

            _pos++;
        }

        public Variable* PeekFast()
        {
            return _pos - 1;
        }

        public Variable Peek()
        {
            var existing = PeekFast();

            Variable ret = new Variable()
            {
                Integer = existing->Integer,
                Type = existing->Type
            };

            return ret;
        }
    }
}
