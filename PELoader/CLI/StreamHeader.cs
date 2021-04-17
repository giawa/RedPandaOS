using System;
using System.Text;

namespace PELoader
{
    public class StreamHeader
    {
        public uint Offset;
        public uint Size;
        public string Name;

        public byte[] Heap;

        public StreamHeader(byte[] data, ref uint offset)
        {
            Offset = BitConverter.ToUInt32(data, (int)offset);
            Size = BitConverter.ToUInt32(data, (int)offset + 4);

            offset += 8;

            StringBuilder name = new StringBuilder();
            while (offset < data.Length && data[offset] != 0)
                name.Append((char)data[offset++]);
            Name = name.ToString();

            // move to the next 4-byte boundary
            offset += (4 - (offset % 4));
        }

        public void ReadHeap(VirtualMemory memory, uint metadataOffset)
        {
            Heap = memory.GetBytes(metadataOffset + Offset, (int)Size);

            /*if (Name == "#Strings")
            {
                for (int i = 0; i < Heap.Length; i++)
                {
                    if (Heap[i] != 0) Console.Write((char)Heap[i]);
                    else Console.WriteLine();
                }
            }*/
        }

        public override string ToString()
        {
            return $"{Name} heap contains {Size} bytes.";
        }
    }
}
