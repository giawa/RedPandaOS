using System;
using System.Runtime.InteropServices;

namespace PELoader
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ImageCOR20Header
    {
        public uint cb;
        public ushort majorRuntimeVersion;
        public ushort minorRuntimeVersion;
        public ImageDataDirectory metadata;
        public uint flags;
        public uint entryPointToken;
        public ImageDataDirectory resources;
        public ImageDataDirectory strongNameSignature;
        public ImageDataDirectory codeManagerTable;
        public ImageDataDirectory vTableFixups;
        public ImageDataDirectory exportAddressTableJumps;
        public ImageDataDirectory managedNativeHeader;

        public CLIRuntimeFlags Flags { get { return (CLIRuntimeFlags)flags; } }
    }

    [Flags]
    public enum CLIRuntimeFlags : uint
    {
        COMIMAGE_FLAGS_ILONLY = 0x00000001,
        COMIMAGE_FLAGS_32BITREQUIRED = 0x00000002,
        COMIMAGE_FLAGS_STRONGNAMESIGNED = 0x00000008,
        COMIMAGE_FLAGS_NATIVE_ENTRYPOINT = 0x00000010,
        COMIMAGE_FLAGS_TRACKDEBUGDATA = 0x00010000
    }
}
