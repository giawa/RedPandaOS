using System;
using PELoader;

namespace ILInterpreter.Plugs
{
    public static class SystemConsolePlugs
    {
        [Plug("System.Console", new ElementType.EType[] { ElementType.EType.String })]
        public static void Write(IInterpreter interpreter)
        {
            var s = interpreter.Stack.Pop();
            if (s.Type != ObjType.String) throw new InvalidOperationException();
            Console.Write(interpreter.StringHeap[(int)s.Integer]);
        }

        [Plug("System.Console", new ElementType.EType[] { ElementType.EType.String })]
        public static void WriteLine(IInterpreter interpreter)
        {
            var s = interpreter.Stack.Pop();
            if (s.Type != ObjType.String) throw new InvalidOperationException();
            Console.WriteLine(interpreter.StringHeap[(int)s.Integer]);
        }

        [Plug("System.Console", ElementType.EType.ValueType)]
        public static void ReadKey(IInterpreter interpreter)
        {
            Console.ReadKey();
            interpreter.Stack.Push(new Variable(0));
        }
    }
}
