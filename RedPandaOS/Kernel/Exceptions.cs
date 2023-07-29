namespace Kernel
{
    public static class Exceptions
    {
        public static void Throw(Runtime.KernelException exception)
        {
            Logging.Write(LogLevel.Panic, "Caught exception: ");

            if (exception.Message == null) Logging.WriteLine(LogLevel.Panic, "Unknown Type");
            else Logging.WriteLine(LogLevel.Panic, exception.Message);

            // try printing the stack trace
            //PrintStackTrace();

            while (true) ;
        }

        public static void PrintStackTrace(uint ebp)
        {
            Logging.LoggingLevel = LogLevel.Warning;    // disable memory allocation messages

            // try printing the stack trace
            //uint ebp = CPUHelper.CPU.ReadEBP();
            for (uint i = 0, s = 0; i < 60 && s < 10; i++)   // explore the stack up to a depth of 40 dwords
            {
                var address = ebp + (i << 2);
                if ((address & 0xfff) == 0) break;
                //if (address >= 0x9000) break;

                var contents = CPUHelper.CPU.ReadMemInt(ebp + (i << 2));
                var symbol = GetSymbolName(contents);

                if (symbol != null)
                {
                    Logging.Write(LogLevel.Panic, "0x{0:X} : 0x{1:X} ", address, contents);
                    Logging.WriteLine(LogLevel.Panic, symbol);
                    s++;
                }
                else if (_symbols == null)// && contents > 0xA000 && contents <= 0x13000)
                {
                    Logging.WriteLine(LogLevel.Panic, "0x{0:X} : 0x{1:X}", address, contents);
                    s++;
                }
            }
        }

        public static void PrintStackTrace()
        {
            Logging.WriteLine(LogLevel.Panic, "****");
            Logging.LoggingLevel = LogLevel.Warning;    // disable memory allocation messages

            // try printing the stack trace
            uint ebp = CPUHelper.CPU.ReadEBP();
            for (uint i = 0, s = 0; i < 60 && s < 100; i++)   // explore the stack up to a depth of 40 dwords
            {
                var address = ebp + (i << 2);
                if ((address & 0xff) == 0) break;
                //if (address >= 0x9000) break;

                var contents = CPUHelper.CPU.ReadMemInt(ebp + (i << 2));
                var symbol = GetSymbolName(contents);

                if (symbol != null)
                {
                    Logging.Write(LogLevel.Panic, "0x{0:X} : 0x{1:X} ", address, contents);
                    Logging.WriteLine(LogLevel.Panic, symbol);
                    s++;
                }
                else //if (_symbols == null)// && contents > 0xA000 && contents <= 0x13000)
                {
                    Logging.WriteLine(LogLevel.Panic, "0x{0:X} : 0x{1:X}", address, contents);
                    s++;
                }
            }
        }

        private static string GetSymbolName(uint addr)
        {
            if (_symbols == null) return null;

            for (int i = 0; i < _symbols.Count - 1; i++)
            {
                if (addr >= _symbols[i].Address && addr < _symbols[i + 1].Address)
                {
                    int length = _symbols[i].Name.Length;
                    char[] array = new char[length];
                    for (int j = 0; j < length; j++) array[j] = (char)_symbols[i].Name[j];
                    return new string(array);
                }
            }

            return null;
        }

        public static void ReadSymbols(byte drive)
        {
            uint symbolsOffset = 0x17200U;
            uint[] sector = new uint[128];

            Logging.WriteLine(LogLevel.Trace, "Reading symbols...");
            while (symbolsOffset < 0x1A000)
            {
                Devices.PATA.Access(0, 0, symbolsOffset / 512, 1, 0, sector);
                if (sector[127] == 0) break;
                AddSymbols(sector);
                symbolsOffset += 512;
            }
            Logging.WriteLine(LogLevel.Trace, "Done reading {0} symbols...", (uint)Exceptions.SymbolCount);
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
