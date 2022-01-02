namespace Kernel
{
    public static class Exceptions
    {
        public static void Throw(Runtime.KernelException exception)
        {
            Logging.Write(LogLevel.Panic, "Caught exception: ");
            Logging.WriteLine(LogLevel.Panic, exception.Message);

            // try printing the stack trace
            uint ebp = CPUHelper.CPU.ReadEBP();
            for (uint i = 0, s = 0; i < 40 && s < 10; i++)   // explore the stack up to a depth of 40 dwords
            {
                var address = ebp + (i << 2);
                if (address >= 0x9000) break;

                var contents = CPUHelper.CPU.ReadMemInt(ebp + (i << 2));
                var symbol = GetSymbolName(contents);
                
                if (symbol != null)
                {
                    Logging.Write(LogLevel.Panic, "0x{0:X} : 0x{1:X} ", address, contents);
                    Logging.WriteLine(LogLevel.Panic, symbol);
                    s++;
                }
                else if (_symbols == null) Logging.WriteLine(LogLevel.Panic, "0x{0:X} : 0x{1:X}", address, contents);
            }

            while (true) ;
        }

        private static string GetSymbolName(uint addr)
        {
            if (_symbols == null) return null;

            for (int i = 0; i < _symbols.Count - 1; i++)
            {
                if (addr >= _symbols[i].Address && addr < _symbols[i + 1].Address)
                {
                    var arrayPtr = Memory.Utilities.ObjectToPtr(_symbols[i].Name) + 8;
                    return Memory.Utilities.UnsafeCast<string>(arrayPtr);
                }
            }

            return null;
        }

        public class SymbolEntry
        {
            public uint Address;
            public byte[] Name;
        }

        private static Runtime.Collections.List<SymbolEntry> _symbols;

        public static int SymbolCount { get { return (_symbols == null ? 0 : _symbols.Count); } }

        public static void AddSymbols(uint[] sector)
        {
            if (_symbols == null) _symbols = new Runtime.Collections.List<SymbolEntry>(512);

            var byteArray = Memory.Utilities.UnsafeCast<byte[]>(sector);

            for (int i = 0; i < 512 - 4; i++)
            {
                if (byteArray[i + 3] == 0xff) break;    // end of the sector

                SymbolEntry entry = new SymbolEntry();
                entry.Address = (uint)(byteArray[i] | (byteArray[i + 1] << 8) | (byteArray[i + 2] << 16) | (byteArray[i + 3] << 24));
                int start = i + 4, end;
                for (end = start; end < 512; end++)
                {
                    if (byteArray[end] == 0) break;
                }
                entry.Name = new byte[end - start + 1];
                for (int j = 0; j < entry.Name.Length; j++) entry.Name[j] = byteArray[start + j];
                _symbols.Add(entry);

                i = end;
            }
        }
    }
}
