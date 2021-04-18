using System;

namespace PELoader
{
    public class StandAloneSigLayout
    {
        public uint signature;

        public StandAloneSigLayout(CLIMetadata metadata, ref int offset)
        {
            if (metadata.WideBlob)
            {
                signature = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                signature = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }
        }

        public string GetSignature(CLIMetadata metadata)
        {
            return metadata.GetBlob(signature);
        }
    }
}
