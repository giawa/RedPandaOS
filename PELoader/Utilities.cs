using System;
using System.Runtime.InteropServices;

namespace PELoader
{
    public static class Utilities
    {
        public static T ToStruct<T>(this byte[] bytes) where T : struct
        {
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);

            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }

        public static T ToStruct<T>(this byte[] bytes, int offset, int size) where T : struct
        {
            return MemoryMarshal.Cast<byte, T>(bytes.AsSpan(offset, size))[0];
        }
    }
}
