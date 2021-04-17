using PELoader;
using System;
using System.Collections.Generic;

namespace ILInterpreter
{
    class Program
    {
        static void Main(string[] args)
        {
            PortableExecutableFile file = new PortableExecutableFile(@"..\..\..\..\TestIL\bin\Debug\netcoreapp3.1\TestIL.dll");

            uint rvaOffset = 0x2050;
            MethodHeader method = new MethodHeader(file.Memory, ref rvaOffset);

            Interpreter interpreter = new Interpreter();
            interpreter.LoadMethod(method);
            interpreter.Execute();

            Console.WriteLine($"Interpreted method returned: {interpreter.ReturnValue}");
            Console.ReadKey();
        }
    }

    public class Interpreter
    {
        private byte[] _code;
        private int _programCounter;

        private Stack<object> _stack = new Stack<object>();
        private bool _done = false;

        public object ReturnValue { get; private set; }

        private object[] _localVariables = new object[15];

        public void LoadMethod(MethodHeader method)
        {
            _code = method.Code;
            _programCounter = 0;

            _stack.Clear();
            _done = false;
        }

        public void ExecuteOpcode()
        {
            int opcode = _code[_programCounter++];

            if (opcode == 0xfe) opcode = (opcode << 8) | _code[_programCounter++];

            switch (opcode)
            {
                case 0x0000: break; // NOP

                // LDLOC
                case 0x0006: _stack.Push(_localVariables[0]); break;
                case 0x0007: _stack.Push(_localVariables[1]); break;
                case 0x0008: _stack.Push(_localVariables[2]); break;

                // STLOC
                case 0x000A: _localVariables[0] = _stack.Pop(); break;
                case 0x000B: _localVariables[1] = _stack.Pop(); break;
                case 0x000C: _localVariables[2] = _stack.Pop(); break;

                // ADD
                case 0x0058: ADD(); break;

                // LDC.I4
                case 0x001B: _stack.Push(5); break;
                case 0x001F: _stack.Push((int)_code[_programCounter++]); break;

                // BR.S
                case 0x002B:
                    sbyte offset = (sbyte)_code[_programCounter++];
                    _programCounter += offset;
                    break;

                // RET
                case 0x002A:
                    ReturnValue = _stack.Peek();
                    _done = true;
                    break;
                default: throw new Exception("Unknown opcode " + opcode);
            }
        }

        private void ADD()
        {
            var object1 = _stack.Pop();
            var object2 = _stack.Pop();

            if (object1 is int && object2 is int)
            {
                _stack.Push((int)object1 + (int)object2);
            }
            else
            {
                throw new Exception("No addition for this type yet");
            }
        }

        public void Execute()
        {
            while (!_done)
            {
                ExecuteOpcode();
            }
        }
    }
}
