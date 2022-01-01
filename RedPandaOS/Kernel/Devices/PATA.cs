using System;

namespace Kernel.Devices
{
    public class PATA
    {
        public class Channel
        {
            public ushort Base;
            public ushort Control;
            public ushort BusMasterIDE;
            public byte DisableInterrupts;
            public byte Padding;
        }

        public class Device
        {
            public byte Reserved;
            public byte Channel;
            public byte Drive;
            public ushort Type;
            public ushort Signature;
            public ushort Capabilities;
            public uint CommandSets;
            public uint Size;
            public byte[] Model;

            public Device()
            {
                Model = new byte[41];
            }
        }

        [Flags]
        public enum Error : byte
        {
            None = 0x00,
            NoAddressMark = 0x01,
            Track0NotFound = 0x02,
            CommandAborted = 0x04,
            MediaChangeRequest = 0x08,
            IDMarkNotFound = 0x10,
            MediaChanged = 0x20,
            UncorrectableData = 0x40,
            BadBlock = 0x80,
        }

        [Flags]
        public enum Status : byte
        {
            None = 0x00,
            Error = 0x01,
            Index = 0x02,
            CorrectedData = 0x04,
            DataRequestReady = 0x08,
            DriveSeekComplete = 0x10,
            DriveWriteFault = 0x20,
            DriveReady = 0x40,
            Busy = 0x80,
        }

        public enum Command : byte
        {
            Read_PIO = 0x20,
            Read_PIO_Ext = 0x24,
            Read_DMA = 0xC8,
            Read_DMA_Ext = 0x25,
            Write_PIO = 0x30,
            Write_PIO_Ext = 0x34,
            Write_DMA = 0xCA,
            Write_DMA_Ext = 0x35,
            Cache_Flush = 0xE7,
            Cache_Flush_Ext = 0xEA,
            Packet = 0xA0,
            Identify_Packet = 0xA1,
            Identify = 0xEC,

            // ATAPI Commands
            Read = 0xA8,
            Eject = 0x1B,
        }

        public enum IdentifyType : byte
        {
            DeviceType = 0,
            Cylinders = 2,
            Heads = 6,
            Sectors = 12,
            Serial = 20,
            Model = 54,
            Capabilities = 98,
            FieldValid = 106,
            MaxLBA = 120,
            CommandSets = 164,
            MaxLBA_Ext = 200,
        }

        public enum Register : byte
        {
            Data = 0,
            Error = 1,
            Features = 1,
            SectorCount0 = 2,
            LBA0 = 3,
            LBA1 = 4,
            LBA2 = 5,
            HDDevSel = 6,
            Command = 7,
            Status = 7,
            SectorCount1 = 8,
            LBA3 = 9,
            LBA4 = 0x0A,
            LBA5 = 0x0B,
            Control = 0x0C,
            AltStatus = 0x0C,
            DevAddress = 0x0D,
        }

        private Channel[] _channels;

        private const byte ATA_REG_CONTROL = 0x0C;

        private int _irqInvoked = 0;
        private uint[] _buffer;
        private Runtime.Collections.List<Device> _devices = new Runtime.Collections.List<Device>(4);

