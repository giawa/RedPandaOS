namespace Kernel.Devices
{
    public static class PCI
    {
        public static void ScanBus()
        {
            VGA.WriteVideoMemoryString("Enumerating PCI bus:");
            VGA.WriteLine();
            for (int i = 0; i < 256; i++)
            {
                var result = ReadDword((byte)i, 0, 0, 0);
                if ((result & 0x0000ffff) == 0x0000ffff) continue;

                var reg2 = ReadDword((byte)i, 0, 0, 8);

                VGA.WriteVideoMemoryString("PCI addr 0x");
                VGA.WriteHex((byte)i);
                VGA.WriteVideoMemoryChar(' ');
                VGA.WriteHex(result);
                VGA.WriteVideoMemoryChar(' ');
                PrintClass(reg2);
                VGA.WriteLine();

                for (int j = 0; j < 256; j++)
                {
                    result = ReadDword((byte)i, (byte)j, 0, 0);
                    if ((result & 0x0000ffff) == 0x0000ffff) continue;

                    reg2 = ReadDword((byte)i, (byte)j, 0, 8);

                    VGA.WriteVideoMemoryString(" |-> Slot 0x");
                    VGA.WriteHex((byte)j);
                    VGA.WriteVideoMemoryChar(' ');
                    VGA.WriteHex(result);
                    VGA.WriteVideoMemoryChar(' ');
                    PrintClass(reg2);
                    VGA.WriteLine();
                }
            }
        }

        public static void PrintClass(uint reg2)
        {
            ClassCode code = (ClassCode)((reg2 >> 24) & 0xff);
            VGA.WriteHex(reg2);
            VGA.WriteVideoMemoryChar(' ');

            switch (code)
            {
                case ClassCode.NetworkController: VGA.WriteVideoMemoryString("Network Controller"); break;
                case ClassCode.DisplayController: VGA.WriteVideoMemoryString("Display Controller"); break;
                case ClassCode.BridgeDevice: VGA.WriteVideoMemoryString("Bridge Device"); break;
                default: VGA.WriteVideoMemoryString("Unknown "); VGA.WriteHex((byte)code); break;
            }
        }

        private static uint ReadDword(byte bus, byte slot, byte func, byte offset)
        {
            uint address;

            address = (uint)((bus << 16) | (slot << 11) | (func << 8) | (offset & 0xfc)) | 0x80000000U;

            CPUHelper.CPU.OutDxEax(0xCF8, address);

            return CPUHelper.CPU.InDxDword(0xCFC);
        }

        private static void WriteDword(byte bus, byte slot, byte func, byte offset, uint data)
        {
            uint address;

            address = (uint)((bus << 16) | (slot << 11) | (func << 8) | (offset & 0xfc)) | 0x80000000U;

            CPUHelper.CPU.OutDxEax(0xCF8, address);
            CPUHelper.CPU.OutDxEax(0xCFC, data);
        }

        public enum ClassCode : byte
        {
            Unclassified = 0x00,
            MassStorageController = 0x01,
            NetworkController = 0x02,
            DisplayController = 0x03,
            MultimediaController = 0x04,
            MemoryController = 0x05,
            BridgeDevice = 0x06,
            SimpleCommunicationController = 0x07,
            BaseSystemPeripheral = 0x08,
            InputDeviceController = 0x09,
            DockingStation = 0x0A,
            Processor = 0x0B,
            SetialBusController = 0x0C,
            WirelessController = 0x0D,
            IntelligentController = 0x0E,
            SatelliteCommunicationController = 0x0F,
            EncryptionController = 0x10,
            SignalProcessingController = 0x11,
            ProcessingAccelerator = 0x12,
            NonEssentialInstrumentation = 0x13,
            CoProcessor = 0x40,
            UnassignedClass = 0xFF
        }
    }
}
