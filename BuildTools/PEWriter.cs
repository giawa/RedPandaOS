using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BuildTools
{
    public class PEWriter
    {
        private byte[] _data;
        private int _baseAddr = 0x80000;

        public PEWriter(byte[] data, int baseAddr = 0x80000)
        {
            _data = data;
            _baseAddr = baseAddr;
        }

        public void Write(string filename)
        {
            if (File.Exists(filename)) File.Delete(filename);

            // create the DOS header/stub first
            MemoryBinaryWriter dosStub = new MemoryBinaryWriter(256);
            dosStub.WriteShort(0x5a4d); // first two byte signature

            dosStub.Position = 0x3C;
            dosStub.WriteInt(0x100);    // set offset to start of PE header

            // put together the COFF header
            MemoryBinaryWriter coffHeader = new MemoryBinaryWriter(24);
            coffHeader.WriteString("PE", 4);    // signature
            coffHeader.WriteShort(0x14C);       // x86 machine type
            coffHeader.WriteShort(1);           // 1 section for now
            var seconds = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
            coffHeader.WriteInt((int)seconds);  // timedatestamp
            coffHeader.WriteInt(0);             // deprecated pointer to symbol table
            coffHeader.WriteInt(0);             // deprecated number of symbols
            coffHeader.Position += 2;           // size of optional header will be computed later, so skip past it
            coffHeader.WriteShort(0x0103);      // IMAGE_FILE_32BIT_MACHINE | IMAGE_FILE_EXECUTABLE_IMAGE | IMAGE_FILE_RELOCS_STRIPPED

            // put together optional header
            MemoryBinaryWriter optionalHeader = new MemoryBinaryWriter();
            int baseAddr = _baseAddr;
#if PE32_PLUS
            optionalHeader.WriteShort(0x20b);       // PE32+
#else
            optionalHeader.WriteShort(0x10b);       // PE32
#endif
            optionalHeader.WriteByte(0x01);         // PEWriter version 1.0
            optionalHeader.WriteByte(0x00);         // PEWriter version 1.0
            optionalHeader.WriteInt(_data.Length);  // size of code
            optionalHeader.WriteInt(0);             // size of initialized data is 0
            optionalHeader.WriteInt(0);             // size of uninitialized data is 0
            optionalHeader.WriteInt(baseAddr);      // address of entry point
            optionalHeader.WriteInt(baseAddr);      // beginning of code section (same as entry point for us)
#if !PE32_PLUS
            optionalHeader.WriteInt(0);             // skip over base of data since we don't use data atm
#endif

            ushort sectionAlignment = 0x1000;
            ushort fileAlignment = 512;

            // with file alignment and assuming a single section we can work out total file size
            int imageSize = fileAlignment + CeilingToAlignment(_data.Length, fileAlignment);
#if PE32_PLUS
            optionalHeader.WriteLong(baseAddr);    // default base address for windows applications
#else
            optionalHeader.WriteInt(baseAddr);    // default base address for windows applications
#endif
            optionalHeader.WriteInt(sectionAlignment);        // section alignment is usually equal to the page size
            optionalHeader.WriteInt(fileAlignment);           // file alignment defaults to 512
            optionalHeader.WriteShort(4);           // major operating system version
            optionalHeader.WriteShort(0);           // minor operating system version
            optionalHeader.WriteShort(1);           // major image version 1.0
            optionalHeader.WriteShort(0);           // major image version 1.0
            optionalHeader.WriteShort(1);           // major subsystem version 1.0
            optionalHeader.WriteShort(0);           // major subsystem version 1.0
            optionalHeader.WriteInt(0);             // win32versionvalue must be 0
            //optionalHeader.Position += 4;           // size of image
            optionalHeader.WriteInt(imageSize);
            //optionalHeader.Position += 4;           // size of headers
            optionalHeader.WriteInt(512);           // assume headers will only take up 512 bytes for now as we have a single section
            optionalHeader.Position += 4;           // checksum
            optionalHeader.WriteShort(3);           // console application IMAGE_SUBSYSTEM_WINDOWS_CUI
            optionalHeader.WriteShort(0);           // dll characteristics (we aren't a dll so leave as 0)
#if PE32_PLUS
            optionalHeader.WriteLong(4096);         // reserve a 4096 byte stack
            optionalHeader.WriteLong(4096);         // commit a 4096 byte stack
            optionalHeader.WriteLong(8192);         // reserve a 8192 byte heap
            optionalHeader.WriteLong(8192);         // commit a 8192 byte heap
#else
            optionalHeader.WriteInt(4096);         // reserve a 4096 byte stack
            optionalHeader.WriteInt(4096);         // commit a 4096 byte stack
            optionalHeader.WriteInt(8192);         // reserve a 8192 byte heap
            optionalHeader.WriteInt(8192);         // commit a 8192 byte heap
#endif
            optionalHeader.WriteInt(0);             // loader flags must be zero
            optionalHeader.WriteInt(0);             // number of data directories

            // done making the optional header, so go back and fill in coff header
            coffHeader.Position = 20;
            coffHeader.WriteShort((short)optionalHeader.Length);
            optionalHeader.Position = 4;

            MemoryBinaryWriter sectionHeader = new MemoryBinaryWriter(40);
            sectionHeader.WriteString(".text", 8);  // name
            sectionHeader.WriteInt(CeilingToAlignment(_data.Length, sectionAlignment));   // virtualsize
            sectionHeader.WriteInt(baseAddr);       // virtualaddress
            sectionHeader.WriteInt(_data.Length);   // virtualsize
            sectionHeader.WriteInt(fileAlignment);  // our headers thus far are under 512 bytes, so assume we start at 512
            sectionHeader.WriteInt(0);              // pointer to relocations
            sectionHeader.WriteInt(0);              // pointer to line numbers (deprecated)
            sectionHeader.WriteInt(0);              // number of relocations and line numbers are both 0
            sectionHeader.WriteInt(0x40000020);     // IMAGE_SCN_MEM_READ | IMAGE_SCN_CNT_CODE

            // build actual file
            byte[] peBytes = new byte[imageSize];
            Array.Copy(dosStub.ToArray(), peBytes, dosStub.Length);
            Array.Copy(coffHeader.ToArray(), 0, peBytes, dosStub.Length, coffHeader.Length);
            Array.Copy(optionalHeader.ToArray(), 0, peBytes, dosStub.Length + coffHeader.Length, optionalHeader.Length);
            Array.Copy(sectionHeader.ToArray(), 0, peBytes, dosStub.Length + coffHeader.Length + optionalHeader.Length, sectionHeader.Length);

            Array.Copy(_data, 0, peBytes, fileAlignment, _data.Length);

            File.WriteAllBytes(filename, peBytes);
        }

        private int CeilingToAlignment(int value, int alignment)
        {
            while ((value % alignment) != 0) value++;   // terrible hack for now

            return value;
        }
    }

    public class MemoryBinaryWriter
    {
        private List<byte> _bytes;
        private int _position = 0;

        public int Position
        {
            get { return _position; }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException("Cannot set negative position");
                //else if (value >= _bytes.Count && !_allowResize) throw new ArgumentOutOfRangeException("Cannot set position past size");
                else _position = value;
            }
        }
        public int Length => _bytes.Count;

        public byte[] ToArray() => _bytes.ToArray();

        private bool _allowResize = false;

        public MemoryBinaryWriter(int size)
        {
            _bytes = new List<byte>(size);
            for (int i = 0; i < size; i++) _bytes.Add(0);
        }

        public MemoryBinaryWriter()
        {
            _allowResize = true;
            _bytes = new List<byte>();
        }

        public void WriteShort(short value)
        {
            WriteBytes(BitConverter.GetBytes(value));
        }

        public void WriteInt(int value)
        {
            WriteBytes(BitConverter.GetBytes(value));
        }

        public void WriteLong(long value)
        {
            WriteBytes(BitConverter.GetBytes(value));
        }

        public void WriteByte(byte value)
        {
            WriteBytes(new byte[] { value });
        }

        public void WriteString(string value, int maxLength)
        {
            if (value.Length > 8) throw new Exception("No support for long strings");

            byte[] bytes = new byte[maxLength];
            for (int i = 0; i < value.Length; i++) bytes[i] = (byte)value[i];

            WriteBytes(bytes);
        }

        public void WriteBytes(byte[] value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (Position + i >= Length)
                {
                    if (!_allowResize) throw new Exception("Tried to write beyond the end of the array");
                    while (Position + i >= Length) _bytes.Add(0);
                }
                
                _bytes[Position + i] = value[i];
            }
            Position += value.Length;
        }
    }
}
