using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PELoader
{
    public class CLIMetadata
    {
        private StreamHeader[] _streamHeaders;
        private MetadataLayout _metadataLayout;

        public StreamHeader Strings { get; private set; }
        public StreamHeader US { get; private set; }
        public StreamHeader Blob { get; private set; }

        public CLIMetadata(PortableExecutableFile peFile, ImageDataDirectory cliImageDataDirectory)
        {
            // read in the CLI header, _directories[IMAGE_DIRECTORY_ENTRY_COM_DESCRIPTOR]
            var cliHeader = peFile.Memory.GetBytes(cliImageDataDirectory.virtualAddress, Marshal.SizeOf<ImageCOR20Header>()).ToStruct<ImageCOR20Header>();

            // read in the metadata root
            byte[] metadata = peFile.Memory.GetBytes(cliHeader.metadata.virtualAddress, (int)cliHeader.metadata.size);
            var metadataRoot = metadata.ToStruct<MetadataRoot>(0, Marshal.SizeOf<MetadataRoot>());

            // verify the version looks appropriate
            /*StringBuilder version = new StringBuilder();
            for (int i = 0; i < metadataRoot.versionLength; i++)
            {
                if (metadata[16 + i] == 0) break;
                version.Append((char)metadata[16 + i]);
            }*/

            ushort metadataStreams = BitConverter.ToUInt16(metadata, 18 + (int)metadataRoot.versionLength);
            uint metadataOffset = 20 + metadataRoot.versionLength;
            _streamHeaders = new StreamHeader[metadataStreams];

            for (int i = 0; i < _streamHeaders.Length; i++)
            {
                _streamHeaders[i] = new StreamHeader(metadata, ref metadataOffset);
                _streamHeaders[i].ReadHeap(peFile.Memory, cliHeader.metadata.virtualAddress);

                if (_streamHeaders[i].Name == "#~")
                {
                    _metadataLayout = _streamHeaders[i].Heap.ToStruct<MetadataLayout>(0, Marshal.SizeOf<MetadataLayout>());
                }
                else if (_streamHeaders[i].Name == "#Strings")
                {
                    Strings = _streamHeaders[i];
                }
                else if (_streamHeaders[i].Name == "#US")
                {
                    US = _streamHeaders[i];
                }
                else if (_streamHeaders[i].Name == "#Blob")
                {
                    Blob = _streamHeaders[i];
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MetadataLayout
    {
        public uint reserved;
        public byte majorVersion;
        public byte minorVersion;
        public byte heapSizes;
        public byte reserved2;
        public ulong valid;
        public ulong sorted;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MetadataRoot
    {
        public uint signature;
        public ushort majorVersion;
        public ushort minorVersion;
        public uint reserved;
        public uint versionLength;
    }
}
