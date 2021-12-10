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
                var reg0 = ReadDword((byte)i, 0, 0, 0);
                if ((reg0 & 0x0000ffff) == 0x0000ffff) continue;

                var reg2 = ReadDword((byte)i, 0, 0, 8);

                PrintPCIDevice(reg0, reg2, (byte)i);

                for (int j = 1; j < 32; j++)
                {
                    reg0 = ReadDword((byte)i, (byte)j, 0, 0);
                    if ((reg0 & 0x0000ffff) == 0x0000ffff) continue;

                    reg2 = ReadDword((byte)i, (byte)j, 0, 8);

                    PrintPCIDevice(reg0, reg2, (byte)j, true);

                    var reg3 = ReadDword((byte)i, (byte)j, 0, 0x0C);
                    var headerType = (reg3 >> 16) & 255;

                    if ((headerType & 0x80) != 0)
                    {
                        for (int f = 1; f < 8; f++)
                        {
                            reg0 = ReadDword((byte)i, (byte)j, (byte)f, 0);
                            if ((reg0 & 0x0000ffff) == 0x0000ffff) continue;

                            reg2 = ReadDword((byte)i, (byte)j, (byte)f, 8);

                            PrintPCIDevice(reg0, reg2, (byte)f, true, true);
                        }
                    }
                }
            }
        }

        private static void PrintPCIDevice(uint reg0, uint reg2, byte address, bool isSlot = false, bool isFunc = false)
        {
            if (isFunc) VGA.WriteVideoMemoryString(" |-> function 0x");
            else if (isSlot) VGA.WriteVideoMemoryString("|-> slot 0x");
            else VGA.WriteVideoMemoryString("PCI bus 0x");

            VGA.WriteHex(address);
            VGA.WriteVideoMemoryChar(' ');
            VGA.WriteHex(reg0);
            VGA.WriteVideoMemoryChar(' ');
            PrintClass(reg2);
            VGA.WriteLine();
        }

        private static void PrintClass(uint reg2)
        {
            ClassCode code = (ClassCode)((reg2 >> 24) & 0xff);
            VGA.WriteHex(reg2);
            VGA.WriteVideoMemoryChar(' ');

            switch (code)
            {
                case ClassCode.MassStorageController: VGA.WriteVideoMemoryString("Mass Storage Controller"); break;
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
