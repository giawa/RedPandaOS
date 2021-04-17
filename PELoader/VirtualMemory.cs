using System;
using System.Collections.Generic;

namespace PELoader
{
    public class VirtualMemory
    {
        public class Section
        {
            public ImageSectionHeader Header;

            public byte[] Data;

            public Section(ImageSectionHeader header, byte[] data)
            {
                Header = header;

                if (data.Length < header.virtualSize)
                {
                    Data = new byte[header.virtualSize];
                    Buffer.BlockCopy(data, 0, Data, 0, data.Length);
                }
                else
                {
                    Data = data;
                }
            }
        }

        public List<Section> Sections = new List<Section>();

        public byte GetByte(uint location)
        {
            foreach (var section in Sections)
            {
                if (section.Header.virtualAddress <= location && section.Header.virtualAddress + section.Header.virtualSize > location)
                {
                    return section.Data[location - section.Header.virtualAddress];
                }
            }

            throw new Exception("Invalid access");
        }

        public byte[] GetBytes(uint location, int length)
        {
            foreach (var section in Sections)
            {
                if (section.Header.virtualAddress <= location && section.Header.virtualAddress + section.Header.virtualSize > location)
                {
                    byte[] data = new byte[length];
                    for (int i = 0; i < length; i++)
                        data[i] = section.Data[location - section.Header.virtualAddress + i];
                    return data;
                }
            }

            throw new Exception("Invalid access");
        }
    }
}
