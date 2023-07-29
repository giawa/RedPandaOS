using System;

namespace PELoader
{
    public class LocalVarSig
    {
        public ElementType[] LocalVariables { get; private set; }

        public LocalVarSig(CLIMetadata metadata, uint addr)
        {
            // find the StandAloneSig row
            var sig = metadata.StandAloneSigs[((int)addr & 0x00ffffff) - 1];

            var blob = metadata.GetBlob(sig.signature);

            if (blob[0] != 0x07) throw new Exception("Invalid LOCAL_SIG (II.23.2.6)");

            LocalVariables = new ElementType[blob[1]];
            uint offset = 0;
            var uncompressed = CLIMetadata.DecompressUnsignedSignature(blob, 2);    // first 2 bytes are LOCAL_SIG and Count
            for (int i = 0; i < LocalVariables.Length; i++)
                LocalVariables[i] = new ElementType(uncompressed, ref offset);
        }
    }

    public class ExceptionHeader
    {
        [Flags]
        public enum ExceptionHeaderFlags : ushort
        {
            Exception,
            Filter,
            Finally,
            Fault
        }

        public ExceptionHeaderFlags Flags { get; private set; }
        public ushort TryOffset { get; private set; }
        public byte TryLength { get; private set; }
        public ushort HandlerOffset { get; private set; }
        public byte HandlerLength { get; private set; }
        public uint Token { get; private set; }

        public ExceptionHeader(byte[] data)
        {
            Flags = (ExceptionHeaderFlags)(BitConverter.ToUInt16(data, 0) & 0x0fff);
            TryOffset = BitConverter.ToUInt16(data, 2);
            TryLength = data[4]; 
            HandlerOffset = BitConverter.ToUInt16(data, 5);
            HandlerLength = data[7];
            Token = BitConverter.ToUInt32(data, 8);
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

        public ExceptionHeader[] ExceptionHeaders;

        public MethodHeader(byte[] code)
        {
            Code = code;
        }

        public MethodHeader(VirtualMemory memory, CLIMetadata metadata, MethodDefLayout methodDef)
        {
            MethodDef = methodDef;

            uint rva = methodDef.RVA;

            byte type = memory.GetByte(rva);

            if ((type & 0x03) == 0x02)
            {
                // tiny method header
                CodeSize = (uint)(type >> 2);
                rva++;
            }
            else if ((type & 0x03) == 0x03)
            {
                // fat method header
                var fatHeader = memory.GetBytes(rva, 12);
                rva += 12;

                Flags = (ushort)(BitConverter.ToUInt16(fatHeader, 0) & 0x0fff);
                var size = BitConverter.ToUInt16(fatHeader, 0) >> 12;
                MaxStack = BitConverter.ToUInt16(fatHeader, 2);
                CodeSize = BitConverter.ToUInt32(fatHeader, 4);
                LocalVarSigTok = BitConverter.ToUInt32(fatHeader, 8);

                if (LocalVarSigTok != 0)
                {
                    LocalVars = new LocalVarSig(metadata, LocalVarSigTok);
                }
            }
            else throw new Exception("Unsupported header type value (II.25.4.1)");

            Code = memory.GetBytes(rva, (int)CodeSize);
            rva += CodeSize;

            // if a FAT header then check if more sections follow the method body (II.25.4.5)
            if ((type & 0x03) != 0x02 && (type & 0x08) != 0)
            {
                // align RVA back to 4-bvyte boundary
                if ((rva & 0x03) != 0) rva = (rva & ~(uint)0x03) + 4;
                var headerType = memory.GetByte(rva);

                if ((headerType & 0x03) == 0x01) // Exception handling header
                {
                    var exceptionHeaderCount = (memory.GetByte(rva + 1) - 4) / 12;
                    rva += 4;
                    ExceptionHeaders = new ExceptionHeader[exceptionHeaderCount];

                    for (int i = 0; i < exceptionHeaderCount; i++)
                    {
                        var exceptionData = memory.GetBytes(rva, 12);
                        rva += 12;

                        ExceptionHeaders[i] = new ExceptionHeader(exceptionData);
                    }
                }
                else throw new Exception("Unhandled method header");
                
                if ((headerType & 0x80) != 0) throw new Exception("More sections follows after this header (II.25.4.5)");
            }
        }
    }
}
