// ESP32-C3 IO MUX Controller for Renode
//
// Controls per-pin mux selection, pull-up/down, drive strength, input enable,
// sleep-mode overrides, and clock output configuration.
//
// Base: DR_REG_IO_MUX_BASE = 0x60009000, Size: 0x1000
//
// Register map:
//   0x00:        PIN_CTRL (clock output config, pad power select)
//   0x04-0x58:   GPIO0-GPIO21 per-pin config (22 pins, 4 bytes each)
//   0xFC:        DATE (version register, default 0x2006050)
//
// Per-pin register bit fields (PERIPHS_IO_MUX_x_U):
//   [0]    SLP_OE     - Output enable in sleep mode
//   [1]    SLP_SEL    - Use sleep config (select sleep-mode overrides)
//   [2]    SLP_PD     - Pulldown enable in sleep mode
//   [3]    SLP_PU     - Pullup enable in sleep mode
//   [4]    SLP_IE     - Input enable in sleep mode
//   [6:5]  SLP_DRV    - Drive strength in sleep mode (0-3)
//   [7]    FUN_PD     - Pulldown enable
//   [8]    FUN_PU     - Pullup enable
//   [9]    FUN_IE     - Input enable
//   [11:10] FUN_DRV   - Drive strength (0-3, default 2 for SPI pins)
//   [14:12] MCU_SEL   - Function select (0=default, 1=GPIO, 2+=peripheral)
//   [15]   FILTER_EN  - Input filter enable
//
// Pin name mapping (GPIO number -> IO MUX register):
//   GPIO0  = XTAL_32K_P  (0x04)    GPIO11 = VDD_SPI   (0x30)
//   GPIO1  = XTAL_32K_N  (0x08)    GPIO12 = SPIHD     (0x34)
//   GPIO2  = GPIO2       (0x0C)    GPIO13 = SPIWP     (0x38)
//   GPIO3  = GPIO3       (0x10)    GPIO14 = SPICS0    (0x3C)
//   GPIO4  = MTMS        (0x14)    GPIO15 = SPICLK    (0x40)
//   GPIO5  = MTDI        (0x18)    GPIO16 = SPID      (0x44)
//   GPIO6  = MTCK        (0x1C)    GPIO17 = SPIQ      (0x48)
//   GPIO7  = MTDO        (0x20)    GPIO18 = GPIO18    (0x4C)
//   GPIO8  = GPIO8       (0x24)    GPIO19 = GPIO19    (0x50)
//   GPIO9  = GPIO9       (0x28)    GPIO20 = U0RXD     (0x54)
//   GPIO10 = GPIO10      (0x2C)    GPIO21 = U0TXD     (0x58)
//
// Reset values:
//   Most pins reset to 0x0000.
//   SPI flash pins (GPIO12-17) reset with FUN_DRV=2, FUN_IE=1 (0x0E00)
//     to support SPI flash boot.
//   GPIO20 (U0RXD) resets with FUN_IE=1, FUN_PU=1 (0x0300) for UART RX.
//   GPIO8 resets with FUN_PU=1 (0x0100) for boot strapping pull-up.

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class ESP32C3_IOMUX : BasicDoubleWordPeripheral, IKnownSize
    {
        public ESP32C3_IOMUX(Machine machine) : base(machine)
        {
            pinRegs = new uint[NumberOfPins];
            SetDefaults();
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            SetDefaults();
        }

        public long Size => 0x1000;

        private void SetDefaults()
        {
            // Most pins reset to 0
            for (int i = 0; i < NumberOfPins; i++)
            {
                pinRegs[i] = 0;
            }

            // SPI flash pins (GPIO12-17): FUN_DRV=2 (bits [11:10]=0b10), FUN_IE=1 (bit 9)
            // Reset value = (2 << 10) | (1 << 9) = 0x0A00
            for (int i = 12; i <= 17; i++)
            {
                pinRegs[i] = 0x0A00;
            }

            // GPIO8: FUN_PU=1 (bit 8) for boot strapping pull-up
            // Reset value = (1 << 8) = 0x0100
            pinRegs[8] = 0x0100;

            // GPIO20 (U0RXD): FUN_IE=1 (bit 9), FUN_PU=1 (bit 8) for UART RX input
            // Reset value = (1 << 9) | (1 << 8) = 0x0300
            pinRegs[20] = 0x0300;
        }

        private void DefineRegisters()
        {
            // PIN_CTRL: 0x00 (clock output config, pad power select)
            // [3:0]   CLK_OUT1 - Clock output 1 selection (4 bits)
            // [7:4]   CLK_OUT2 - Clock output 2 selection (4 bits)
            // [11:8]  CLK_OUT3 - Clock output 3 selection (4 bits)
            // [14:12] PAD_POWER_SWITCH_DELAY - Pad power switch delay (3 bits)
            // [15]    PAD_POWER_SEL - Pad power domain select
            Registers.PinCtrl.Define(this)
                .WithValueField(0, 4, name: "CLK_OUT1")
                .WithValueField(4, 4, name: "CLK_OUT2")
                .WithValueField(8, 4, name: "CLK_OUT3")
                .WithValueField(12, 3, name: "PAD_POWER_SWITCH_DELAY")
                .WithFlag(15, name: "PAD_POWER_SEL")
                .WithReservedBits(16, 16);

            // Per-pin registers: GPIO0 at 0x04, GPIO1 at 0x08, ..., GPIO21 at 0x58
            for (int i = 0; i < NumberOfPins; i++)
            {
                DefinePinRegister(i);
            }

            // DATE: 0xFC (version register)
            Registers.Date.Define(this, 0x2006050)
                .WithValueField(0, 28, name: "IO_MUX_DATE")
                .WithReservedBits(28, 4);
        }

        private void DefinePinRegister(int pin)
        {
            int idx = pin;
            uint resetValue = pinRegs[idx];
            long offset = 0x04 + pin * 4;
            ((Registers)offset).Define(this, resetValue)
                .WithFlag(0, name: $"GPIO{idx}_SLP_OE",
                    writeCallback: (_, value) => SetBit(idx, 0, value),
                    valueProviderCallback: _ => GetBit(idx, 0))
                .WithFlag(1, name: $"GPIO{idx}_SLP_SEL",
                    writeCallback: (_, value) => SetBit(idx, 1, value),
                    valueProviderCallback: _ => GetBit(idx, 1))
                .WithFlag(2, name: $"GPIO{idx}_SLP_PD",
                    writeCallback: (_, value) => SetBit(idx, 2, value),
                    valueProviderCallback: _ => GetBit(idx, 2))
                .WithFlag(3, name: $"GPIO{idx}_SLP_PU",
                    writeCallback: (_, value) => SetBit(idx, 3, value),
                    valueProviderCallback: _ => GetBit(idx, 3))
                .WithFlag(4, name: $"GPIO{idx}_SLP_IE",
                    writeCallback: (_, value) => SetBit(idx, 4, value),
                    valueProviderCallback: _ => GetBit(idx, 4))
                .WithValueField(5, 2, name: $"GPIO{idx}_SLP_DRV",
                    writeCallback: (_, value) => SetField(idx, 5, 2, (uint)value),
                    valueProviderCallback: _ => GetField(idx, 5, 2))
                .WithFlag(7, name: $"GPIO{idx}_FUN_PD",
                    writeCallback: (_, value) => SetBit(idx, 7, value),
                    valueProviderCallback: _ => GetBit(idx, 7))
                .WithFlag(8, name: $"GPIO{idx}_FUN_PU",
                    writeCallback: (_, value) => SetBit(idx, 8, value),
                    valueProviderCallback: _ => GetBit(idx, 8))
                .WithFlag(9, name: $"GPIO{idx}_FUN_IE",
                    writeCallback: (_, value) => SetBit(idx, 9, value),
                    valueProviderCallback: _ => GetBit(idx, 9))
                .WithValueField(10, 2, name: $"GPIO{idx}_FUN_DRV",
                    writeCallback: (_, value) => SetField(idx, 10, 2, (uint)value),
                    valueProviderCallback: _ => GetField(idx, 10, 2))
                .WithValueField(12, 3, name: $"GPIO{idx}_MCU_SEL",
                    writeCallback: (_, value) => SetField(idx, 12, 3, (uint)value),
                    valueProviderCallback: _ => GetField(idx, 12, 3))
                .WithFlag(15, name: $"GPIO{idx}_FILTER_EN",
                    writeCallback: (_, value) => SetBit(idx, 15, value),
                    valueProviderCallback: _ => GetBit(idx, 15))
                .WithReservedBits(16, 16);
        }

        private void SetBit(int pin, int bit, bool value)
        {
            if (value)
                pinRegs[pin] |= (1u << bit);
            else
                pinRegs[pin] &= ~(1u << bit);
        }

        private bool GetBit(int pin, int bit)
        {
            return (pinRegs[pin] & (1u << bit)) != 0;
        }

        private void SetField(int pin, int shift, int width, uint value)
        {
            uint mask = ((1u << width) - 1u) << shift;
            pinRegs[pin] = (pinRegs[pin] & ~mask) | ((value << shift) & mask);
        }

        private uint GetField(int pin, int shift, int width)
        {
            return (pinRegs[pin] >> shift) & ((1u << width) - 1u);
        }

        private const int NumberOfPins = 22;

        private uint[] pinRegs;

        private enum Registers : long
        {
            PinCtrl = 0x00,
            // GPIO0-GPIO21: 0x04 + n*4 (defined dynamically)
            Date    = 0xFC,
        }
    }
}
