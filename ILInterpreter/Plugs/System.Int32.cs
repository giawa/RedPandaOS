using PELoader;
using System;

namespace ILInterpreter.Plugs
{
    public static class SystemInt32Plugs
    {
        [Plug("System.Int32", ElementType.EType.String, SigFlags.HASTHIS)]
        public static void ToString(IInterpreter interpreter)
        {
            var addr = interpreter.Stack.Pop().Integer;
            var localVar = interpreter.LocalVariables.Get((int)addr).Integer;
            interpreter.Stack.Push(interpreter.AllocString(localVar.ToString()));
        }
    }
}
