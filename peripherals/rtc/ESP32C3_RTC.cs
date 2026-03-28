// ESP32-C3 RTC Controller for Renode
//
// The RTC controller manages reset state, low-power timers, clock config,
// brownout detection, and general-purpose storage registers used by ROM/firmware.
//
// Base: DR_REG_RTCCNTL_BASE = 0x60008000, Size: 0x800
//
// Boot-critical registers:
//   0x38: RESET_STATE (reset reason, must return POWERON_RESET=1)
//   0x10/0x14: TIME_LOW0/HIGH0 (incrementing RTC counter)
//   0xB8: STORE4 = RTC_XTAL_FREQ_REG (must return 40MHz: 0x00280028)
//   0xD8: BROWN_OUT (bit 30=enable, bit 31=detect, no brownout)
//   0x50-0x5C, 0xB8-0xC4: STORE0-7 (general-purpose, used by ROM for boot state)

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class ESP32C3_RTC : BasicDoubleWordPeripheral, IKnownSize
    {
        public ESP32C3_RTC(Machine machine) : base(machine)
        {
            store = new uint[8];
            rtcTimeCounter = 0;
            intEna = 0;
            intRaw = 0;
            SetDefaults();
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            rtcTimeCounter = 0;
            intEna = 0;
            intRaw = 0;
            SetDefaults();
        }

        public long Size => 0x800;

        private void SetDefaults()
        {
            // STORE4 = RTC_XTAL_FREQ_REG: 40MHz encoded as (40 | 40<<16)
            store[4] = 0x00280028;

            // STORE0-3, STORE5-7: zero (cleared on poweron)
            store[0] = 0;
            store[1] = 0;
            store[2] = 0;
            store[3] = 0;
            store[5] = 0;
            store[6] = 0;
            store[7] = 0;
        }

        private void DefineRegisters()
        {
            // OPTIONS0: 0x00
            Registers.Options0.Define(this, 0x1C00A000)
                .WithValueField(0, 32, name: "OPTIONS0");

            // SLP_TIMER0/1: 0x04, 0x08
            Registers.SlpTimer0.Define(this)
                .WithValueField(0, 32, name: "SLP_TIMER0");
            Registers.SlpTimer1.Define(this)
                .WithValueField(0, 32, name: "SLP_TIMER1");

            // TIME_UPDATE: 0x0C (write bit 31 to trigger latch)
            Registers.TimeUpdate.Define(this)
                .WithValueField(0, 32, name: "TIME_UPDATE",
                    writeCallback: (_, value) =>
                    {
                        if ((value & 0x80000000u) != 0)
                        {
                            // Trigger time latch: advance counter
                            rtcTimeCounter += 1000;
                        }
                    });

            // TIME_LOW0: 0x10 (read-only, low 32 bits of latched RTC time)
            Registers.TimeLow0.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "TIME_LOW0",
                    valueProviderCallback: _ =>
                    {
                        // Auto-advance on read (for firmware that reads without latching)
                        rtcTimeCounter += 1000;
                        return (uint)(rtcTimeCounter & 0xFFFFFFFF);
                    });

            // TIME_HIGH0: 0x14 (read-only, high 16 bits)
            Registers.TimeHigh0.Define(this)
                .WithValueField(0, 16, mode: FieldMode.Read, name: "TIME_HIGH0",
                    valueProviderCallback: _ => (uint)((rtcTimeCounter >> 32) & 0xFFFF));

            // STATE0: 0x18
            Registers.State0.Define(this)
                .WithValueField(0, 32, name: "STATE0");

            // TIMER1-6: 0x1C-0x30
            Registers.Timer1.Define(this).WithValueField(0, 32, name: "TIMER1");
            Registers.Timer2.Define(this).WithValueField(0, 32, name: "TIMER2");
            Registers.Timer3.Define(this).WithValueField(0, 32, name: "TIMER3");
            Registers.Timer4.Define(this).WithValueField(0, 32, name: "TIMER4");
            Registers.Timer5.Define(this).WithValueField(0, 32, name: "TIMER5");
            Registers.Timer6.Define(this).WithValueField(0, 32, name: "TIMER6");

            // ANA_CONF: 0x34
            Registers.AnaConf.Define(this)
                .WithValueField(0, 32, name: "ANA_CONF");

            // RESET_STATE: 0x38 (reset reason for CPU)
            // Bits [5:0] = RESET_CAUSE_PROCPU (1 = POWERON_RESET)
            Registers.ResetState.Define(this, 0x00000001)
                .WithValueField(0, 32, name: "RESET_STATE");

            // WAKEUP_STATE: 0x3C
            Registers.WakeupState.Define(this)
                .WithValueField(0, 32, name: "WAKEUP_STATE");

            // INT_ENA: 0x40
            Registers.IntEna.Define(this)
                .WithValueField(0, 32, name: "INT_ENA",
                    writeCallback: (_, value) => intEna = (uint)value,
                    valueProviderCallback: _ => intEna);

            // INT_RAW: 0x44
            Registers.IntRaw.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "INT_RAW",
                    valueProviderCallback: _ => intRaw);

            // INT_ST: 0x48 (masked: RAW & ENA)
            Registers.IntSt.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "INT_ST",
                    valueProviderCallback: _ => intRaw & intEna);

            // INT_CLR: 0x4C (write-1-to-clear)
            Registers.IntClr.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Write, name: "INT_CLR",
                    writeCallback: (_, value) => intRaw &= ~(uint)value);

            // STORE0-3: 0x50-0x5C (general-purpose storage)
            DefineStoreRegister(Registers.Store0, 0);
            DefineStoreRegister(Registers.Store1, 1);
            DefineStoreRegister(Registers.Store2, 2);
            DefineStoreRegister(Registers.Store3, 3);

            // SLP_REJECT_CONF through CPU_PERIOD_CONF: 0x60-0x6C
            Registers.SlpWakeupConf.Define(this).WithValueField(0, 32, name: "SLP_WAKEUP_CONF");
            Registers.ExtWakeupConf.Define(this).WithValueField(0, 32, name: "EXT_WAKEUP_CONF");
            Registers.SlpRejectConf.Define(this).WithValueField(0, 32, name: "SLP_REJECT_CONF");
            Registers.CpuPeriodConf.Define(this).WithValueField(0, 32, name: "CPU_PERIOD_CONF");

            // CLK_CONF: 0x70
            Registers.ClkConf.Define(this, 0x11583218)
                .WithValueField(0, 32, name: "CLK_CONF");

            // SLOW_CLK_CONF: 0x74
            Registers.SlowClkConf.Define(this)
                .WithValueField(0, 32, name: "SLOW_CLK_CONF");

            // SDIO_CONF: 0x78
            Registers.SdioConf.Define(this)
                .WithValueField(0, 32, name: "SDIO_CONF");

            // BIAS_CONF: 0x7C
            Registers.BiasConf.Define(this)
                .WithValueField(0, 32, name: "BIAS_CONF");

            // REG (regulator): 0x80
            Registers.Reg.Define(this)
                .WithValueField(0, 32, name: "REG");

            // PWC: 0x84
            Registers.Pwc.Define(this)
                .WithValueField(0, 32, name: "PWC");

            // DIG registers: 0x88-0x94
            Registers.DigPwc.Define(this).WithValueField(0, 32, name: "DIG_PWC");
            Registers.DigIsoReg.Define(this).WithValueField(0, 32, name: "DIG_ISO");
            Registers.Wdtconfig0.Define(this).WithValueField(0, 32, name: "WDTCONFIG0");
            Registers.Wdtconfig1.Define(this).WithValueField(0, 32, name: "WDTCONFIG1");

            // STORE4-7: 0xB8-0xC4
            DefineStoreRegister(Registers.Store4, 4);
            DefineStoreRegister(Registers.Store5, 5);
            DefineStoreRegister(Registers.Store6, 6);
            DefineStoreRegister(Registers.Store7, 7);

            // BROWN_OUT: 0xD8
            // Bit 30 = brownout enable, bit 31 = brownout detected
            // Default: enabled but not detected
            Registers.BrownOut.Define(this, 0x40000000)
                .WithValueField(0, 32, name: "BROWN_OUT");
        }

        private void DefineStoreRegister(Registers reg, int index)
        {
            int idx = index;
            reg.Define(this)
                .WithValueField(0, 32, name: $"STORE{idx}",
                    writeCallback: (_, value) =>
                    {
                        store[idx] = (uint)value;
                        if (idx == 4)
                            this.Log(LogLevel.Info, "STORE4 (XTAL_FREQ) written: 0x{0:X8}", value);
                    },
                    valueProviderCallback: _ => store[idx]);
        }

        private uint[] store;
        private ulong rtcTimeCounter;
        private uint intEna;
        private uint intRaw;

        private enum Registers : long
        {
            Options0 = 0x00,
            SlpTimer0 = 0x04,
            SlpTimer1 = 0x08,
            TimeUpdate = 0x0C,
            TimeLow0 = 0x10,
            TimeHigh0 = 0x14,
            State0 = 0x18,
            Timer1 = 0x1C,
            Timer2 = 0x20,
            Timer3 = 0x24,
            Timer4 = 0x28,
            Timer5 = 0x2C,
            Timer6 = 0x30,
            AnaConf = 0x34,
            ResetState = 0x38,
            WakeupState = 0x3C,
            IntEna = 0x40,
            IntRaw = 0x44,
            IntSt = 0x48,
            IntClr = 0x4C,
            Store0 = 0x50,
            Store1 = 0x54,
            Store2 = 0x58,
            Store3 = 0x5C,
            SlpWakeupConf = 0x60,
            ExtWakeupConf = 0x64,
            SlpRejectConf = 0x68,
            CpuPeriodConf = 0x6C,
            ClkConf = 0x70,
            SlowClkConf = 0x74,
            SdioConf = 0x78,
            BiasConf = 0x7C,
            Reg = 0x80,
            Pwc = 0x84,
            DigPwc = 0x88,
            DigIsoReg = 0x8C,
            Wdtconfig0 = 0x90,
            Wdtconfig1 = 0x94,
            Store4 = 0xB8,
            Store5 = 0xBC,
            Store6 = 0xC0,
            Store7 = 0xC4,
            BrownOut = 0xD8,
        }
    }
}
