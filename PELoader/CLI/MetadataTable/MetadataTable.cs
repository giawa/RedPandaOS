namespace PELoader
{
    public struct MetadataTable
    {
        public const byte Assembly = 0x20;
        public const byte AssemblyOS = 0x22;
        public const byte AssemblyProcessor = 0x21;
        public const byte AssemblyRef = 0x23;
        public const byte AssemblyRefOS = 0x25;
        public const byte AssemblyRefProcessor = 0x24;
        public const byte ClassLayout = 0x0F;
        public const byte Constant = 0x0B;
        public const byte CustomAttribute = 0x0C;
        public const byte DeclSecurity = 0x0E;
        public const byte EventMap = 0x12;
        public const byte Event = 0x14;
        public const byte ExportedType = 0x27;
        public const byte Field = 0x04;
        public const byte FieldLayout = 0x10;
        public const byte FieldMarshal = 0x0D;
        public const byte FieldRVA = 0x1D;
        public const byte File = 0x26;
        public const byte GenericParam = 0x2A;
        public const byte GenericParamConstraint = 0x2C;
        public const byte ImplMap = 0x1C;
        public const byte InterfaceImpl = 0x09;
        public const byte ManifestResource = 0x28;
        public const byte MemberRef = 0x0A;
        public const byte MethodDef = 0x06;
        public const byte MethodImpl = 0x19;
        public const byte MethodSemantics = 0x18;
        public const byte MethodSpec = 0x2B;
        public const byte Module = 0x00;
        public const byte ModuleRef = 0x1A;
        public const byte NestedClass = 0x29;
        public const byte Param = 0x08;
        public const byte Property = 0x17;
        public const byte PropertyMap = 0x15;
        public const byte StandAloneSig = 0x11;
        public const byte TypeDef = 0x02;
        public const byte TypeRef = 0x01;
        public const byte TypeSpec = 0x1B;
    }
}
