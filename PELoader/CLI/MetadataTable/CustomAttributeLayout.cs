using System;

namespace PELoader
{
    public class CustomAttributeLayout
    {
        public uint parent;
        public uint type;
        public uint value;

        public CustomAttributeLayout(CLIMetadata metadata, ref int offset)
        {
            byte firstByte = metadata.Table.Heap[offset];

            uint tableSize = 0;
            uint maxTableSize = (1 << 11);

            switch (firstByte & 0x1f)
            {
                case 0: tableSize = metadata.TableSizes[MetadataTable.MethodDef]; break;
                case 1: tableSize = metadata.TableSizes[MetadataTable.Field]; break;
                case 2: tableSize = metadata.TableSizes[MetadataTable.TypeRef]; break;
                case 3: tableSize = metadata.TableSizes[MetadataTable.TypeDef]; break;
                case 4: tableSize = metadata.TableSizes[MetadataTable.Param]; break;
                case 5: tableSize = metadata.TableSizes[MetadataTable.InterfaceImpl]; break;
                case 6: tableSize = metadata.TableSizes[MetadataTable.MemberRef]; break;
                case 7: tableSize = metadata.TableSizes[MetadataTable.Module]; break;
                //case 8: tableSize = metadata.TableSizes[MetadataTable.Permission]; break;
                case 9: tableSize = metadata.TableSizes[MetadataTable.Property]; break;
                case 10: tableSize = metadata.TableSizes[MetadataTable.Event]; break;
                case 11: tableSize = metadata.TableSizes[MetadataTable.StandAloneSig]; break;
                case 12: tableSize = metadata.TableSizes[MetadataTable.ModuleRef]; break;
                case 13: tableSize = metadata.TableSizes[MetadataTable.TypeSpec]; break;
                case 14: tableSize = metadata.TableSizes[MetadataTable.Assembly]; break;
                case 15: tableSize = metadata.TableSizes[MetadataTable.AssemblyRef]; break;
                case 16: tableSize = metadata.TableSizes[MetadataTable.File]; break;
                case 17: tableSize = metadata.TableSizes[MetadataTable.ExportedType]; break;
                case 18: tableSize = metadata.TableSizes[MetadataTable.ManifestResource]; break;
                case 19: tableSize = metadata.TableSizes[MetadataTable.GenericParam]; break;
                case 20: tableSize = metadata.TableSizes[MetadataTable.GenericParamConstraint]; break;
                case 21: tableSize = metadata.TableSizes[MetadataTable.MethodSpec]; break;
                default: throw new Exception("Invalid table");
            }

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

            firstByte = metadata.Table.Heap[offset];

            tableSize = 0;
            maxTableSize = (1 << 13);

            switch (firstByte & 0x07)
            {
                case 2: tableSize = metadata.TableSizes[MetadataTable.MethodDef]; break;
                case 3: tableSize = metadata.TableSizes[MetadataTable.MemberRef]; break;
                default: throw new Exception("Invalid table");
            }

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
        }
    }
}
