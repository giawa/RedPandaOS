using PELoader;

namespace IL2Asm.Assembler
{
    public class DataType
    {
        public ElementType Type;
        public object Data;

        public DataType(ElementType type, object data)
        {
            Type = type;
            Data = data;
        }

        public DataType(ElementType.EType type, object data)
        {
            Type = new ElementType(type);
            Data = data;
        }
    }
}
