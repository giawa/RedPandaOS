using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace ILInterpreter
{
    public enum ObjType : uint
    {
        Byte = 0,               // Byte and SByte
        Int16 = 1,              // Int16 and UInt16
        Int32 = 2,              // Int32 and UInt32
        Int64 = 3,              // Int64 and UInt64
        F = 4,                  // (Double, Single)
        NativeInt = 5,          // (IntPtr, nint)
        Address = 6,            // &
        TransientPointer = 7,   // *
        // anything past here is a non-value type object
        Object = 8,
        String = 9,
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 8)]
    public struct Variable
    {
        [FieldOffset(0)]
        public ObjType Type;

        [FieldOffset(4)]
        public long Integer;    // stores all integer types, including nativeint, address and transient pointer

        [FieldOffset(4)]
        public double Float;    // stores F types (Double and Single)

        public override string ToString()
        {
            if (Type >= ObjType.Object) return $"Object type {Type.ToString()} at address {Integer}";
            else if (Type == ObjType.F) return $"Floating point with contents {Float}";
            else return $"Integer type {Type.ToString()} with contents {Integer}";
        }

        public Variable(byte value) : this()
        {
            Type = ObjType.Byte;
            Integer = value;
        }

        public Variable(short value) : this()
        {
            Type = ObjType.Int16;
            Integer = value;
        }

        public Variable(int value) : this()
        {
            Type = ObjType.Int32;
            Integer = value;
        }

        public Variable(long value) : this()
        {
            Type = ObjType.Int64;
            Integer = value;
        }

        public Variable(long value, ObjType type) : this()
        {
            Type = type;
            Integer = value;
        }

        public Variable(IntPtr value) : this()
        {
            Type = ObjType.NativeInt;
            Integer = (long)value;
        }

        public Variable(double value) : this()
        {
            Type = ObjType.F;
            Float = value;
        }

        public static Variable Zero = new Variable(0);
        public static Variable One = new Variable(1);
    }

    public interface IVariableStack
    {
        Variable Pop();
        void Push(Variable v);
    }

    public interface IVariableArray
    {
        Variable Get(int a);
    }

    public interface IInterpreter
    {
        IVariableStack Stack { get; }
        IVariableArray LocalVariables { get; }
        List<string> StringHeap { get; }
    }
}