        public PATA(PCI.PCIDevice device)
        {
            _buffer = new uint[2048 / 4];
            _channels = new Channel[2];
            _channels[0] = new Channel();
            _channels[1] = new Channel();

            _channels[0].Base = (ushort)(device.BAR0 == 0 ? 0x1F0 : (device.BAR0 & 0xFFFFFFFC));
            _channels[0].Control = (ushort)(device.BAR1 == 0 ? 0x3F6 : (device.BAR1 & 0xFFFFFFFC));
            _channels[0].BusMasterIDE = (ushort)((device.BAR4 & 0xFFFFFFFC) + 0);
            _channels[0].DisableInterrupts = 0x02;  // disable irq

            _channels[1].Base = (ushort)(device.BAR2 == 0 ? 0x170 : (device.BAR2 & 0xFFFFFFFC));
            _channels[1].Control = (ushort)(device.BAR3 == 0 ? 0x376 : (device.BAR3 & 0xFFFFFFFC));
            _channels[1].BusMasterIDE = (ushort)((device.BAR4 & 0xFFFFFFFC) + 8);
            _channels[1].DisableInterrupts = 0x02;  // disable irq

            WriteRegister(0x00, Register.Control, 2);
            WriteRegister(0x01, Register.Control, 2);

            for (byte i = 0; i < 2; i++)
            {
                for (byte j = 0; j < 2; j++)
                {
                    // select drive
                    WriteRegister(i, Register.HDDevSel, (byte)(0xA0 | (j << 4)));
                    System.Threading.Thread.Sleep(1);   // wait 1ms for drive select to happen

                    WriteRegister(i, Register.Command, (byte)Command.Identify);
                    System.Threading.Thread.Sleep(1);   // wait 1ms for identify command to complete

                    var status = (Status)ReadRegister(i, Register.Status);
                    if (status == 0) continue;
                    Logging.WriteLine(LogLevel.Trace, "Got {0} from IDE device {1} {2}", (uint)status, i, j);

                    bool error = false;
                    int count = 0;

                    while (count < 1000)
                    {
                        if ((status & Status.Error) != Status.None)
                        {
                            error = true;
                            break;
                        }
                        else if ((status & Status.Busy) == Status.None && (status & Status.DataRequestReady) != Status.None)
                        {
                            break;
                        }
                        count++;
                    }

                    //int type = 0;

                    if (error)
                    {
                        /*byte cl = Read(i, Register.LBA1);
                        byte ch = Read(i, Register.LBA2);

                        if (cl == 0x14 && ch == 0xEB) type = 1;
                        else if (cl == 0x69 && ch == 0x96) type = 1;
                        else
                        {
                            Logging.WriteLine(LogLevel.Trace, "Drive was not PATA or ATAPI");
                            continue;
                        }*/
                        Logging.WriteLine(LogLevel.Trace, "Drive was not PATA");
                        continue;
                    }

                    if (count >= 1000)
                    {
                        Logging.WriteLine(LogLevel.Trace, "Timed out while probing PATA drive.");
                        continue;
                    }

                    ReadBuffer(i, Register.Data, 128);

                    Device ideDevice = new Device();
                    _devices.Add(ideDevice);

                    ideDevice.Reserved = 1;
                    ideDevice.Type = 0; // PATA
                    ideDevice.Channel = i;
                    ideDevice.Drive = j;
                    ideDevice.Signature = (ushort)(_buffer[0] & 0xffff);
                    ideDevice.Capabilities = (ushort)((_buffer[24] >> 16) & 0xffff);
                    ideDevice.CommandSets = _buffer[41];

                    if ((ideDevice.CommandSets & (1 << 26)) != 0) ideDevice.Size = _buffer[50];
                    else ideDevice.Size = _buffer[30];

                    Logging.WriteLine(LogLevel.Trace, "Got HD size {0}MiB command sets {1}", ideDevice.Size / 2 / 1024, ideDevice.CommandSets);

                    byte[] byteAccessibleBuffer = Memory.Utilities.UnsafeCast<byte[]>(_buffer);
                    for (int k = 0; k < 40; k += 2)
                    {
                        ideDevice.Model[k] = byteAccessibleBuffer[54 + k + 1];
                        ideDevice.Model[k + 1] = byteAccessibleBuffer[54 + k];
                    }

                    VGA.WriteString("Model Name: ");
                    foreach (var c in ideDevice.Model) VGA.WriteChar(c);
                    VGA.WriteLine();
                }
            }
        }

        public byte ReadRegister(byte channel, Register register)
        {
            uint result = 0;
            byte reg = (byte)register;

            if (reg > 0x07 && reg < 0x0C)
                WriteRegister(channel, Register.Control, (byte)(0x80 | _channels[channel].DisableInterrupts));

            if (reg < 0x08)
                result = CPUHelper.CPU.InDxByte((ushort)(_channels[channel].Base + reg - 0x00));
            else if (reg < 0x0C)
                result = CPUHelper.CPU.InDxByte((ushort)(_channels[channel].Base + reg - 0x06));
            else if (reg < 0x0E)
                result = CPUHelper.CPU.InDxByte((ushort)(_channels[channel].Control + reg - 0x0C));
            else if (reg < 0x16)
                result = CPUHelper.CPU.InDxByte((ushort)(_channels[channel].BusMasterIDE + reg - 0x0E));

            if (reg > 0x07 && reg < 0x0C)
                WriteRegister(channel, Register.Control, _channels[channel].DisableInterrupts);

            return (byte)result;
        }

