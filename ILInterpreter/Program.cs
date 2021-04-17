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

                // LDC.I4
                case 0x001B: _stack.Push(5); break;
                case 0x001F: _stack.Push((int)_code[_programCounter++]); break;

                // BR.S
                case 0x002B:
                    sbyte offset = (sbyte)_code[_programCounter++];
                    _programCounter += offset;
                    break;

                // LDC.R8
                case 0x0023:
                    double temp = BitConverter.ToDouble(_code, _programCounter);
                    _programCounter += 8;
                    _stack.Push(temp);
                    break;

                // RET
                case 0x002A:
                    ReturnValue = _stack.Peek();
                    _done = true;
                    break;

                // ADD
                case 0x0058: ADD(); break;

                // CONV
                case 0x0069: _stack.Push(PopToInt()); break;
                case 0x006C: _stack.Push(PopToDouble()); break;

                default: throw new Exception("Unknown opcode " + opcode.ToString("X"));
            }
        }

        private double PopToDouble()
        {
            var obj = _stack.Pop();

            if (obj is int) return (int)obj;
            else if (obj is uint) return (uint)obj;
            else if (obj is float) return (float)obj;
            else if (obj is double) return (double)obj;
            else throw new Exception("Unsure how to convert to double");
        }

        private int PopToInt()
        {
            var obj = _stack.Pop();

            if (obj is int) return (int)obj;
            else if (obj is uint) return (int)(uint)obj;
            else if (obj is float) return (int)(float)obj;
            else if (obj is double) return (int)(double)obj;
            else throw new Exception("Unsure how to convert to int");
        }

        private void ADD()
        {
            var object1 = _stack.Pop();
            var object2 = _stack.Pop();

            if (object1.GetType() != object2.GetType()) throw new Exception("We don't support addition of different types yet.");

            if (object1 is int && object2 is int)
            {
                _stack.Push((int)object1 + (int)object2);
            }
            else if (object1 is uint && object2 is uint)
            {
                _stack.Push((uint)object1 + (uint)object2);
            }
            else if (object1 is long && object2 is long)
            {
                _stack.Push((long)object1 + (long)object2);
            }
            else if (object1 is ulong && object2 is ulong)
            {
                _stack.Push((ulong)object1 + (ulong)object2);
            }
            else if (object1 is float && object2 is float)
            {
                _stack.Push((float)object1 + (float)object2);
            }
            else if (object1 is double && object2 is double)
            {
                _stack.Push((double)object1 + (double)object2);
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
