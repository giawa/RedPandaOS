using IL2Asm.BaseTypes;

namespace Kernel.Memory
{
    public interface IHeapAllocator
    {
        public uint Malloc(uint size, uint init = 0);

        public uint MallocPageAligned(uint size, uint init = 0);

        public void Free(uint addr);

        public T Malloc<T>();

        public T Malloc<T>(uint size);

        public T[] MallocArray<T>(uint arraySize);
    }
}
