using System;

namespace PELoader
{
    public class CustomAttributeLayout
    {
        public uint parent;
        public uint type;
        public uint value;
        public byte[] blob;

        public MethodDefLayout MethodDef;
        public MemberRefLayout MemberRef;

        private string _name;

        public string Name
        {
            get
            {
                if (!string.IsNullOrEmpty(_name)) return _name;

                if (MethodDef != null)
                {
                    _name = MethodDef.ToString();
                }
                else if (MemberRef != null)
                {
                    _name = MemberRef.ToString();
                }

                return _name;
            }
        }

        public CustomAttributeLayout(CLIMetadata metadata, ref int offset)
        {
            uint tableSize = metadata.HasCustomAttributeCount;
            uint maxTableSize = (1 << 11);

            if (tableSize >= maxTableSize)
            {
                parent = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                parent = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            switch (parent & 0x1f)
            {
                case 0x00: parent = 0x06000000 | (parent >> 5); break;
                case 0x01: parent = 0x04000000 | (parent >> 5); break;
                case 0x02: parent = 0x01000000 | (parent >> 5); break;
                case 0x03: parent = 0x02000000 | (parent >> 5); break;
                case 0x04: parent = 0x08000000 | (parent >> 5); break;
                case 0x05: parent = 0x09000000 | (parent >> 5); break;
                case 0x06: parent = 0x0A000000 | (parent >> 5); break;
                case 0x07: parent = 0x00000000 | (parent >> 5); break;
                //case 0x08: parent = 0x00000000 | (parent >> 5); break; // Permission?
                case 0x09: parent = 0x17000000 | (parent >> 5); break;
                case 0x0A: parent = 0x14000000 | (parent >> 5); break;
                case 0x0B: parent = 0x11000000 | (parent >> 5); break;
                case 0x0C: parent = 0x1A000000 | (parent >> 5); break;
                case 0x0D: parent = 0x1B000000 | (parent >> 5); break;
                case 0x0E: parent = 0x20000000 | (parent >> 5); break;
                case 0x0F: parent = 0x23000000 | (parent >> 5); break;
                case 0x10: parent = 0x26000000 | (parent >> 5); break;
                case 0x11: parent = 0x27000000 | (parent >> 5); break;
                case 0x12: parent = 0x28000000 | (parent >> 5); break;
                case 0x13: parent = 0x2A000000 | (parent >> 5); break;
                case 0x14: parent = 0x2C000000 | (parent >> 5); break;
                case 0x15: parent = 0x2B000000 | (parent >> 5); break;
                default: throw new Exception("Unsupported parent type");
            }

            tableSize = metadata.CustomAttributeTypeCount;
            maxTableSize = (1 << 13);

            if (tableSize >= maxTableSize)
            {
                type = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                type = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            switch (type & 0x07)
            {
                case 0x02:
                    type = 0x06000000 | (type >> 3);
                    MethodDef = metadata.MethodDefs[(int)(type & 0x00ffffff) - 1];
                    break;
                case 0x03: 
                    type = 0x0A000000 | (type >> 3);
                    MemberRef = metadata.MemberRefs[(int)(type & 0x00ffffff) - 1];
                    break;
                default: throw new Exception("Unsupported type");
            }

            if (metadata.WideBlob)
            {
                value = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                value = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            blob = metadata.GetBlob(value);
        }
    }
}
