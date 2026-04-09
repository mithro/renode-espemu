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
            latchedRtcTime = 0;
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

            // TIME_UPDATE: 0x0C (write bit 31 to trigger latch, bit 31 auto-clears)
            Registers.TimeUpdate.Define(this)
                .WithFlag(31, mode: FieldMode.Read | FieldMode.Write, name: "RTC_CNTL_TIME_UPDATE",
                    writeCallback: (_, value) =>
                    {
                        if (value)
                        {
                            // Trigger time latch: advance counter and snapshot
                            rtcTimeCounter += 1000;
                            latchedRtcTime = rtcTimeCounter;
                        }
                    },
                    // On hardware, bit 31 auto-clears after the latch completes.
                    // Firmware does read-modify-write (REG_READ | (1<<31)), so if
                    // this bit stays set, the readback shows 0x80000000 instead of 0.
                    valueProviderCallback: _ => false)
                .WithValueField(0, 31, name: "TIME_UPDATE_RESERVED");

            // TIME_LOW0: 0x10 (read-only, low 32 bits of latched RTC time)
            Registers.TimeLow0.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "TIME_LOW0",
                    valueProviderCallback: _ => (uint)(latchedRtcTime & 0xFFFFFFFF));

            // TIME_HIGH0: 0x14 (read-only, high 16 bits)
            Registers.TimeHigh0.Define(this)
                .WithValueField(0, 16, mode: FieldMode.Read, name: "TIME_HIGH0",
                    valueProviderCallback: _ => (uint)((latchedRtcTime >> 32) & 0xFFFF));

            // STATE0: 0x18
            Registers.State0.Define(this)
                .WithValueField(0, 32, name: "STATE0");

            // TIMER1-6: 0x1C-0x30
            Registers.Timer1.Define(this, 0x28140403).WithValueField(0, 32, name: "TIMER1");
            Registers.Timer2.Define(this, 0x01000000).WithValueField(0, 32, name: "TIMER2");
            Registers.Timer3.Define(this, 0x0A080A08).WithValueField(0, 32, name: "TIMER3");
            Registers.Timer4.Define(this, 0x10200A08).WithValueField(0, 32, name: "TIMER4");
            Registers.Timer5.Define(this, 0x00008000).WithValueField(0, 32, name: "TIMER5");
            Registers.Timer6.Define(this, 0x0A080000).WithValueField(0, 32, name: "TIMER6");

            // ANA_CONF: 0x34
            Registers.AnaConf.Define(this, 0x00C40000)
                .WithValueField(0, 32, name: "ANA_CONF");

            // RESET_STATE: 0x38 (reset reason for CPU)
            // Bits [5:0] = RESET_CAUSE_PROCPU (1 = POWERON_RESET)
            // Header default is 0x3000 but we override bit 0 for POWERON_RESET
            Registers.ResetState.Define(this, 0x00003001)
                .WithValueField(0, 32, name: "RESET_STATE");

            // WAKEUP_STATE: 0x3C
            Registers.WakeupState.Define(this, 0x00060000)
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

            // EXT_XTL_CONF through CPU_PERIOD_CONF: 0x60-0x6C
            Registers.ExtXtlConf.Define(this, 0x00066C80).WithValueField(0, 32, name: "EXT_XTL_CONF");
            Registers.ExtWakeupConf.Define(this).WithValueField(0, 32, name: "EXT_WAKEUP_CONF");
            Registers.SlpRejectConf.Define(this).WithValueField(0, 32, name: "SLP_REJECT_CONF");
            Registers.CpuPeriodConf.Define(this).WithValueField(0, 32, name: "CPU_PERIOD_CONF");

            // CLK_CONF: 0x70
            Registers.ClkConf.Define(this, 0x11583218)
                .WithValueField(0, 32, name: "CLK_CONF");

            // SLOW_CLK_CONF: 0x74
            Registers.SlowClkConf.Define(this, 0x00400000)
                .WithValueField(0, 32, name: "SLOW_CLK_CONF");

            // SDIO_CONF: 0x78
            Registers.SdioConf.Define(this, 0x0AB0BE0A)
                .WithValueField(0, 32, name: "SDIO_CONF");

            // BIAS_CONF: 0x7C
            // Hardware reset value is 0x00010800. The previous default 0xA0010800
            // had bits 31:29 set, which ESP-IDF clears during boot initialization.
            Registers.BiasConf.Define(this, 0x00010800)
                .WithValueField(0, 32, name: "BIAS_CONF");

            // PWC: 0x84 (no register at 0x80 per header)
            Registers.Pwc.Define(this)
                .WithValueField(0, 32, name: "PWC");

            // DIG registers: 0x88-0x8C
            Registers.DigPwc.Define(this, 0x00555010).WithValueField(0, 32, name: "DIG_PWC");
            Registers.DigIsoReg.Define(this, 0xAA805080).WithValueField(0, 32, name: "DIG_ISO");

            // RTC WDT registers: 0x90-0xA8 (accept writes silently, firmware configures WDT)
            Registers.Wdtconfig0.Define(this, 0x00013214).WithValueField(0, 32, name: "WDTCONFIG0");
            Registers.Wdtconfig1.Define(this, 0x00030D40).WithValueField(0, 32, name: "WDTCONFIG1");
            Registers.Wdtconfig2.Define(this, 0x00013880).WithValueField(0, 32, name: "WDTCONFIG2");
            Registers.Wdtconfig3.Define(this, 0x00000FFF).WithValueField(0, 32, name: "WDTCONFIG3");
            Registers.Wdtconfig4.Define(this, 0x00000FFF).WithValueField(0, 32, name: "WDTCONFIG4");
            Registers.Wdtfeed.Define(this).WithValueField(0, 32, name: "WDTFEED");
            Registers.Wdtwprotect.Define(this, 0x50D83AA1).WithValueField(0, 32, name: "WDTWPROTECT");

            // SWD (super watchdog): 0xAC-0xB0
            // SWD_CONF bit 31 (SWD_AUTO_FEED_EN) is set by ESP-IDF during boot,
            // so the post-boot value on HW is 0x84B00000. Default at reset is 0x04B00000.
            Registers.SwdConf.Define(this, 0x04B00000).WithValueField(0, 32, name: "SWD_CONF");
            // SWD_WPROTECT resets to 0 on hardware. The unlock key 0x8F1D312A is
            // written by firmware to unlock SWD registers; it should not be the default.
            Registers.SwdWprotect.Define(this).WithValueField(0, 32, name: "SWD_WPROTECT");

            // SW_CPU_STALL: 0xB4
            Registers.SwCpuStall.Define(this).WithValueField(0, 32, name: "SW_CPU_STALL");

            // STORE4-7: 0xB8-0xC4
            DefineStoreRegister(Registers.Store4, 4);
            DefineStoreRegister(Registers.Store5, 5);
            DefineStoreRegister(Registers.Store6, 6);
            DefineStoreRegister(Registers.Store7, 7);

            // LOW_POWER_ST: 0xC8
            Registers.LowPowerSt.Define(this)
                .WithValueField(0, 32, name: "LOW_POWER_ST");

            // DIAG0: 0xCC
            Registers.Diag0.Define(this)
                .WithValueField(0, 32, name: "DIAG0");

            // PAD_HOLD: 0xD0
            Registers.PadHold.Define(this)
                .WithValueField(0, 32, name: "PAD_HOLD");

            // DIG_PAD_HOLD: 0xD4
            Registers.DigPadHold.Define(this)
                .WithValueField(0, 32, name: "DIG_PAD_HOLD");

            // BROWN_OUT: 0xD8
            Registers.BrownOut.Define(this, 0x43FF0010)
                .WithValueField(0, 32, name: "BROWN_OUT");

            // TIME_LOW1: 0xDC
            Registers.TimeLow1.Define(this)
                .WithValueField(0, 32, name: "TIME_LOW1");

            // TIME_HIGH1: 0xE0
            Registers.TimeHigh1.Define(this)
                .WithValueField(0, 32, name: "TIME_HIGH1");

            // XTAL32K_CLK_FACTOR: 0xE4
            Registers.Xtal32kClkFactor.Define(this)
                .WithValueField(0, 32, name: "XTAL32K_CLK_FACTOR");

            // XTAL32K_CONF: 0xE8
            Registers.Xtal32kConf.Define(this, 0x0FF00000)
                .WithValueField(0, 32, name: "XTAL32K_CONF");

            // USB_CONF: 0xEC
            Registers.UsbConf.Define(this)
                .WithValueField(0, 32, name: "USB_CONF");

            // SLP_REJECT_CAUSE: 0xF0
            Registers.SlpRejectCause.Define(this)
                .WithValueField(0, 32, name: "SLP_REJECT_CAUSE");

            // OPTION1: 0xF4
            Registers.Option1.Define(this)
                .WithValueField(0, 32, name: "OPTION1");

            // SLP_WAKEUP_CAUSE: 0xF8
            Registers.SlpWakeupCause.Define(this)
                .WithValueField(0, 32, name: "SLP_WAKEUP_CAUSE");

            // ULP_CP_TIMER_1: 0xFC
            Registers.UlpCpTimer1.Define(this, 0x0000C800)
                .WithValueField(0, 32, name: "ULP_CP_TIMER_1");

            // INT_ENA_W1TS: 0x100
            Registers.IntEnaW1ts.Define(this)
                .WithValueField(0, 32, name: "INT_ENA_W1TS");

            // INT_ENA_W1TC: 0x104
            Registers.IntEnaW1tc.Define(this)
                .WithValueField(0, 32, name: "INT_ENA_W1TC");

            // RETENTION_CTRL: 0x108
            Registers.RetentionCtrl.Define(this, 0xA0D00000)
                .WithValueField(0, 32, name: "RETENTION_CTRL");

            // FIB_SEL: 0x10C (firmware integrity boot select)
            Registers.FibSel.Define(this, 0x00000007)
                .WithValueField(0, 32, name: "FIB_SEL");

            // GPIO_WAKEUP: 0x110
            Registers.GpioWakeup.Define(this)
                .WithValueField(0, 32, name: "GPIO_WAKEUP");

            // DBG_SEL: 0x114
            Registers.DbgSel.Define(this)
                .WithValueField(0, 32, name: "DBG_SEL");

            // DBG_MAP: 0x118
            Registers.DbgMap.Define(this)
                .WithValueField(0, 32, name: "DBG_MAP");

            // SENSOR_CTRL: 0x11C
            Registers.SensorCtrl.Define(this)
                .WithValueField(0, 32, name: "SENSOR_CTRL");

            // DBG_SAR_SEL: 0x120
            Registers.DbgSarSel.Define(this)
                .WithValueField(0, 32, name: "DBG_SAR_SEL");

            // PG_CTRL: 0x124
            Registers.PgCtrl.Define(this)
                .WithValueField(0, 32, name: "PG_CTRL");

            // DATE: 0x1FC
            Registers.Date.Define(this, 0x02007270)
                .WithValueField(0, 32, name: "DATE");
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
        private ulong latchedRtcTime;
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
            ExtXtlConf = 0x60,
            ExtWakeupConf = 0x64,
            SlpRejectConf = 0x68,
            CpuPeriodConf = 0x6C,
            ClkConf = 0x70,
            SlowClkConf = 0x74,
            SdioConf = 0x78,
            BiasConf = 0x7C,
            Pwc = 0x84,
            DigPwc = 0x88,
            DigIsoReg = 0x8C,
            Wdtconfig0 = 0x90,
            Wdtconfig1 = 0x94,
            Wdtconfig2 = 0x98,
            Wdtconfig3 = 0x9C,
            Wdtconfig4 = 0xA0,
            Wdtfeed = 0xA4,
            Wdtwprotect = 0xA8,
            SwdConf = 0xAC,
            SwdWprotect = 0xB0,
            SwCpuStall = 0xB4,
            Store4 = 0xB8,
            Store5 = 0xBC,
            Store6 = 0xC0,
            Store7 = 0xC4,
            LowPowerSt = 0xC8,
            Diag0 = 0xCC,
            PadHold = 0xD0,
            DigPadHold = 0xD4,
            BrownOut = 0xD8,
            TimeLow1 = 0xDC,
            TimeHigh1 = 0xE0,
            Xtal32kClkFactor = 0xE4,
            Xtal32kConf = 0xE8,
            UsbConf = 0xEC,
            SlpRejectCause = 0xF0,
            Option1 = 0xF4,
            SlpWakeupCause = 0xF8,
            UlpCpTimer1 = 0xFC,
            IntEnaW1ts = 0x100,
            IntEnaW1tc = 0x104,
            RetentionCtrl = 0x108,
            FibSel = 0x10C,
            GpioWakeup = 0x110,
            DbgSel = 0x114,
            DbgMap = 0x118,
            SensorCtrl = 0x11C,
            DbgSarSel = 0x120,
            PgCtrl = 0x124,
            Date = 0x1FC,
        }
    }
}
