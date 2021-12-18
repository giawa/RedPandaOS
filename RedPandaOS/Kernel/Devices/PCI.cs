namespace Kernel.Devices
{
    public static class PCI
    {
        public struct PCIDevice
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
        }

        private static void InitializeStorageDevice(byte bus, byte device, byte function, uint reg0, uint reg2)
        {
            //storageDevice = Memory.BumpHeap.Malloc<PCIDevice>();

            storageDevice.Bus = bus;
            storageDevice.Device = device;
            storageDevice.Function = function;

            storageDevice.reg0 = reg0;
            storageDevice.reg2 = reg2;

            var reg3 = ReadDword(bus, device, function, 0x0C);
            var headerType = (reg3 >> 16) & 0xff;

            storageDevice.BAR0 = ReadDword(bus, device, function, 0x10);
            storageDevice.BAR0Size = ProbeMemorySize(bus, device, function, 0x10, storageDevice.BAR0);
            storageDevice.BAR1 = ReadDword(bus, device, function, 0x14);
            storageDevice.BAR1Size = ProbeMemorySize(bus, device, function, 0x14, storageDevice.BAR1);

            if (headerType == 0)
            {
                storageDevice.BAR2 = ReadDword(bus, device, function, 0x18);
                storageDevice.BAR0Size = ProbeMemorySize(bus, device, function, 0x18, storageDevice.BAR2);
                storageDevice.BAR3 = ReadDword(bus, device, function, 0x1C);
                storageDevice.BAR0Size = ProbeMemorySize(bus, device, function, 0x1C, storageDevice.BAR3);
                storageDevice.BAR4 = ReadDword(bus, device, function, 0x20);
                storageDevice.BAR0Size = ProbeMemorySize(bus, device, function, 0x20, storageDevice.BAR4);
                storageDevice.BAR5 = ReadDword(bus, device, function, 0x24);
                storageDevice.BAR0Size = ProbeMemorySize(bus, device, function, 0x24, storageDevice.BAR5);
            }

            //VGA.WriteHex(storageDevice.Bus); VGA.WriteVideoMemoryChar(' '); VGA.WriteHex(storageDevice.Device); VGA.WriteVideoMemoryChar(' '); VGA.WriteHex(storageDevice.Function); VGA.WriteLine();
            VGA.WriteString("BAR0: "); VGA.WriteHex(storageDevice.BAR0); VGA.WriteChar(' '); VGA.WriteHex(storageDevice.BAR0Size); VGA.WriteLine();
            VGA.WriteString("BAR1: "); VGA.WriteHex(storageDevice.BAR1); VGA.WriteChar(' '); VGA.WriteHex(storageDevice.BAR1Size); VGA.WriteLine();

            if (headerType == 0)
            {
                VGA.WriteString("BAR2: "); VGA.WriteHex(storageDevice.BAR2); VGA.WriteChar(' '); VGA.WriteHex(storageDevice.BAR2Size); VGA.WriteLine();
                VGA.WriteString("BAR3: "); VGA.WriteHex(storageDevice.BAR3); VGA.WriteChar(' '); VGA.WriteHex(storageDevice.BAR3Size); VGA.WriteLine();
                VGA.WriteString("BAR4: "); VGA.WriteHex(storageDevice.BAR4); VGA.WriteChar(' '); VGA.WriteHex(storageDevice.BAR4Size); VGA.WriteLine();
                VGA.WriteString("BAR5: "); VGA.WriteHex(storageDevice.BAR5); VGA.WriteChar(' '); VGA.WriteHex(storageDevice.BAR5Size); VGA.WriteLine();
            }
        }

        private static uint ProbeMemorySize(byte bus, byte device, byte function, byte offset, uint initialValue)
        {
            WriteDword(bus, device, function, offset, 0xffffffff);
            var newValue = ReadDword(bus, device, function, offset);

            //VGA.WriteVideoMemoryString("ProbeMemorySize returned ");
            //VGA.WriteHex(newValue & 0xfffffff0);
            //VGA.WriteLine();

            // write the original value back
            WriteDword(bus, device, function, offset, initialValue);

            return ~(newValue & 0xfffffff0) + 1;
        }

        private static PCIDevice storageDevice;

        public static void ScanBus()
        {
            VGA.WriteString("Enumerating PCI bus:");
            VGA.WriteLine();

            // there are up to 256 PCI busses, so check each one
            for (int i = 0; i < 256; i++)
            {
                var reg0 = ReadDword((byte)i, 0, 0, 0);
                if ((reg0 & 0x0000ffff) == 0x0000ffff) continue;

                var reg2 = ReadDword((byte)i, 0, 0, 8);

                PrintPCIDevice(reg0, reg2, (byte)i);

                //InitializeStorageDevice((byte)i, 0, 0, reg0, reg2);

                // in each PCI bus there can be up to 32 devices
                // we already checked (and printed) device 0, so check the other 31
                for (int j = 1; j < 32; j++)
                {
                    reg0 = ReadDword((byte)i, (byte)j, 0, 0);
                    if ((reg0 & 0x0000ffff) == 0x0000ffff) continue;

                    reg2 = ReadDword((byte)i, (byte)j, 0, 8);

                    PrintPCIDevice(reg0, reg2, (byte)j, true);

                    //InitializeStorageDevice((byte)i, (byte)j, 0, reg0, reg2);

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

                            ClassCode code = (ClassCode)((reg2 >> 24) & 0xff);
                            //if (code == ClassCode.MassStorageController)
                            {
                                //InitializeStorageDevice((byte)i, (byte)j, (byte)f, reg0, reg2);
                            }
                        }
                    }
                }
            }
        }

        private static void PrintPCIDevice(uint reg0, uint reg2, byte address, bool isSlot = false, bool isFunc = false)
        {
            if (isFunc) VGA.WriteString(" |-> func 0x");
            else if (isSlot) VGA.WriteString("|-> slot 0x");
            else VGA.WriteString("PCI bus 0x");

            VGA.WriteHex(address);
            VGA.WriteChar(' ');
            //VGA.WriteHex(reg0);
            //VGA.WriteVideoMemoryChar(' ');
            PrintClass(reg2);
            VGA.WriteLine();
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
                    }
                    break;
                case ClassCode.NetworkController: VGA.WriteString("Network Controller"); break;
                case ClassCode.DisplayController: VGA.WriteString("Display Controller"); break;
                case ClassCode.BridgeDevice: VGA.WriteString("Bridge Device"); break;
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
