using System;

namespace PELoader
{
    public class AssemblyLayout
    {
        public uint hashAlgId;
        public ushort majorVersion, minorVersion, buildNumber, revisionNumber;
        public uint flags;
        public uint publicKey;
        public uint name;
        public uint culture;

        public AssemblyLayout(CLIMetadata metadata, ref int offset)
        {
            hashAlgId = BitConverter.ToUInt32(metadata.Table.Heap, offset);
            offset += 4;

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
                publicKey = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                publicKey = BitConverter.ToUInt16(metadata.Table.Heap, offset);
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
        }

        public string GetName(CLIMetadata metadata)
        {
            return metadata.GetString(name);
        }
    }
}
