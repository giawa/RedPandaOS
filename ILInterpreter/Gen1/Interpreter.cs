using PELoader;
using System;
using System.Collections.Generic;
using System.Text;

namespace ILInterpreter.Gen1
{
    public class Interpreter
    {
        private CLIMetadata _metadata;
        private byte[] _code;
        private int _programCounter;

        private Stack<object> _stack = new Stack<object>();
        private bool _done = false;

        public object ReturnValue { get; private set; }

        private object[] _localVariables = new object[256];

        public void LoadMethod(CLIMetadata metadata, MethodHeader method)
        {
            _metadata = metadata;

            _code = method.Code;
            _programCounter = 0;

            _stack.Clear();
            _done = false;
        }

        private sbyte _sbyte;
        private byte _byte;
        private int _int;
        private uint _uint;

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
                case 0x0009: _stack.Push(_localVariables[3]); break;

                // STLOC
                case 0x000A: _localVariables[0] = _stack.Pop(); break;
                case 0x000B: _localVariables[1] = _stack.Pop(); break;
                case 0x000C: _localVariables[2] = _stack.Pop(); break;
                case 0x000D: _localVariables[3] = _stack.Pop(); break;

                // LDLOC.S
                case 0x0011: _stack.Push(_localVariables[_code[_programCounter++]]); break;

                // LDLOCA.S
                case 0x0012: _stack.Push((IntPtr)_code[_programCounter]); break;    // local variables are at { 32'd0, 32'd0} thru { 32'd0, 32'd255 }

                // STLOC.S
                case 0x0013: _localVariables[_code[_programCounter++]] = _stack.Pop(); break;

                // LDC.I4
                case 0x0016: _stack.Push(0); break;
                case 0x0017: _stack.Push(1); break;
                case 0x0018: _stack.Push(2); break;
                case 0x0019: _stack.Push(3); break;
                case 0x001A: _stack.Push(4); break;
                case 0x001B: _stack.Push(5); break;
                case 0x001C: _stack.Push(6); break;
                case 0x001D: _stack.Push(7); break;
                case 0x001E: _stack.Push(8); break;
                case 0x001F: _stack.Push((int)_code[_programCounter++]); break;

                // LDC.I4.S
                case 0x0020:
                    _stack.Push(BitConverter.ToInt32(_code, _programCounter));
                    _programCounter += 4;
                    break;

                // LDC.R8
                case 0x0023:
                    double temp = BitConverter.ToDouble(_code, _programCounter);
                    _programCounter += 8;
                    _stack.Push(temp);
                    break;

                // CALL
                case 0x0028: CALL(); break;

                // RET
                case 0x002A:
                    ReturnValue = _stack.Peek();
                    _done = true;
                    break;

                // BR.S
                case 0x002B:
                    _sbyte = (sbyte)_code[_programCounter++];
                    _programCounter += _sbyte;
                    break;

                // BRFALSE.S
                case 0x002C:
                    _sbyte = (sbyte)_code[_programCounter++];
                    if (!PopTrue()) _programCounter += _sbyte;
                    break;

                // BRTRUE.S
                case 0x002D:
                    _sbyte = (sbyte)_code[_programCounter++];
                    if (PopTrue()) _programCounter += _sbyte;
                    break;

                // BRTRUE
                case 0x003A:
                    _int = BitConverter.ToInt32(_code, _programCounter);
                    _programCounter += 4;
                    if (PopTrue()) _programCounter += _int;
                    break;

                // ADD
                case 0x0058: ADD(); break;

                // DIV
                case 0x005B: DIV(); break;

                // REM
                case 0x005D: REM(); break;

                // LDSTR
                case 0x0072: LDSTR(); break;

                // CONV
                case 0x0069: _stack.Push(PopToInt()); break;
                case 0x006C: _stack.Push(PopToDouble()); break;

                case 0xFE01: CEQ(); break;  // CEQ
                case 0xFE02: CGT(); break;  // CGT
                case 0xFE04: CLT(); break;  // CLT

