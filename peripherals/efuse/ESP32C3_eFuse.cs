// ESP32-C3 eFuse Controller for Renode
//
// Provides one-time-programmable (OTP) storage with pre-configured values.
// For emulation, only the read data registers matter — firmware reads chip
// revision, MAC address, flash encryption status, etc.
//
// Register layout at DR_REG_EFUSE_BASE (0x60008800):
//   0x000-0x028: PGM_DATA0-7 + PGM_CHECK_VALUE0-2 (programming, not needed for boot)
//   0x02C:       RD_WR_DIS (write-disable bits)
//   0x030-0x040: RD_REPEAT_DATA0-4 (BLK0 config data)
//   0x044-0x058: RD_MAC_SPI_SYS_0-5 (BLK1: MAC address, SPI config, chip version)
//   0x05C-0x078: RD_SYS_PART1_DATA0-7 (BLK2: system parameters)
//   0x07C-0x098: RD_USR_DATA0-7 (BLK3: user data)
//   0x09C-0x0B8: RD_KEY0_DATA0-7 (BLK4: key storage)
//   0x0BC-0x0D8: RD_KEY1_DATA0-7 (BLK5)
//   0x0DC-0x0F8: RD_KEY2_DATA0-7 (BLK6)
//   0x0FC-0x118: RD_KEY3_DATA0-7 (BLK7)
//   0x11C-0x138: RD_KEY4_DATA0-7 (BLK8)
//   0x13C-0x158: RD_KEY5_DATA0-7 (BLK9)
//   0x15C-0x168: RD_SYS_PART2_DATA0-7 (BLK10)
//   0x1CC:       STATUS
//   0x1D0:       CMD
//   0x1FC:       DATE

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class ESP32C3_eFuse : BasicDoubleWordPeripheral, IKnownSize
    {
        public ESP32C3_eFuse(Machine machine) : base(machine)
        {
            // Pre-populate eFuse data with default values for ESP32-C3 rev 0.3
            efuseData = new uint[TotalWords];
            SetDefaults();
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            // eFuse data survives reset (it's OTP)
        }

        public long Size => 0x200;

        /// <summary>
        /// Set the 6-byte MAC address in the eFuse block.
        /// Format: mac[0] is most significant (OUI first byte).
        /// </summary>
        public void SetMacAddress(byte[] mac)
        {
            if (mac.Length != 6)
            {
                this.Log(LogLevel.Warning, "MAC address must be 6 bytes");
                return;
            }
            // RD_MAC_SPI_SYS_0 (offset 0x44, word index 17):
            //   [7:0]=mac[5], [15:8]=mac[4], [23:16]=mac[3], [31:24]=mac[2]
            efuseData[WordIndex_MacSpiSys0] =
                ((uint)mac[2] << 24) | ((uint)mac[3] << 16) |
                ((uint)mac[4] << 8) | ((uint)mac[5]);
            // RD_MAC_SPI_SYS_1 (offset 0x48, word index 18):
            //   [7:0]=mac[1], [15:8]=mac[0]
            efuseData[WordIndex_MacSpiSys1] =
                (efuseData[WordIndex_MacSpiSys1] & 0xFFFF0000u) |
                ((uint)mac[0] << 8) | ((uint)mac[1]);

            this.Log(LogLevel.Info, "MAC set to {0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}",
                mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);
        }

        /// <summary>
        /// Set the chip revision (wafer version).
        /// </summary>
        public void SetChipRevision(uint major, uint minor)
        {
            // WAFER_VERSION_MINOR_LO: RD_MAC_SPI_SYS_3 (0x50) bits [20:18]
            uint sys3 = efuseData[WordIndex_MacSpiSys3];
            sys3 &= ~(0x7u << 18); // clear WAFER_VERSION_MINOR_LO
            sys3 |= ((minor & 0x7) << 18);
            efuseData[WordIndex_MacSpiSys3] = sys3;

            // WAFER_VERSION_MINOR_HI: RD_MAC_SPI_SYS_5 (0x58) bit [23]
            uint sys5 = efuseData[WordIndex_MacSpiSys5];
            sys5 &= ~(0x1u << 23); // clear WAFER_VERSION_MINOR_HI
            sys5 |= (((minor >> 3) & 0x1) << 23);
            // WAFER_VERSION_MAJOR: RD_MAC_SPI_SYS_5 (0x58) bits [25:24]
            sys5 &= ~(0x3u << 24); // clear WAFER_VERSION_MAJOR
            sys5 |= ((major & 0x3) << 24);
            efuseData[WordIndex_MacSpiSys5] = sys5;

            this.Log(LogLevel.Info, "Chip revision set to v{0}.{1}", major, minor);
        }

        private void SetDefaults()
        {
            Array.Clear(efuseData, 0, efuseData.Length);

            // Default MAC: 7C:DF:A1:52:45:4E (Espressif OUI + "REN" in ASCII)
            SetMacAddress(new byte[] { 0x7C, 0xDF, 0xA1, 0x52, 0x45, 0x4E });

            // Default chip revision: v0.3 (ESP32-C3 rev 3)
            SetChipRevision(0, 3);

            // DATE register (offset 0x1FC / word 127)
            efuseData[127] = 0x02012200; // version date
        }

        private void DefineRegisters()
        {
            // Read data registers: 0x2C - 0x178 (all read-only, backed by efuseData[])
            // Word index = offset / 4
            for (int offset = 0x2C; offset <= 0x178; offset += 4)
            {
                int wordIdx = offset / 4;
                int capturedIdx = wordIdx;
                ((Registers)(offset)).Define(this)
                    .WithValueField(0, 32, mode: FieldMode.Read,
                        name: $"RD_DATA_{capturedIdx:X2}",
                        valueProviderCallback: _ => efuseData[capturedIdx]);
            }

            // Programming data registers: 0x00 - 0x28 (write for burn, ignored in emulation)
            for (int offset = 0x00; offset <= 0x28; offset += 4)
            {
                ((Registers)(offset)).Define(this)
                    .WithValueField(0, 32, name: $"PGM_DATA_{offset / 4}");
            }

            // STATUS register: 0x1CC
            Registers.Status.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "STATUS",
                    valueProviderCallback: _ => 0); // idle

            // CMD register: 0x1D0
            Registers.Cmd.Define(this)
                .WithValueField(0, 32, name: "CMD",
                    writeCallback: (_, value) =>
                    {
                        if ((value & 0x1) != 0)
                            this.Log(LogLevel.Info, "eFuse read command (ignored in emulation)");
                        if ((value & 0x2) != 0)
                            this.Log(LogLevel.Warning, "eFuse program command (not supported)");
                    });

            // CLK register: 0x1C8
            Registers.Clk.Define(this)
                .WithValueField(0, 32, name: "CLK");

            // CONF register: 0x1CC
            Registers.Conf.Define(this)
                .WithValueField(0, 32, name: "CONF");

            // DATE register: 0x1FC
            Registers.Date.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "DATE",
                    valueProviderCallback: _ => efuseData[127]);

            // INT registers
            Registers.IntRaw.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "INT_RAW",
                    valueProviderCallback: _ => 0);
            Registers.IntSt.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "INT_ST",
                    valueProviderCallback: _ => 0);
            Registers.IntEna.Define(this)
                .WithValueField(0, 32, name: "INT_ENA");
            Registers.IntClr.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Write, name: "INT_CLR");
        }

        private uint[] efuseData;

        // Word indices for key registers
        private const int WordIndex_MacSpiSys0 = 0x44 / 4; // 17
        private const int WordIndex_MacSpiSys1 = 0x48 / 4; // 18
        private const int WordIndex_MacSpiSys3 = 0x50 / 4; // 20
        private const int WordIndex_MacSpiSys5 = 0x58 / 4; // 22

        private const int TotalWords = 128; // 0x200 bytes / 4

        private enum Registers : long
        {
            // Read data at 0x2C-0x178 defined dynamically
            // Programming at 0x00-0x28 defined dynamically
            Clk = 0x1C8,
            Conf = 0x1CC,
            Status = 0x1D0,
            Cmd = 0x1D4,
            IntRaw = 0x1D8,
            IntSt = 0x1DC,
            IntEna = 0x1E0,
            IntClr = 0x1E4,
            Date = 0x1FC,
        }
    }
}