        public void WriteRegister(byte channel, Register register, byte data)
        {
            byte reg = (byte)register;

            if (reg > 0x07 && reg < 0x0C)
                WriteRegister(channel, Register.Control, (byte)(0x80 | _channels[channel].DisableInterrupts));

            if (reg < 0x08)
                CPUHelper.CPU.OutDxAl((ushort)(_channels[channel].Base + reg - 0x00), data);
            else if (reg < 0x0C)
                CPUHelper.CPU.OutDxAl((ushort)(_channels[channel].Base + reg - 0x06), data);
            else if (reg < 0x0E)
                CPUHelper.CPU.OutDxAl((ushort)(_channels[channel].Control + reg - 0x0C), data);
            else if (reg < 0x16)
                CPUHelper.CPU.OutDxAl((ushort)(_channels[channel].BusMasterIDE + reg - 0x0E), data);

            if (reg > 0x07 && reg < 0x0C)
                WriteRegister(channel, Register.Control, _channels[channel].DisableInterrupts);
        }

        public void ReadBuffer(byte channel, Register register, int size)
        {
            var reg = (byte)register;

            if (reg > 0x07 && reg < 0x0C)
                WriteRegister(channel, Register.Control, (byte)(0x80 | _channels[channel].DisableInterrupts));

            uint arrayBase = Memory.Utilities.ObjectToPtr(_buffer) + 8;
            ushort dx = 0;

            if (reg < 0x08)
                dx = (ushort)(_channels[channel].Base + reg);
            else if (reg < 0x0C)
                dx = (ushort)(_channels[channel].Base + reg - 0x06);
            else if (reg < 0x0E)
                dx = (ushort)(_channels[channel].Control + reg - 0x0C);
            else if (reg < 0x16)
                dx = (ushort)(_channels[channel].BusMasterIDE + reg - 0x0E);

            CPUHelper.CPU.InDxMultiDword(dx, arrayBase, size);

            if (reg > 0x07 && reg < 0x0C)
                WriteRegister(channel, Register.Control, _channels[channel].DisableInterrupts);
        }

        public byte Poll(byte channel, int advancedCheck)
        {
            // delay 400ns
            for (int i = 0; i < 4; i++)
                ReadRegister(channel, Register.AltStatus);

            // wait for busy to clear
            while ((ReadRegister(channel, Register.Status) & (byte)Status.Busy) != 0)
            {
                Logging.WriteLine(LogLevel.Trace, "[PATA.Poll] Waiting for busy to clear");
            }

            if (advancedCheck != 0)
            {
                var state = (Status)ReadRegister(channel, Register.Status);

                if ((state & Status.Error) != 0) return 2;              // error
                if ((state & Status.DriveWriteFault) != 0) return 1;    // device fault
                if ((state & Status.DataRequestReady) == 0) return 3;   // drq should be set
            }

            return 0;
        }

        private byte[] lbaIo = new byte[6];