                default: throw new Exception("Unknown opcode " + opcode.ToString("X"));
            }
        }

        private void CALL()
        {
            uint methodDesc = BitConverter.ToUInt32(_code, _programCounter);
            _programCounter += 4;

            // the below is a total hack to work with my prime test code, which intercepts the methodDesc
            // for Int32.ToString, Console.Write(string) and Console.WriteLine(string), in that order
            if (methodDesc == 0x0a00000b)
            {
                var addr = (int)(IntPtr)_stack.Pop();
                var localVar = (int)_localVariables[addr];
                _stack.Push(localVar.ToString());
            }
            else if (methodDesc == 0x0a00000c)
            {
                Console.Write(_stack.Pop());
            }
            else if (methodDesc == 0x0a00000d)
            {
                Console.WriteLine(_stack.Pop());
            }
            else
            {
                throw new Exception("Unknown method");
            }
        }

        private void LDSTR()
        {
            ushort addr = BitConverter.ToUInt16(_code, _programCounter);
            _programCounter += 2;
            ushort unknown = BitConverter.ToUInt16(_code, _programCounter);
            _programCounter += 2;

            byte blob = _metadata.US.Heap[addr++];

            if ((blob & 0x80) == 0)
            {
                var bytes = _metadata.US.Heap.AsSpan(addr, blob - 1);
                _stack.Push(Encoding.Unicode.GetString(bytes));
            }
            else
            {
                throw new Exception("No support yet for longer blobs.  See II.24.2.4");
            }
        }

        private bool PopTrue()
        {
            var value = _stack.Pop();

            if (value is int) return (int)value == 1;
            else if (value is IntPtr) return (IntPtr)value != IntPtr.Zero;
            else if (value is bool) return (bool)value;
            else throw new Exception("Unsure how to convert argument");
        }

        private void CEQ()
        {
            var value2 = _stack.Pop();
            var value1 = _stack.Pop();

            bool push1;

            if (value1 is int) push1 = (int)value1 == (int)value2;
            else if (value1 is uint) push1 = (uint)value1 == (uint)value2;
            else if (value1 is double) push1 = (double)value1 == (double)value2;
            else throw new Exception("Unsure how to convert to argument");

            _stack.Push(push1 ? 1 : 0);
        }

        private void CGT()
        {
            var value2 = _stack.Pop();
            var value1 = _stack.Pop();

            bool push1;

            if (value1 is int) push1 = (int)value1 > (int)value2;
            else if (value1 is uint) push1 = (uint)value1 > (uint)value2;
            else if (value1 is double) push1 = (double)value1 > (double)value2;
            else throw new Exception("Unsure how to convert to argument");

            _stack.Push(push1 ? 1 : 0);
        }

        private void CLT()
        {
            var value2 = _stack.Pop();
            var value1 = _stack.Pop();

            bool push1;

            if (value1 is int) push1 = (int)value1 < (int)value2;
            else if (value1 is uint) push1 = (uint)value1 < (uint)value2;
            else if (value1 is double) push1 = (double)value1 < (double)value2;
            else throw new Exception("Unsure how to convert to argument");

            _stack.Push(push1 ? 1 : 0);
        }

        private double PopToDouble()
        {
            var value = _stack.Pop();

            if (value is int) return (int)value;
            else if (value is uint) return (uint)value;
            else if (value is double) return (double)value;
            else throw new Exception("Unsure how to convert to double");
        }

        private int PopToInt()
        {
            var obj = _stack.Pop();

            if (obj is int) return (int)obj;
            else if (obj is uint) return (int)(uint)obj;
            else if (obj is double) return (int)(double)obj;
            else throw new Exception("Unsure how to convert to int");
        }

        private void REM()
        {
            var value2 = _stack.Pop();
            var value1 = _stack.Pop();

            if (value1 is int && value2 is int)
            {
                if ((int)value2 == 0) throw new DivideByZeroException();
                _stack.Push((int)value1 % (int)value2);
            }
            else
            {
                throw new Exception("No remainder for this type yet");
            }
        }

        private void DIV()
        {
            var value2 = _stack.Pop();
            var value1 = _stack.Pop();

            if (value2.GetType() != value1.GetType()) throw new Exception("We don't support addition of different types yet.");

            if (value2 is int && value1 is int)
            {
                _stack.Push((int)value1 / (int)value2);
            }
            else if (value2 is uint && value1 is uint)
            {
                _stack.Push((uint)value1 / (uint)value2);
            }
            else if (value2 is long && value1 is long)
            {
                _stack.Push((long)value1 / (long)value2);
            }
            else if (value2 is ulong && value1 is ulong)
            {
                _stack.Push((ulong)value1 / (ulong)value2);
            }
            else if (value2 is double && value1 is double)
            {
                _stack.Push((double)value1 / (double)value2);
            }
            else
            {
                throw new Exception("No division for this type yet");
            }
        }

        private void ADD()
        {
            var value2 = _stack.Pop();
            var value1 = _stack.Pop();

            if (value2.GetType() != value1.GetType()) throw new Exception("We don't support addition of different types yet.");

            if (value2 is int && value1 is int)
            {
                _stack.Push((int)value2 + (int)value1);
            }
            else if (value2 is uint && value1 is uint)
            {
                _stack.Push((uint)value2 + (uint)value1);
            }
            else if (value2 is long && value1 is long)
            {
                _stack.Push((long)value2 + (long)value1);
            }
            else if (value2 is ulong && value1 is ulong)
            {
                _stack.Push((ulong)value2 + (ulong)value1);
            }
            else if (value2 is double && value1 is double)
            {
                _stack.Push((double)value2 + (double)value1);
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
