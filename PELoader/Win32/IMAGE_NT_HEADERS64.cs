using System.Runtime.InteropServices;

namespace PELoader
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ImageNTHeaders64
    {
        public uint signature;
        public ImageFileHeader fileHeader;
        public ImageOptionalHeader64 optionalHeader;
    }
}
