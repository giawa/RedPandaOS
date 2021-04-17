using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace PELoader
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ImageNTHeaders32
    {
        public uint signature;
        public ImageFileHeader fileHeader;
        public ImageOptionalHeader32 optionalHeader;
    }
}
