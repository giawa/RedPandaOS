using System;

namespace PELoader
{
    public class AssemblyRefLayout
    {
        public ushort majorVersion, minorVersion, buildNumber, revisionNumber;
        public uint flags;
        public uint publicKeyOrToken;
        public uint name;
        public uint culture;
        public uint hashValue;

        public AssemblyRefLayout(CLIMetadata metadata, ref int offset)
        {
            majorVersion = BitConverter.ToUInt16(metadata.Table.Heap, offset);
            offset += 2;

            minorVersion = BitConverter.ToUInt16(metadata.Table.Heap, offset);
            offset += 2;

            buildNumber = BitConverter.ToUInt16(metadata.Table.Heap, offset);
            offset += 2;

            revisionNumber = BitConverter.ToUInt16(metadata.Table.Heap, offset);
            offset += 2;

            flags = BitConverter.ToUInt32(metadata.Table.Heap, offset);
            offset += 4;

            if (metadata.WideBlob)
            {
                publicKeyOrToken = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                publicKeyOrToken = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            if (metadata.WideStrings)
            {
                name = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;

                culture = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                name = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;

                culture = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            if (metadata.WideBlob)
            {
                hashValue = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                hashValue = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }
        }

        public string GetName(CLIMetadata metadata)
        {
            return metadata.GetString(name);
        }
    }
}
