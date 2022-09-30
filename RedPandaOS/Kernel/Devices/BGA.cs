using CPUHelper;
using Runtime;

namespace Kernel.Devices
{
    public class BGA
    {
        private enum Register : ushort
        {
            VBE_DISPI_INDEX_ID = 0,
            VBE_DISPI_INDEX_XRES = 1,
            VBE_DISPI_INDEX_YRES = 2,
            VBE_DISPI_INDEX_BPP = 3,
            VBE_DISPI_INDEX_ENABLE = 4,
            VBE_DISPI_INDEX_BANK = 5,
            VBE_DISPI_INDEX_VIRT_WIDTH = 6,
            VBE_DISPI_INDEX_VIRT_HEIGHT = 7,
            VBE_DISPI_INDEX_X_OFFSET = 8,
            VBE_DISPI_INDEX_Y_OFFSET = 9,
        }

        public static bool IsAvailable()
        {
            //return false;

            var bgaAvailable = ReadBGARegister(Register.VBE_DISPI_INDEX_ID);

            return (bgaAvailable >= 0xB0C0 && bgaAvailable <= 0xB0C5);
        }

        public uint FrameBufferAddress;

        public BGA(PCI.PCIDevice device)
        {
            FrameBufferAddress = device.BAR0 & 0xfffffff0U;
            
            // try to detect if this is a VirtualBox device with VMSVGA enabled, which has backwards BAR values
            if (FrameBufferAddress < 0x00010000U)
            {
                FrameBufferAddress = device.BAR1 & 0xfffffff0U;
            }

            Logging.WriteLine(LogLevel.Warning, "Got device with frame buffer {0:X}", FrameBufferAddress);
        }

        public void InitializeMode(ushort width, ushort height, byte bpp)
        {
            if ((width % 8) != 0) throw new System.Exception("Unsupported BGA width");

            var bgaAvailable = ReadBGARegister(Register.VBE_DISPI_INDEX_ID);

            if (bgaAvailable != 0xB0C0) WriteBGARegister(Register.VBE_DISPI_INDEX_ID, 0xB0C0);

            WriteBGARegister(Register.VBE_DISPI_INDEX_ENABLE, 0);

            WriteBGARegister(Register.VBE_DISPI_INDEX_XRES, width);
            WriteBGARegister(Register.VBE_DISPI_INDEX_YRES, height);
            WriteBGARegister(Register.VBE_DISPI_INDEX_BPP, bpp);

            WriteBGARegister(Register.VBE_DISPI_INDEX_ENABLE, 1);
        }

        private static void WriteBGARegister(Register register, ushort data)
        {
            CPU.OutDxAx(0x01CE, (ushort)register);
            CPU.OutDxAx(0x01CF, data);
        }

        private static ushort ReadBGARegister(Register register)
        {
            CPU.OutDxAx(0x01CE, (ushort)register);
            return CPU.InDxWord(0x01CF);
        }
    }
}