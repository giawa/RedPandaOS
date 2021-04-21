using PELoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ILInterpreter.Gen2
{
    public class Interpreter : IInterpreter
    {
        private CLIMetadata _metadata;
        private byte[] _code;
        private int _programCounter;

        private NormalVariableStack _stack = new NormalVariableStack();
        private bool _done = false;

        public object ReturnValue { get; private set; }

        private NormalVariableArray _localVariables = new NormalVariableArray(256);

        public IVariableArray LocalVariables { get { return _localVariables; } }
        public IVariableStack Stack { get { return _stack; } }
        public List<string> StringHeap { get { return _stringHeap; } }

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
                case 0x0012: _stack.Push(new Variable((IntPtr)_code[_programCounter])); break;  // local variables are at { 32'd0, 32'd0} thru { 32'd0, 32'd255 }

                // STLOC.S
                case 0x0013: _localVariables[_code[_programCounter++]] = _stack.Pop(); break;

                // LDC.I4
                case 0x0016: _stack.Push(Variable.Zero); break;
                case 0x0017: _stack.Push(Variable.One); break;
                case 0x0018: _stack.Push(new Variable(2)); break;
                case 0x0019: _stack.Push(new Variable(3)); break;
                case 0x001A: _stack.Push(new Variable(4)); break;
                case 0x001B: _stack.Push(new Variable(5)); break;
                case 0x001C: _stack.Push(new Variable(6)); break;
                case 0x001D: _stack.Push(new Variable(7)); break;
                case 0x001E: _stack.Push(new Variable(8)); break;
                case 0x001F: _stack.Push(new Variable((int)_code[_programCounter++])); break;

                // LDC.I4.S
                case 0x0020:
                    _stack.Push(new Variable(BitConverter.ToInt32(_code, _programCounter)));
                    _programCounter += 4;
                    break;

                // LDC.R8
                case 0x0023:
                    double temp = BitConverter.ToDouble(_code, _programCounter);
                    _programCounter += 8;
                    _stack.Push(new Variable(temp));
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
                //case 0x0069: _stack.Push(PopToInt()); break;
                case 0x006C: _stack.Push(PopToDouble()); break;

                case 0xFE01: CEQ(); break;  // CEQ
                case 0xFE02: CGT(); break;  // CGT
                case 0xFE04: CLT(); break;  // CLT

                default: throw new Exception("Unknown opcode " + opcode.ToString("X"));
            }
        }

        private List<string> _stringHeap = new List<string>();

        public Variable AllocString(string s)
        {
            int addr = -1;

            if (_stringHeap.Contains(s)) addr = _stringHeap.IndexOf(s); // TODO:  Really slow, O(2n)
            else _stringHeap.Add(s);

            if (addr == -1) addr = _stringHeap.Count - 1;

            return new Variable()
            {
                Float = 0,
                Integer = addr,
                Type = ObjType.String
            };
        }

        private Dictionary<uint, Action> _pluggedMethods = new Dictionary<uint, Action>();
        private MemberInfo[] _possiblePlugs = null;

        private void CALL()
        {
            uint methodDesc = BitConverter.ToUInt32(_code, _programCounter);
            _programCounter += 4;

            if (_pluggedMethods.TryGetValue(methodDesc, out var action))
            {
                action();
                return;
            }

            if ((methodDesc & 0xff000000) == 0x0a000000)
            {
                var memberRef = _metadata.MemberRefs[(int)(methodDesc & 0x00ffffff) - 1];
                var memberName = memberRef.ToString();

                // search for a method to plug this
                var assembly = Assembly.GetExecutingAssembly();

                if (_possiblePlugs == null) _possiblePlugs = assembly.GetTypes().SelectMany(t => t.GetMethods()).Where(m => m.GetCustomAttribute(typeof(PlugAttribute)) != null).ToArray();

                foreach (var plug in _possiblePlugs)
                {
                    if (plug.Name == memberRef.Name)
                    {
                        var plugAttribute = plug.GetCustomAttribute<PlugAttribute>();
                        if (plugAttribute.IsEquivalent(memberRef))
                        {
                            var invokableMethod = (MethodInfo)plug;
                            _pluggedMethods.Add(methodDesc, () => invokableMethod.Invoke(null, new object[] { this }));
                        }
                    }
                }

                if (!_pluggedMethods.ContainsKey(methodDesc))
                    throw new NotImplementedException($"The method {memberRef.ToPrettyString()} is not yet implemented.");

                _pluggedMethods[methodDesc]();
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
                _stack.Push(AllocString(Encoding.Unicode.GetString(bytes)));
            }
            else
            {
                throw new Exception("No support yet for longer blobs.  See II.24.2.4");
            }
        }

        private bool PopTrue()
        {
            var value = _stack.Pop();

            if (value.Type == ObjType.F || value.Type >= ObjType.Object)
                throw new InvalidOperationException();

            return value.Integer == 1;
        }

        private void CEQ()
        {
            var value2 = _stack.Pop();
            var value1 = _stack.Pop();

            bool push1;

            if (value1.Type != value2.Type) throw new InvalidOperationException();

            if (value1.Type == ObjType.F) push1 = value1.Float == value2.Float;
            else push1 = value1.Integer == value2.Integer;

            _stack.Push(push1 ? Variable.One : Variable.Zero);
        }

        private void CGT()
        {
            var value2 = _stack.Pop();
            var value1 = _stack.Pop();

            bool push1;

            if (value1.Type != value2.Type) throw new InvalidOperationException();

            if (value1.Type == ObjType.F) push1 = value1.Float > value2.Float;
            else push1 = value1.Integer > value2.Integer;

            _stack.Push(push1 ? Variable.One : Variable.Zero);
        }

        private void CLT()
        {
            var value2 = _stack.Pop();
            var value1 = _stack.Pop();

            bool push1;

            if (value1.Type == ObjType.F) push1 = value1.Float < value2.Float;
            else push1 = value1.Integer < value2.Integer;

            _stack.Push(push1 ? Variable.One : Variable.Zero);
        }

        private Variable PopToDouble()
        {
            var value = _stack.Pop();

            if (value.Type == ObjType.F) return value;
            else return new Variable((double)value.Integer);
        }

        /*private Variable PopToInt()
        {
            var value = _stack.Pop();

            if (value.Type == ObjType.F) return (int)

            if (obj is int) return (int)obj;
            else if (obj is uint) return (int)(uint)obj;
            else if (obj is double) return (int)(double)obj;
            else throw new Exception("Unsure how to convert to int");
        }*/

        private void REM()
        {
            var value2 = _stack.Pop();
            var value1 = _stack.Pop();

            if (value1.Type != value2.Type) throw new InvalidOperationException();

            if (value1.Type == ObjType.F)
            {
                value1.Float = value1.Float % value2.Float;
                _stack.Push(value1);
            }
            else
            {
                value1.Integer = value1.Integer % value2.Integer;
                _stack.Push(value1);
            }
        }

        private void DIV()
        {
            var value2 = _stack.Pop();
            var value1 = _stack.Pop();

            if (value1.Type != value2.Type) throw new InvalidOperationException();

            if (value1.Type == ObjType.F)
            {
                value1.Float = value1.Float / value2.Float;
                _stack.Push(value1);
            }
            else
            {
                value1.Integer = value1.Integer / value2.Integer;
                _stack.Push(value1);
            }
        }

        private void ADD()
        {
            var value2 = _stack.Pop();
            var value1 = _stack.Pop();

            // todo:  Some different types can be added together.  See here:
            // https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.add?view=net-5.0
            if (value1.Type != value2.Type) throw new InvalidOperationException();

            if (value1.Type == ObjType.F)
            {
                value1.Float = value1.Float + value2.Float;
                _stack.Push(value1);
            }
            else
            {
                value1.Integer = value1.Integer + value2.Integer;
                _stack.Push(value1);
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
