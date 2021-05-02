﻿using System;
using System.Text;

namespace PELoader
{
    public class MethodSpecLayout
    {
        public uint method;
        public uint instantiation;
        public MethodSpecSig MemberSignature;

        public MethodSpecLayout(CLIMetadata metadata, ref int offset)
        {
            byte firstByte = metadata.Table.Heap[offset];

            uint tableSize = 0;
            uint maxTableSize = (1 << 15);

            switch (firstByte & 0x01)
            {
                case 0x00: tableSize = metadata.TableSizes[MetadataTable.MethodDef]; break;
                case 0x01: tableSize = metadata.TableSizes[MetadataTable.MemberRef]; break;
            }

            if (tableSize >= maxTableSize)
            {
                method = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                method = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            switch (method & 0x01)
            {
                case 0x00: method = 0x06000000 | (method >> 1); break;
                case 0x01: method = 0x0A000000 | (method >> 1); break;
            }

            if (metadata.WideBlob)
            {
                instantiation = BitConverter.ToUInt32(metadata.Table.Heap, offset);
                offset += 4;
            }
            else
            {
                instantiation = BitConverter.ToUInt16(metadata.Table.Heap, offset);
                offset += 2;
            }

            MemberSignature = new MethodSpecSig(metadata, instantiation, this);
        }

        public class MethodSpecSig  // II.23.2.15
        {
            public ElementType[] Types;
            public string[] TypeNames;

            public MethodSpecSig(CLIMetadata metadata, uint addr, MethodSpecLayout methodSpec)
            {
                var blob = metadata.GetBlob(addr);
                var data = CLIMetadata.DecompressUnsignedSignature(blob, 1);    // first byte is the flags

                if (blob[0] == 0x0a)
                {
                    uint genArgCount = data[0];
                    uint offset = 1;

                    Types = new ElementType[genArgCount];
                    TypeNames = new string[genArgCount];

                    for (int i = 0; i < genArgCount; i++)
                    {
                        Types[i] = new ElementType(data, ref offset);

                        if ((Types[i].Token & 0x02000000) != 0) TypeNames[i] = metadata.TypeDefs[(int)(Types[i].Token & 0x00ffffff) - 1].Name;
                        else if (Types[i].Type == ElementType.EType.MVar)
                        {
                            if ((methodSpec.method & 0x06000000) != 0)
                            {
                                var parent = metadata.MethodDefs[(int)(methodSpec.method & 0x00ffffff) - 1];
                                throw new Exception("Incomplete");
                            }
                            else throw new Exception("Broken");
                        }
                        else throw new Exception("No implementation for this table type");
                    }

                    Console.WriteLine("made it");
                }
                else throw new Exception("Unexpected blob type");
            }

            public string ToAsmString()
            {
                StringBuilder sb = new StringBuilder("<");

                for (int i = 0; i < Types.Length; i++)
                {
                    var type = Types[i];

                    if (!string.IsNullOrEmpty(TypeNames[i])) sb.Append(TypeNames[i]);
                    else sb.Append(type.Type);

                    if (i < Types.Length - 1) sb.Append(",");
                }

                sb.Append(">");
                return sb.ToString();
            }
        }
    }
}