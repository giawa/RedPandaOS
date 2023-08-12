using Runtime.Collections;
using System;
using System.Runtime.InteropServices;

namespace Runtime.Memory
{
    public class StackHeap
    {
        private uint _baseAddress;
        private uint _size;

        private uint _index;

        public uint Allocated { get { return _index; } }

        public StackHeap(uint baseAddress, uint size)
        {
            _baseAddress = baseAddress;
            _size = size;

            _index = 0;
        }

        public uint Push()
        {
            if (_index + 4 > _size) throw new Exception("StackHeap grew too large");

            uint index = _index;
            _index += 4;

            return _baseAddress + index;
        }

        public void Pop()
        {
            _index -= 4;

            if (_index < 0) throw new Exception("StackHeap became unbalanced");
        }

        public T[] Push<T>(int length, int sizePerElement)
        {
            uint size = (uint)(8 + length * sizePerElement);

            if (_index + size > _size) throw new Exception("StackHeap grew too large");

            uint index = _index;
            _index += size;

            CPUHelper.CPU.WriteMemInt(_baseAddress + index, (uint)length);
            CPUHelper.CPU.WriteMemInt(_baseAddress + index + 4, (uint)sizePerElement);

            return Memory.Utilities.PtrToObject<T[]>(_baseAddress + index);
        }

        public void Pop<T>(T[] item)
        {
            // we know an array is built of length, stride, items
            uint ptr = Memory.Utilities.ObjectToPtr(item);
            uint size = CPUHelper.CPU.ReadMemInt(ptr) * CPUHelper.CPU.ReadMemInt(ptr + 4) + 8;

            _index -= size;

            if (_index < 0) throw new Exception("StackHeap became unbalanced");
        }
    }

    public class GCPtr
    {
        public uint Address { get; internal set; }
        public uint Size { get; internal set; }

        internal GCPtr() { }

        internal void Set(uint index, uint size)
        {
            Address = index;
            Size = size;
        }

        public T GetObject<T>()
        {
            return Utilities.PtrToObject<T>(Address);
        }
    }

    public class GCHeap
    {
        private uint _baseAddress;
        private uint _size;

        private BitArray _inUse;    // we store as dwords

        private ObjectPool<GCPtr> _ptrs;

        public GCHeap(uint baseAddress, uint size)
        {
            _baseAddress = baseAddress;
            _size = size;

            _inUse = new BitArray((int)(_size / 4 / 32));
            GCPtr[] gcPtrs = new GCPtr[size / 4 / 16];  // TODO: Instrument this?
            for (int i = 0; i < gcPtrs.Length; i++) gcPtrs[i] = new GCPtr();
            _ptrs = new ObjectPool<GCPtr>(gcPtrs);
        }

        public void Free(GCPtr ptr)
        {
            for (int i = 0; i < ptr.Size / 4; i++)
            {
                _inUse[(int)(ptr.Address / 4) + i] = false;
            }
            _ptrs.Return(ptr);
        }

        public GCPtr Allocate(uint size)
        {
            size = (size + 3) / 4;  // we need the size in terms of dwords

            if (!_ptrs.Borrow(out var gcPtr) && gcPtr != null)
            {
                var address = FindAddressOfEmptyRegion(size);

                if (address == uint.MaxValue) throw new Exception("Could not find large enough region");

                gcPtr.Set(address, size);
                return gcPtr;
            }
            else throw new Exception("Could not get a GCPtr");
        }

        private uint FindAddressOfEmptyRegion(uint size)
        {
            int newAddr = 0;

            while (newAddr < _inUse.Length)
            {
                newAddr = _inUse.IndexOfNextZero(newAddr);

                if (newAddr == -1) break;

                // make sure we actually have enough consecutive free words
                bool works = true;
                for (int i = newAddr; i < newAddr + (int)(size >> 2); i++)
                {
                    if (i >= (_inUse.Length << 5) || _inUse[i])
                    {
                        works = false;
                        newAddr = i;
                        break;
                    }
                }

                // if not enough consecutive words then keep searching
                if (!works) continue;

                // otherwise we found a contiguous memory region, so mark it as in use
                for (int i = newAddr; i < newAddr + (int)size / 4; i++)
                    _inUse[i] = true;

                //Available -= size;

                return (uint)newAddr * 4 + _baseAddress;
            }

            return uint.MaxValue;
        }
    }
}