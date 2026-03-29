// ESP32-C3 GPIO Controller for Renode
//
// Controls GPIO pins 0-21: output levels, output enable, input, interrupts,
// and function muxing (input/output signal selection).
//
// Base: DR_REG_GPIO_BASE = 0x60004000, Size: 0x1000
//
// Register groups:
//   0x00:       BT_SELECT
//   0x04-0x0C:  OUT / OUT_W1TS / OUT_W1TC (output level)
//   0x1C:       SDIO_SELECT
//   0x20-0x28:  ENABLE / ENABLE_W1TS / ENABLE_W1TC (output enable)
//   0x38:       STRAP (read-only, boot strapping pins)
//   0x3C:       IN (read-only, input level)
//   0x44-0x4C:  STATUS / STATUS_W1TS / STATUS_W1TC (interrupt status)
//   0x5C:       PCPU_INT (read-only, pro CPU interrupt status)
//   0x60:       PCPU_NMI_INT (read-only)
//   0x64:       CPUSDIO_INT (read-only)
//   0x74-0xCC:  PIN0-PIN21 (per-pin config: int type, wakeup, pad driver)
//   0x154-0x350: FUNC0-127_IN_SEL_CFG (input function mux)
//   0x554-0x5A8: FUNC0-21_OUT_SEL_CFG (output function mux)
//   0x62C:      CLOCK_GATE
//   0x6FC:      DATE (version register)
//
// W1TS/W1TC semantics: writing a 1 sets/clears the corresponding bit in
// the data register; reads always return 0.

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class ESP32C3_GPIO : BasicDoubleWordPeripheral, IKnownSize
    {
        public ESP32C3_GPIO(Machine machine) : base(machine)
        {
            pinConfig = new uint[NumberOfPins];
            funcInSelCfg = new uint[NumberOfInputSignals];
            funcOutSelCfg = new uint[NumberOfPins];

            // Default: OUT_SEL = 0x80 (no function selected)
            for (int i = 0; i < NumberOfPins; i++)
            {
                funcOutSelCfg[i] = 0x80;
            }

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            outData = 0;
            enableData = 0;
            statusInt = 0;

            for (int i = 0; i < NumberOfPins; i++)
            {
                pinConfig[i] = 0;
                funcOutSelCfg[i] = 0x80;
            }

            for (int i = 0; i < NumberOfInputSignals; i++)
            {
                funcInSelCfg[i] = 0;
            }
        }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            // BT_SELECT: 0x00
            Registers.BtSelect.Define(this)
                .WithValueField(0, 32, name: "BT_SEL");

            // GPIO_OUT_REG: 0x04 (output level, bits [25:0])
            Registers.Out.Define(this)
                .WithValueField(0, 26, name: "OUT_DATA",
                    writeCallback: (_, value) => outData = (uint)value & PinMask,
                    valueProviderCallback: _ => outData);

            // GPIO_OUT_W1TS_REG: 0x08 (write-1-to-set output)
            Registers.OutW1ts.Define(this)
                .WithValueField(0, 26, mode: FieldMode.Write, name: "OUT_W1TS",
                    writeCallback: (_, value) => outData |= (uint)value & PinMask);

            // GPIO_OUT_W1TC_REG: 0x0C (write-1-to-clear output)
            Registers.OutW1tc.Define(this)
                .WithValueField(0, 26, mode: FieldMode.Write, name: "OUT_W1TC",
                    writeCallback: (_, value) => outData &= ~((uint)value & PinMask));

            // SDIO_SELECT: 0x1C
            Registers.SdioSelect.Define(this)
                .WithValueField(0, 8, name: "SDIO_SEL");

            // GPIO_ENABLE_REG: 0x20 (output enable, bits [25:0])
            Registers.Enable.Define(this)
                .WithValueField(0, 26, name: "ENABLE_DATA",
                    writeCallback: (_, value) => enableData = (uint)value & PinMask,
                    valueProviderCallback: _ => enableData);

            // GPIO_ENABLE_W1TS_REG: 0x24
            Registers.EnableW1ts.Define(this)
                .WithValueField(0, 26, mode: FieldMode.Write, name: "ENABLE_W1TS",
                    writeCallback: (_, value) => enableData |= (uint)value & PinMask);

            // GPIO_ENABLE_W1TC_REG: 0x28
            Registers.EnableW1tc.Define(this)
                .WithValueField(0, 26, mode: FieldMode.Write, name: "ENABLE_W1TC",
                    writeCallback: (_, value) => enableData &= ~((uint)value & PinMask));

            // GPIO_STRAP_REG: 0x38 (read-only, boot strapping)
            Registers.Strap.Define(this)
                .WithValueField(0, 16, mode: FieldMode.Read, name: "STRAPPING",
                    valueProviderCallback: _ => 0);

            // GPIO_IN_REG: 0x3C (read-only, input level)
            // For emulation: returns output data (loopback, independent of enable)
            Registers.In.Define(this)
                .WithValueField(0, 26, mode: FieldMode.Read, name: "IN_DATA",
                    valueProviderCallback: _ => outData);

            // GPIO_STATUS_REG: 0x44 (interrupt status, bits [25:0])
            Registers.Status.Define(this)
                .WithValueField(0, 26, name: "STATUS_INT",
                    writeCallback: (_, value) => statusInt = (uint)value & PinMask,
                    valueProviderCallback: _ => statusInt);

            // GPIO_STATUS_W1TS_REG: 0x48
            Registers.StatusW1ts.Define(this)
                .WithValueField(0, 26, mode: FieldMode.Write, name: "STATUS_W1TS",
                    writeCallback: (_, value) => statusInt |= (uint)value & PinMask);

            // GPIO_STATUS_W1TC_REG: 0x4C
            Registers.StatusW1tc.Define(this)
                .WithValueField(0, 26, mode: FieldMode.Write, name: "STATUS_W1TC",
                    writeCallback: (_, value) => statusInt &= ~((uint)value & PinMask));

            // PCPU_INT: 0x5C (read-only, pro CPU interrupt status)
            Registers.PcpuInt.Define(this)
                .WithValueField(0, 26, mode: FieldMode.Read, name: "PROCPU_INT",
                    valueProviderCallback: _ => 0u);

            // PCPU_NMI_INT: 0x60 (read-only)
            Registers.PcpuNmiInt.Define(this)
                .WithValueField(0, 26, mode: FieldMode.Read, name: "PROCPU_NMI_INT",
                    valueProviderCallback: _ => 0u);

            // CPUSDIO_INT: 0x64 (read-only)
            Registers.CpuSdioInt.Define(this)
                .WithValueField(0, 26, mode: FieldMode.Read, name: "SDIO_INT",
                    valueProviderCallback: _ => 0u);

            // GPIO_PINn_REG: 0x74 + n*4, for n = 0..21
            // Fields: [17:13]=INT_ENA, [12:11]=CONFIG, [10]=WAKEUP_ENABLE,
            //         [9:7]=INT_TYPE, [4:3]=SYNC1_BYPASS, [2]=PAD_DRIVER, [1:0]=SYNC2_BYPASS
            for (int i = 0; i < NumberOfPins; i++)
            {
                DefinePinRegister(i);
            }

            // GPIO_FUNCn_IN_SEL_CFG_REG: 0x154 + n*4, for n = 0..127
            // Fields: [6]=SIG_IN_SEL, [5]=FUNC_IN_INV_SEL, [4:0]=FUNC_IN_SEL
            for (int i = 0; i < NumberOfInputSignals; i++)
            {
                DefineFuncInSelRegister(i);
            }

            // GPIO_FUNCn_OUT_SEL_CFG_REG: 0x554 + n*4, for n = 0..21
            // Fields: [10]=OEN_INV_SEL, [9]=OEN_SEL, [8]=OUT_INV_SEL, [7:0]=OUT_SEL
            for (int i = 0; i < NumberOfPins; i++)
            {
                DefineFuncOutSelRegister(i);
            }

            // GPIO_CLOCK_GATE_REG: 0x62C
            Registers.ClockGate.Define(this, 0x1)
                .WithFlag(0, name: "CLK_EN");

            // GPIO_DATE_REG: 0x6FC (version, default 0x2006130)
            Registers.Date.Define(this, 0x2006130)
                .WithValueField(0, 28, name: "DATE");
        }

        private void DefinePinRegister(int pin)
        {
            int idx = pin;
            long offset = 0x74 + pin * 4;
            ((Registers)offset).Define(this)
                .WithValueField(0, 2, name: $"PIN{idx}_SYNC2_BYPASS",
                    writeCallback: (_, value) =>
                    {
                        pinConfig[idx] = (pinConfig[idx] & ~0x3u) | ((uint)value & 0x3u);
                    },
                    valueProviderCallback: _ => pinConfig[idx] & 0x3u)
                .WithFlag(2, name: $"PIN{idx}_PAD_DRIVER",
                    writeCallback: (_, value) =>
                    {
                        if (value)
                            pinConfig[idx] |= (1u << 2);
                        else
                            pinConfig[idx] &= ~(1u << 2);
                    },
                    valueProviderCallback: _ => (pinConfig[idx] & (1u << 2)) != 0)
                .WithValueField(3, 2, name: $"PIN{idx}_SYNC1_BYPASS",
                    writeCallback: (_, value) =>
                    {
                        pinConfig[idx] = (pinConfig[idx] & ~(0x3u << 3)) | (((uint)value & 0x3u) << 3);
                    },
                    valueProviderCallback: _ => (pinConfig[idx] >> 3) & 0x3u)
                .WithReservedBits(5, 2)
                .WithValueField(7, 3, name: $"PIN{idx}_INT_TYPE",
                    writeCallback: (_, value) =>
                    {
                        pinConfig[idx] = (pinConfig[idx] & ~(0x7u << 7)) | (((uint)value & 0x7u) << 7);
                    },
                    valueProviderCallback: _ => (pinConfig[idx] >> 7) & 0x7u)
                .WithFlag(10, name: $"PIN{idx}_WAKEUP_ENABLE",
                    writeCallback: (_, value) =>
                    {
                        if (value)
                            pinConfig[idx] |= (1u << 10);
                        else
                            pinConfig[idx] &= ~(1u << 10);
                    },
                    valueProviderCallback: _ => (pinConfig[idx] & (1u << 10)) != 0)
                .WithValueField(11, 2, name: $"PIN{idx}_CONFIG",
                    writeCallback: (_, value) =>
                    {
                        pinConfig[idx] = (pinConfig[idx] & ~(0x3u << 11)) | (((uint)value & 0x3u) << 11);
                    },
                    valueProviderCallback: _ => (pinConfig[idx] >> 11) & 0x3u)
                .WithValueField(13, 5, name: $"PIN{idx}_INT_ENA",
                    writeCallback: (_, value) =>
                    {
                        pinConfig[idx] = (pinConfig[idx] & ~(0x1Fu << 13)) | (((uint)value & 0x1Fu) << 13);
                    },
                    valueProviderCallback: _ => (pinConfig[idx] >> 13) & 0x1Fu);
        }

        private void DefineFuncInSelRegister(int signal)
        {
            int idx = signal;
            long offset = 0x154 + signal * 4;
            ((Registers)offset).Define(this)
                .WithValueField(0, 5, name: $"FUNC{idx}_IN_SEL",
                    writeCallback: (_, value) =>
                    {
                        funcInSelCfg[idx] = (funcInSelCfg[idx] & ~0x1Fu) | ((uint)value & 0x1Fu);
                    },
                    valueProviderCallback: _ => funcInSelCfg[idx] & 0x1Fu)
                .WithFlag(5, name: $"FUNC{idx}_IN_INV_SEL",
                    writeCallback: (_, value) =>
                    {
                        if (value)
                            funcInSelCfg[idx] |= (1u << 5);
                        else
                            funcInSelCfg[idx] &= ~(1u << 5);
                    },
                    valueProviderCallback: _ => (funcInSelCfg[idx] & (1u << 5)) != 0)
                .WithFlag(6, name: $"SIG{idx}_IN_SEL",
                    writeCallback: (_, value) =>
                    {
                        if (value)
                            funcInSelCfg[idx] |= (1u << 6);
                        else
                            funcInSelCfg[idx] &= ~(1u << 6);
                    },
                    valueProviderCallback: _ => (funcInSelCfg[idx] & (1u << 6)) != 0);
        }

        private void DefineFuncOutSelRegister(int pin)
        {
            int idx = pin;
            long offset = 0x554 + pin * 4;
            ((Registers)offset).Define(this, 0x80)
                .WithValueField(0, 8, name: $"FUNC{idx}_OUT_SEL",
                    writeCallback: (_, value) =>
                    {
                        funcOutSelCfg[idx] = (funcOutSelCfg[idx] & ~0xFFu) | ((uint)value & 0xFFu);
                    },
                    valueProviderCallback: _ => funcOutSelCfg[idx] & 0xFFu)
                .WithFlag(8, name: $"FUNC{idx}_OUT_INV_SEL",
                    writeCallback: (_, value) =>
                    {
                        if (value)
                            funcOutSelCfg[idx] |= (1u << 8);
                        else
                            funcOutSelCfg[idx] &= ~(1u << 8);
                    },
                    valueProviderCallback: _ => (funcOutSelCfg[idx] & (1u << 8)) != 0)
                .WithFlag(9, name: $"FUNC{idx}_OEN_SEL",
                    writeCallback: (_, value) =>
                    {
                        if (value)
                            funcOutSelCfg[idx] |= (1u << 9);
                        else
                            funcOutSelCfg[idx] &= ~(1u << 9);
                    },
                    valueProviderCallback: _ => (funcOutSelCfg[idx] & (1u << 9)) != 0)
                .WithFlag(10, name: $"FUNC{idx}_OEN_INV_SEL",
                    writeCallback: (_, value) =>
                    {
                        if (value)
                            funcOutSelCfg[idx] |= (1u << 10);
                        else
                            funcOutSelCfg[idx] &= ~(1u << 10);
                    },
                    valueProviderCallback: _ => (funcOutSelCfg[idx] & (1u << 10)) != 0);
        }

        private const int NumberOfPins = 22;
        private const int NumberOfInputSignals = 128;
        private const uint PinMask = 0x003FFFFF; // bits [21:0] for 22 GPIOs

        private uint outData;
        private uint enableData;
        private uint statusInt;
        private uint[] pinConfig;
        private uint[] funcInSelCfg;
        private uint[] funcOutSelCfg;

        private enum Registers : long
        {
            BtSelect     = 0x00,
            Out          = 0x04,
            OutW1ts      = 0x08,
            OutW1tc      = 0x0C,
            SdioSelect   = 0x1C,
            Enable       = 0x20,
            EnableW1ts   = 0x24,
            EnableW1tc   = 0x28,
            Strap        = 0x38,
            In           = 0x3C,
            Status       = 0x44,
            StatusW1ts   = 0x48,
            StatusW1tc   = 0x4C,
            PcpuInt      = 0x5C,
            PcpuNmiInt   = 0x60,
            CpuSdioInt   = 0x64,
            // PIN0-PIN21: 0x74 + n*4 (defined dynamically)
            // FUNC0-127_IN_SEL_CFG: 0x154 + n*4 (defined dynamically)
            // FUNC0-21_OUT_SEL_CFG: 0x554 + n*4 (defined dynamically)
            ClockGate    = 0x62C,
            Date         = 0x6FC,
        }
    }
}
