using System.Collections.Generic;

namespace ILInterpreter.Gen2
{
    public class NormalVariableStack : IVariableStack
    {
        private Stack<Variable> _stack = new Stack<Variable>();

        public Variable Pop()
        {
            return _stack.Pop();
        }

        public void Push(Variable v)
        {
            _stack.Push(v);
        }

        public Variable Peek()
        {
            return _stack.Peek();
        }

        public void Clear()
        {
            _stack.Clear();
        }
    }
}
