using System;

namespace PELoader
{
    public class MethodHeader
    {
        public uint CodeSize;
        public byte[] Code;

        public ushort Flags;
        public ushort MaxStack;
        public uint LocalVarSigTok;

        public MethodDefLayout MethodDef;

        public MethodHeader(byte[] code)
        {
            Code = code;
        }

        public MethodHeader(VirtualMemory memory, MethodDefLayout methodDef)
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
            }

            Code = memory.GetBytes(rva, (int)CodeSize);
            rva += CodeSize;
        }
    }
}
