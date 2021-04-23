using System;

namespace PELoader
{
    public class LocalVarSig
    {
        /*public SigFlags Flags { get; private set; }
        public uint ParamCount { get; private set; }
        public ElementType RetType { get; private set; }
        public ElementType[] Params { get; private set; }*/
        public ElementType[] LocalVariables { get; private set; }

        public LocalVarSig(CLIMetadata metadata, uint addr)
        {
            // find the StandAloneSig row
            var sig = metadata.StandAloneSigs[((int)addr & 0x00ffffff) - 1];

            var blob = metadata.GetBlob(sig.signature);

            if (blob[0] != 0x07) throw new Exception("Invalid LOCAL_SIG (II.23.2.6)");

            LocalVariables = new ElementType[blob[1]];
            uint offset = 2;
            for (int i = 0; i < LocalVariables.Length; i++)
                LocalVariables[i] = new ElementType(blob, ref offset);
            Console.WriteLine(blob);

            /*var data = CLIMetadata.DecompressUnsignedSignature(blob);

            Flags = (SigFlags)blob[0];
            ParamCount = data[0];

            uint i = 1;
            RetType = new ElementType(data, ref i);

            if (ParamCount > 0)
            {
                Params = new ElementType[ParamCount];
                for (uint p = 0; p < ParamCount; p++)
                {
                    Params[p] = new ElementType(data, ref i);
                }
            }*/
        }
    }

    public class MethodHeader
    {
        public uint CodeSize;
        public byte[] Code;

        public ushort Flags;
        public ushort MaxStack;
        public uint LocalVarSigTok;

        public MethodDefLayout MethodDef;
        public LocalVarSig LocalVars;

        public MethodHeader(byte[] code)
        {
            Code = code;
        }

        public MethodHeader(VirtualMemory memory, CLIMetadata metadata, MethodDefLayout methodDef)
        {
            MethodDef = methodDef;

            uint rva = methodDef.rva;

            byte type = memory.GetByte(rva);

            if ((type & 0x03) == 0x02)
            {
                // tiny method header
                CodeSize = (uint)(type >> 2);
                rva++;
            }
            else
            {
                // fat method header
                var fatHeader = memory.GetBytes(rva, 12);
                rva += 12;

                Flags = (ushort)(BitConverter.ToUInt16(fatHeader, 0) & 0x0fff);
                MaxStack = BitConverter.ToUInt16(fatHeader, 2);
                CodeSize = BitConverter.ToUInt32(fatHeader, 4);
                LocalVarSigTok = BitConverter.ToUInt32(fatHeader, 8);

                if ((type & 0x08) != 0)
                {
                    throw new Exception("More sections follows after this header (II.25.4.5)");
                }

                if (LocalVarSigTok != 0)
                {
                    LocalVars = new LocalVarSig(metadata, LocalVarSigTok);
                }
            }

            Code = memory.GetBytes(rva, (int)CodeSize);
            rva += CodeSize;
        }
    }
}