        public byte Access(byte direction, byte drive, uint lba, byte numSectors, ushort selector, uint[] data)
        {
            byte lbaMode;
            byte channel = _devices[drive].Channel;
            byte slaveBit = _devices[drive].Drive;
            ushort bus = _channels[channel].Base;
            ushort cylinder;
            byte head, sector, err = 0;

            WriteRegister(channel, Register.Control, 2);    // make sure interrupts are disabled for now

            if (lba >= 0x10000000)
            {
                // lba48
                lbaMode = 2;
                lbaIo[0] = (byte)lba;
                lbaIo[1] = (byte)(lba >> 8);
                lbaIo[2] = (byte)(lba >> 16);
                lbaIo[3] = (byte)(lba >> 24);
                lbaIo[4] = 0; // LBA28 is integer, so 32-bits are enough to access 2TB.
                lbaIo[5] = 0; // LBA28 is integer, so 32-bits are enough to access 2TB.
                head = 0; // Lower 4-bits of HDDEVSEL are not used here.
            }
            else if ((_devices[drive].Capabilities & 0x200) == 0x200)
            {
                // lba28
                lbaMode = 1;
                lbaIo[0] = (byte)lba;
                lbaIo[1] = (byte)(lba >> 8);
                lbaIo[2] = (byte)(lba >> 16);
                lbaIo[3] = 0; // These Registers are not used here.
                lbaIo[4] = 0; // These Registers are not used here.
                lbaIo[5] = 0; // These Registers are not used here.
                head = (byte)((lba & 0xF000000) >> 24);
            }
            else
            {
                // chs
                lbaMode = 0;
                sector = (byte)((lba % 63) + 1);
                cylinder = (ushort)((lba + 1 - sector) / (16 * 63));
                lbaIo[0] = sector;
                lbaIo[1] = (byte)((cylinder >> 0) & 0xFF);
                lbaIo[2] = (byte)((cylinder >> 8) & 0xFF);
                lbaIo[3] = 0;
                lbaIo[4] = 0;
                lbaIo[5] = 0;
                head = (byte)((lba + 1 - sector) % (16 * 63) / (63)); // Head number is written to HDDEVSEL lower 4-bits.
            }

            // wait for drive to be not busy
            int count = 0;
            while (((Status)ReadRegister(channel, Register.Status) & Status.Busy) == Status.Busy && count < 1000) count++;

            if (count >= 1000)
            {
                Logging.WriteLine(LogLevel.Panic, "Timed out while waiting for ATA not busy");
                while (true) ;
            }

            if (lbaMode == 0)
            {
                WriteRegister(channel, Register.HDDevSel, (byte)(0xA0 | (slaveBit << 4) | head));   // CHS mode
            }
            else
            {
                WriteRegister(channel, Register.HDDevSel, (byte)(0xE0 | (slaveBit << 4) | head));   // LBA mode
            }

            if (lbaMode == 2)
            {
                WriteRegister(channel, Register.SectorCount1, 0);
                WriteRegister(channel, Register.LBA3, lbaIo[3]);
                WriteRegister(channel, Register.LBA4, lbaIo[4]);
                WriteRegister(channel, Register.LBA5, lbaIo[5]);
            }
            WriteRegister(channel, Register.SectorCount0, numSectors);
            WriteRegister(channel, Register.LBA0, lbaIo[0]);
            WriteRegister(channel, Register.LBA1, lbaIo[1]);
            WriteRegister(channel, Register.LBA2, lbaIo[2]);

            // not supporting dma yet, so force to 0
            WriteRegister(channel, Register.Command, (byte)GetAccessCommand(lbaMode, 0, direction));

            uint arrayBase = Memory.Utilities.ObjectToPtr(data) + 8;

            // the below is for pio reads, this would need to be in an if statement and have a dma branch once dma is supported
            if (direction == 0)
            {
                // read
                for (uint i = 0; i < numSectors; i++)
                {
                    err = Poll(channel, 1);
                    if (err != 0)
                    {
                        Logging.WriteLine(LogLevel.Trace, "Got err {0}.  Aborting {1}", err, i);
                        return err;
                    }

                    CPUHelper.CPU.InDxMultiDword(bus, (i << 9) + arrayBase, 128);
                }
            }
            else
            {
                for (uint i = 0; i < numSectors; i++)
                {
                    err = Poll(channel, 0);
                    if (err != 0)
                    {
                        Logging.WriteLine(LogLevel.Trace, "Got err {0}.  Aborting {1}", err, i);
                        return err;
                    }

                    CPUHelper.CPU.OutDxMultiDword(bus, (i << 9) + arrayBase, 128);
                }

                if (lbaMode == 0 || lbaMode == 1) WriteRegister(channel, Register.Command, (byte)Command.Cache_Flush);
                else WriteRegister(channel, Register.Command, (byte)Command.Cache_Flush_Ext);

                err = Poll(channel, 0);
            }

            return err;
        }

        private Command GetAccessCommand(byte lbaMode, byte dma, byte direction)
        {
            if (lbaMode == 0 && dma == 0 && direction == 0) return Command.Read_PIO;
            if (lbaMode == 1 && dma == 0 && direction == 0) return Command.Read_PIO;
            if (lbaMode == 2 && dma == 0 && direction == 0) return Command.Read_PIO_Ext;
            if (lbaMode == 0 && dma == 1 && direction == 0) return Command.Read_DMA;
            if (lbaMode == 1 && dma == 1 && direction == 0) return Command.Read_DMA;
            if (lbaMode == 2 && dma == 1 && direction == 0) return Command.Read_DMA_Ext;
            if (lbaMode == 0 && dma == 0 && direction == 1) return Command.Write_PIO;
            if (lbaMode == 1 && dma == 0 && direction == 1) return Command.Write_PIO;
            if (lbaMode == 2 && dma == 0 && direction == 1) return Command.Write_PIO_Ext;
            if (lbaMode == 0 && dma == 1 && direction == 1) return Command.Write_DMA;
            if (lbaMode == 1 && dma == 1 && direction == 1) return Command.Write_DMA;
            if (lbaMode == 2 && dma == 1 && direction == 1) return Command.Write_DMA_Ext;

            Logging.WriteLine(LogLevel.Panic, "Unexpected access command sequence");
            return Command.Read;
        }
    }
}
