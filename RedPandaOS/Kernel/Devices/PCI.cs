namespace Kernel.Devices
{
    public static class PCI
    {
        public class PCIDevice
        {
            public byte Bus;
            public byte Device;
            public byte Function;
            private byte Padding;

            public uint reg0;
            public uint reg2;

            public uint BAR0;
            public uint BAR1;
            public uint BAR2;
            public uint BAR3;
            public uint BAR4;
            public uint BAR5;

            public uint BAR0Size;
            public uint BAR1Size;
            public uint BAR2Size;
            public uint BAR3Size;
            public uint BAR4Size;
            public uint BAR5Size;

            public ClassCode ClassCode
            {
                get
                {
                    return (ClassCode)((reg2 >> 24) & 0xff);
                }
            }

            public byte SubClass
            {
                get
                {
                    return (byte)(reg2 >> 16);
                }
            }

            public uint this[int index]
            {
                get
                {
                    if (index < 0 || index > 63) throw new System.Exception("index out of range");
                    return ReadDword(Bus, Device, Function, (byte)(index * 4));
                }
                set
                {
                    if (index < 0 || index > 63) throw new System.Exception("index out of range");
                    Logging.WriteLine(LogLevel.Warning, "Writing to bus {0:X} device {1:X} function {2:X}", Bus, Device, Function);
                    WriteDword(Bus, Device, Function, (byte)(index * 4), value);
                }
            }
        }

        public static Runtime.Collections.List<PCIDevice> Devices;

        private static void AddDevice(byte bus, byte device, byte function, uint reg0, uint reg2)
        {
            PCIDevice pciDevice = new PCIDevice();

            pciDevice.Bus = bus;
            pciDevice.Device = device;
            pciDevice.Function = function;

            pciDevice.reg0 = reg0;
            pciDevice.reg2 = reg2;

            var reg3 = ReadDword(bus, device, function, 0x0C);
            var headerType = (reg3 >> 16) & 0xff;

            pciDevice.BAR0 = ReadDword(bus, device, function, 0x10);
            pciDevice.BAR0Size = ProbeMemorySize(bus, device, function, 0x10, pciDevice.BAR0);
            pciDevice.BAR1 = ReadDword(bus, device, function, 0x14);
            pciDevice.BAR1Size = ProbeMemorySize(bus, device, function, 0x14, pciDevice.BAR1);

            if (headerType == 0)
            {
                pciDevice.BAR2 = ReadDword(bus, device, function, 0x18);
                pciDevice.BAR2Size = ProbeMemorySize(bus, device, function, 0x18, pciDevice.BAR2);
                pciDevice.BAR3 = ReadDword(bus, device, function, 0x1C);
                pciDevice.BAR3Size = ProbeMemorySize(bus, device, function, 0x1C, pciDevice.BAR3);
                pciDevice.BAR4 = ReadDword(bus, device, function, 0x20);
                pciDevice.BAR4Size = ProbeMemorySize(bus, device, function, 0x20, pciDevice.BAR4);
                pciDevice.BAR5 = ReadDword(bus, device, function, 0x24);
                pciDevice.BAR5Size = ProbeMemorySize(bus, device, function, 0x24, pciDevice.BAR5);
            }

            Devices.Add(pciDevice);
        }

        private static uint ProbeMemorySize(byte bus, byte device, byte function, byte offset, uint initialValue)
        {
            WriteDword(bus, device, function, offset, 0xffffffff);
            var newValue = ReadDword(bus, device, function, offset);

            // write the original value back
            WriteDword(bus, device, function, offset, initialValue);

            if ((initialValue & 1) != 0) newValue &= 0xfffffffc;
            else newValue &= 0xfffffff0;

            return ~newValue + 1;
        }

        public static void ScanBus()
        {
            Devices = new Runtime.Collections.List<PCIDevice>();
            Logging.WriteLine(LogLevel.Trace, "Enumerating PCI bus:");

            // there are up to 256 PCI busses, so check each one
            for (int i = 0; i < 256; i++)
            {
                var reg0 = ReadDword((byte)i, 0, 0, 0);
                if ((reg0 & 0x0000ffff) == 0x0000ffff) continue;

                var reg2 = ReadDword((byte)i, 0, 0, 8);

                PrintPCIDevice(reg0, reg2, (byte)i);

                AddDevice((byte)i, 0, 0, reg0, reg2);

                // in each PCI bus there can be up to 32 devices
                // we already checked (and printed) device 0, so check the other 31
                for (int j = 1; j < 32; j++)
                {
                    reg0 = ReadDword((byte)i, (byte)j, 0, 0);
                    if ((reg0 & 0x0000ffff) == 0x0000ffff) continue;

                    reg2 = ReadDword((byte)i, (byte)j, 0, 8);

                    PrintPCIDevice(reg0, reg2, (byte)j, true);

                    AddDevice((byte)i, (byte)j, 0, reg0, reg2);

                    var reg3 = ReadDword((byte)i, (byte)j, 0, 0x0C);
                    var headerType = (reg3 >> 16) & 255;

                    // check if this is a multi-function device, and if so check the other functions
                    if ((headerType & 0x80) != 0)
                    {
                        for (int f = 1; f < 8; f++)
                        {
                            reg0 = ReadDword((byte)i, (byte)j, (byte)f, 0);
                            if ((reg0 & 0x0000ffff) == 0x0000ffff) continue;

                            reg2 = ReadDword((byte)i, (byte)j, (byte)f, 8);

                            PrintPCIDevice(reg0, reg2, (byte)f, true, true);

                            AddDevice((byte)i, (byte)j, (byte)f, reg0, reg2);
                        }
                    }
                }
            }
        }

        private static void PrintPCIDevice(uint reg0, uint reg2, byte address, bool isSlot = false, bool isFunc = false)
        {
            string format = "PCI bus 0x{0:X} {1:X}";
            if (isFunc) format = " |->func 0x{0:X} {1:X}";
            else if (isSlot) format = "|-> slot 0x{0:X} {1:X}";

            Logging.WriteLine(LogLevel.Trace, format, address, reg2);

            /*if (isFunc) VGA.WriteString(" |-> func 0x");
            else if (isSlot) VGA.WriteString("|-> slot 0x");
            else VGA.WriteString("PCI bus 0x");

            VGA.WriteHex(address);
            VGA.WriteChar(' ');
            //VGA.WriteHex(reg0);
            //VGA.WriteVideoMemoryChar(' ');
            PrintClass(reg2);
            VGA.WriteLine();*/
        }

        private static void PrintClass(uint reg2)
        {
            ClassCode code = (ClassCode)((reg2 >> 24) & 0xff);
            //VGA.WriteHex(reg2);
            //VGA.WriteVideoMemoryChar(' ');)

            switch (code)
            {
                case ClassCode.MassStorageController:
                    uint subclass = (reg2 >> 16) & 0xff;
                    switch (subclass)
                    {
                        case 1: VGA.WriteString("IDE "); break;
                        case 2: VGA.WriteString("Floppy "); break;
                        case 3: VGA.WriteString("IPI "); break;
                        case 4: VGA.WriteString("RAID "); break;
                        case 5: VGA.WriteString("ATA "); break;
                        case 6: VGA.WriteString("SATA "); break;
                        case 7: VGA.WriteString("SCSI "); break;
                        case 8: VGA.WriteString("NVM "); break;
                        default: VGA.WriteString("Unknown "); break;
                    }
                    VGA.WriteString("Mass Storage Controller");
                    if (subclass == 1)
                    {
                        uint progif = (reg2 >> 8) & 0xff;
                        switch (progif)
                        {
                            case 0x00: VGA.WriteString(" (ISA Compatibility)"); break;
                            case 0x05: VGA.WriteString(" (PCI Native)"); break;
                            case 0x0A: VGA.WriteString(" (ISA Compatibility 2)"); break;
                            case 0x0F: VGA.WriteString(" (PCI Native 2)"); break;
                            case 0x80: VGA.WriteString(" (ISA Compatibility 3)"); break;
                            case 0x85: VGA.WriteString(" (PCI Native 3)"); break;
                            case 0x8A: VGA.WriteString(" (ISA Compatibility 4)"); break;
                            case 0x8F: VGA.WriteString(" (PCI Native 4)"); break;
                        }
                        //if (progif == 0x80) VGA.WriteVideoMemoryString(" (ISA Compatibility 3)");
                        //Logging.WriteLine(LogLevel.Trace, "IDE prog if = 0x{0}", progif);
                    }
                    break;
                case ClassCode.NetworkController: VGA.WriteString("Network Controller"); break;
                case ClassCode.DisplayController: VGA.WriteString("Display Controller"); break;
                case ClassCode.MultimediaController: VGA.WriteString("Multimedia Controller"); break;
                case ClassCode.BridgeDevice: VGA.WriteString("Bridge Device"); break;
                case ClassCode.SerialBusController: VGA.WriteString("Serial Bus Controller"); break;
                default: VGA.WriteString("Unknown "); VGA.WriteHex((byte)code); break;
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
            SerialBusController = 0x0C,
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
