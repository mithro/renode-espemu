// ESP32-C3 SPI Memory Controller (SPI1/SPI0) for Renode
//
// Minimal implementation to support esp_flash_init():
// - Accepts RDID command (0x9F) and returns a valid JEDEC ID
// - W0-W15 data buffer registers for command responses
// - CMD, ADDR, USER registers for SPI command configuration
// - STATUS register
//
// Base: DR_REG_SPI_MEM_BASE = 0x60002000 (SPI1), Size: 0x1000
//
// The key sequence for chip detection:
// 1. Firmware writes RDID command config to USER/USER1/USER2 regs
// 2. Firmware triggers command via CMD_REG bit 28 (FLASH_RDID)
// 3. Hardware executes and puts 3-byte JEDEC ID in W0_REG
// 4. Firmware reads W0_REG to get manufacturer + device ID
// 5. If ID is valid (not 0x000000 or 0xFFFFFF), init succeeds

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Memory
{
    public class ESP32C3_SpiMem : BasicDoubleWordPeripheral, IKnownSize
    {
        public ESP32C3_SpiMem(Machine machine) : base(machine)
        {
            dataBuffer = new uint[16]; // W0-W15
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            Array.Clear(dataBuffer, 0, dataBuffer.Length);
        }

        public long Size => 0x1000;

        /// <summary>
        /// Set the JEDEC flash ID returned by RDID command.
        /// Format: [23:16]=manufacturer, [15:8]=memtype, [7:0]=capacity
        /// Default: Winbond W25Q32 (0xEF4016 = 4MB)
        /// </summary>
        public uint FlashJedecId { get; set; } = 0x00EF4016; // Winbond 4MB

        private void ExecuteCommand(uint cmdBits)
        {
            // Bit 17 = SPI_MEM_USR: user-defined command mode
            // The command byte is in USER2_REG bits [31:28] (command length) and [15:0] (command value)
            if ((cmdBits & (1u << 17)) != 0)
            {
                // Extract command byte from USER2 register
                uint cmdByte = user2Value & 0xFFFF;
                this.Log(LogLevel.Debug, "SPI USR command: 0x{0:X2}", cmdByte);

                switch (cmdByte)
                {
                    case 0x9F: // RDID — Read JEDEC ID
                        dataBuffer[0] = FlashJedecId & 0x00FFFFFF;
                        this.Log(LogLevel.Info, "RDID: returning JEDEC ID 0x{0:X6}", FlashJedecId);
                        break;
                    case 0x05: // RDSR — Read Status Register 1
                        dataBuffer[0] = 0; // Not busy, write disabled
                        rdStatusValue = 0;
                        break;
                    case 0x35: // RDSR2 — Read Status Register 2
                        dataBuffer[0] = 0;
                        break;
                    case 0x15: // RDSR3 — Read Status Register 3
                        dataBuffer[0] = 0;
                        break;
                    case 0x06: // WREN — Write Enable (no-op in emulation)
                        break;
                    case 0x04: // WRDI — Write Disable (no-op)
                        break;
                    default:
                        this.Log(LogLevel.Debug, "Unhandled SPI command: 0x{0:X2}", cmdByte);
                        break;
                }
            }
            // Bit 26 = SPI_MEM_FLASH_RDSR (direct status read)
            else if ((cmdBits & (1u << 26)) != 0)
            {
                rdStatusValue = 0;
            }
            // Other direct command bits
            else if (cmdBits != 0)
            {
                this.Log(LogLevel.Debug, "SPI CMD: 0x{0:X8}", cmdBits);
            }
        }

        private void DefineRegisters()
        {
            // CMD_REG: 0x00 (command trigger)
            Registers.Cmd.Define(this)
                .WithValueField(0, 32, name: "CMD",
                    writeCallback: (_, value) =>
                    {
                        ExecuteCommand((uint)value);
                    },
                    valueProviderCallback: _ => 0); // Always reads as idle (command done)

            // ADDR_REG: 0x04
            Registers.Addr.Define(this)
                .WithValueField(0, 32, name: "ADDR");

            // CTRL_REG: 0x08
            Registers.Ctrl.Define(this)
                .WithValueField(0, 32, name: "CTRL");

            // CTRL1_REG: 0x0C
            Registers.Ctrl1.Define(this)
                .WithValueField(0, 32, name: "CTRL1");

            // CTRL2_REG: 0x10
            Registers.Ctrl2.Define(this)
                .WithValueField(0, 32, name: "CTRL2");

            // CLOCK_REG: 0x14
            Registers.Clock.Define(this)
                .WithValueField(0, 32, name: "CLOCK");

            // USER_REG: 0x18
            Registers.User.Define(this)
                .WithValueField(0, 32, name: "USER");

            // USER1_REG: 0x1C
            Registers.User1.Define(this)
                .WithValueField(0, 32, name: "USER1");

            // USER2_REG: 0x20 (bits [15:0]=command value, [31:28]=command bitlen)
            Registers.User2.Define(this)
                .WithValueField(0, 32, name: "USER2",
                    writeCallback: (_, value) => user2Value = (uint)value,
                    valueProviderCallback: _ => user2Value);

            // MOSI_DLEN_REG: 0x24
            Registers.MosiDlen.Define(this)
                .WithValueField(0, 32, name: "MOSI_DLEN");

            // MISO_DLEN_REG: 0x28
            Registers.MisoDlen.Define(this)
                .WithValueField(0, 32, name: "MISO_DLEN");

            // RD_STATUS_REG: 0x2C
            Registers.RdStatus.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "RD_STATUS",
                    valueProviderCallback: _ => rdStatusValue);

            // MISC_REG: 0x34
            Registers.Misc.Define(this)
                .WithValueField(0, 32, name: "MISC");

            // CACHE_FCTRL_REG: 0x3C
            Registers.CacheFctrl.Define(this)
                .WithValueField(0, 32, name: "CACHE_FCTRL");

            // FSM_REG: 0x54
            Registers.Fsm.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "FSM",
                    valueProviderCallback: _ => 0); // Idle

            // W0-W15 data buffer: 0x58-0x94
            for (int i = 0; i < 16; i++)
            {
                int idx = i;
                ((Registers)(0x58 + i * 4)).Define(this)
                    .WithValueField(0, 32, name: $"W{idx}",
                        writeCallback: (_, value) => dataBuffer[idx] = (uint)value,
                        valueProviderCallback: _ => dataBuffer[idx]);
            }

            // INT_RAW_REG: 0xC0
            Registers.IntRaw.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "INT_RAW",
                    valueProviderCallback: _ => 0);

            // INT_ST_REG: 0xC4
            Registers.IntSt.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "INT_ST",
                    valueProviderCallback: _ => 0);

            // INT_ENA_REG: 0xC8
            Registers.IntEna.Define(this)
                .WithValueField(0, 32, name: "INT_ENA");

            // INT_CLR_REG: 0xCC
            Registers.IntClr.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Write, name: "INT_CLR");

            // FLASH_WAITI_CTRL_REG: 0x98
            Registers.FlashWaitiCtrl.Define(this)
                .WithValueField(0, 32, name: "FLASH_WAITI_CTRL");

            // FLASH_SUS_CTRL_REG: 0x9C
            Registers.FlashSusCtrl.Define(this)
                .WithValueField(0, 32, name: "FLASH_SUS_CTRL");

            // FLASH_SUS_CMD_REG: 0xA0
            Registers.FlashSusCmd.Define(this)
                .WithValueField(0, 32, name: "FLASH_SUS_CMD");

            // SUS_STATUS_REG: 0xA4
            Registers.SusStatus.Define(this)
                .WithValueField(0, 32, name: "SUS_STATUS");

            // TIMING_CALI_REG: 0xA8
            Registers.TimingCali.Define(this)
                .WithValueField(0, 32, name: "TIMING_CALI");

            // DIN_MODE_REG: 0xAC
            Registers.DinMode.Define(this)
                .WithValueField(0, 32, name: "DIN_MODE");

            // DIN_NUM_REG: 0xB0
            Registers.DinNum.Define(this)
                .WithValueField(0, 32, name: "DIN_NUM");

            // DOUT_MODE_REG: 0xB4
            Registers.DoutMode.Define(this)
                .WithValueField(0, 32, name: "DOUT_MODE");

            // CLOCK_GATE_REG: 0xDC
            Registers.ClockGate.Define(this, 0x00000001) // CLK_EN=1
                .WithValueField(0, 32, name: "CLOCK_GATE");

            // DATE_REG: 0x3FC
            Registers.Date.Define(this, 0x02007210)
                .WithValueField(0, 28, name: "DATE")
                .WithReservedBits(28, 4);
        }

        private uint[] dataBuffer;
        private uint rdStatusValue;
        private uint user2Value;

        private enum Registers : long
        {
            Cmd = 0x00,
            Addr = 0x04,
            Ctrl = 0x08,
            Ctrl1 = 0x0C,
            Ctrl2 = 0x10,
            Clock = 0x14,
            User = 0x18,
            User1 = 0x1C,
            User2 = 0x20,
            MosiDlen = 0x24,
            MisoDlen = 0x28,
            RdStatus = 0x2C,
            Misc = 0x34,
            CacheFctrl = 0x3C,
            Fsm = 0x54,
            // W0-W15 at 0x58-0x94 defined dynamically
            FlashWaitiCtrl = 0x98,
            FlashSusCtrl = 0x9C,
            FlashSusCmd = 0xA0,
            SusStatus = 0xA4,
            TimingCali = 0xA8,
            DinMode = 0xAC,
            DinNum = 0xB0,
            DoutMode = 0xB4,
            ClockGate = 0xDC,
            IntRaw = 0xC0,
            IntSt = 0xC4,
            IntEna = 0xC8,
            IntClr = 0xCC,
            Date = 0x3FC,
        }
    }
}
