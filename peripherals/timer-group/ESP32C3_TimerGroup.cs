// ESP32-C3 Timer Group Peripheral for Renode
//
// Each ESP32-C3 has two timer groups (TIMG0 at 0x6001F000, TIMG1 at 0x60020000).
// Each group contains:
//   - One 54-bit general-purpose timer (Timer 0) with configurable divider
//   - One Main Watchdog Timer (MWDT) -- stubbed for emulation
//   - RTC calibration registers (used during boot for clock calibration)
//
// Counter advancement: The counter advances by a scaled step on each T0UPDATE
// write and INT_RAW/INT_ST read, using a fixed time assumption per access.
// This avoids LimitTimer overhead (which stalls emulation) and deadlocks from
// accessing machine.ElapsedVirtualTime inside register callbacks.

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class ESP32C3_TimerGroup : BasicDoubleWordPeripheral, IKnownSize, INumberedGPIOOutput
    {
        public ESP32C3_TimerGroup(Machine machine) : base(machine)
        {
            // GPIO outputs for interrupts (0=T0, 1=WDT)
            var dict = new Dictionary<int, IGPIO>();
            dict[0] = new GPIO();
            dict[1] = new GPIO();
            Connections = dict;

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            timerCounter = 0;
            latchedCounter = 0;
            timerEnabled = false;
            timerIncrease = true;
            timerDivider = 1;
            alarmLo = 0;
            alarmHi = 0;
            alarmEnabled = false;
            loadLo = 0;
            loadHi = 0;
            intEna = 0;
            intRaw = 0;
            wdtWriteProtect = 0x50D83AA1;
            // START_CYCLING=1 in default 0x00013000, so calibration is running at reset
            rtcCaliStarted = true;
            UpdateInterrupt();
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public long Size => 0x100;

        /// <summary>
        /// Advance the timer counter by a scaled step.
        /// Called on T0UPDATE writes and INT_RAW/INT_ST reads.
        /// Each call represents one "time quantum" of emulation progress.
        /// </summary>
        private void AdvanceTimer()
        {
            if (!timerEnabled)
                return;

            // Each access represents roughly 100us of emulated time.
            // At APB_CLK = 80MHz, that's 8000 ticks before divider.
            // The divider scales this down: step = 8000 / divider.
            ulong step = TicksPerAccess / (ulong)timerDivider;
            if (step == 0) step = 1;

            if (timerIncrease)
                timerCounter += step;
            else if (timerCounter >= step)
                timerCounter -= step;

            timerCounter &= CounterMask;
        }

        private void CheckAlarm()
        {
            if (!alarmEnabled)
                return;

            ulong alarmValue = ((ulong)(alarmHi & 0x003FFFFF) << 32) | alarmLo;
            if (timerCounter >= alarmValue && alarmValue != 0)
            {
                intRaw |= 0x1; // T0_INT_RAW
                alarmEnabled = false; // auto-clear on alarm
                this.Log(LogLevel.Debug, "Timer 0 alarm triggered at counter=0x{0:X}", timerCounter);
                UpdateInterrupt();
            }
        }

        private void UpdateInterrupt()
        {
            bool t0Active = (intRaw & 0x1) != 0 && (intEna & 0x1) != 0;
            Connections[0].Set(t0Active);
        }

        private void DefineRegisters()
        {
            // --- Timer 0 registers ---

            // T0CONFIG: 0x00
            Registers.T0Config.Define(this, 0x60002000) // default: divider=1, autoreload=1, increase=1
                .WithValueField(0, 9, name: "T0CONFIG_RESERVED_0_8")
                .WithFlag(9, name: "T0_USE_XTAL")
                .WithFlag(10, name: "T0_ALARM_EN",
                    writeCallback: (_, value) => alarmEnabled = value,
                    valueProviderCallback: _ => alarmEnabled)
                .WithTag("T0CONFIG_RESERVED_11", 11, 1)
                .WithFlag(12, mode: FieldMode.Write, name: "T0_DIVCNT_RST",
                    writeCallback: (_, value) => { if (value) this.Log(LogLevel.Debug, "Timer 0 divider counter reset"); })
                .WithValueField(13, 16, name: "T0_DIVIDER",
                    writeCallback: (_, value) => { timerDivider = (uint)value; if (timerDivider == 0) timerDivider = 1; },
                    valueProviderCallback: _ => timerDivider)
                .WithFlag(29, name: "T0_AUTORELOAD")
                .WithFlag(30, name: "T0_INCREASE",
                    writeCallback: (_, value) => timerIncrease = value,
                    valueProviderCallback: _ => timerIncrease)
                .WithFlag(31, name: "T0_EN",
                    writeCallback: (_, value) => timerEnabled = value,
                    valueProviderCallback: _ => timerEnabled);

            // T0LO: 0x04 (read-only, latched low 32 bits)
            Registers.T0Lo.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "T0_LO",
                    valueProviderCallback: _ => (uint)(latchedCounter & 0xFFFFFFFF));

            // T0HI: 0x08 (read-only, latched high 22 bits)
            Registers.T0Hi.Define(this)
                .WithValueField(0, 22, mode: FieldMode.Read, name: "T0_HI",
                    valueProviderCallback: _ => (uint)((latchedCounter >> 32) & 0x003FFFFF));

            // T0UPDATE: 0x0C (write to latch counter; advances + checks alarm)
            Registers.T0Update.Define(this)
                .WithValueField(0, 30, name: "T0UPDATE_RESERVED")
                .WithTag("T0UPDATE_RESERVED_30", 30, 1)
                .WithFlag(31, name: "T0_UPDATE",
                    writeCallback: (_, value) =>
                    {
                        AdvanceTimer();
                        latchedCounter = timerCounter;
                        CheckAlarm();
                    },
                    valueProviderCallback: _ => false);

            // T0ALARMLO: 0x10
            Registers.T0AlarmLo.Define(this)
                .WithValueField(0, 32, name: "T0_ALARM_LO",
                    writeCallback: (_, value) => alarmLo = (uint)value,
                    valueProviderCallback: _ => alarmLo);

            // T0ALARMHI: 0x14
            Registers.T0AlarmHi.Define(this)
                .WithValueField(0, 22, name: "T0_ALARM_HI",
                    writeCallback: (_, value) => alarmHi = (uint)value,
                    valueProviderCallback: _ => alarmHi & 0x003FFFFF);

            // T0LOADLO: 0x18
            Registers.T0LoadLo.Define(this)
                .WithValueField(0, 32, name: "T0_LOAD_LO",
                    writeCallback: (_, value) => loadLo = (uint)value,
                    valueProviderCallback: _ => loadLo);

            // T0LOADHI: 0x1C
            Registers.T0LoadHi.Define(this)
                .WithValueField(0, 22, name: "T0_LOAD_HI",
                    writeCallback: (_, value) => loadHi = (uint)value,
                    valueProviderCallback: _ => loadHi & 0x003FFFFF);

            // T0LOAD: 0x20 (write-trigger: reload counter from LOADLO/LOADHI)
            Registers.T0Load.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Write, name: "T0_LOAD",
                    writeCallback: (_, __) =>
                    {
                        timerCounter = ((ulong)(loadHi & 0x003FFFFF) << 32) | loadLo;
                        this.Log(LogLevel.Debug, "Timer 0 reloaded to 0x{0:X}", timerCounter);
                    });

            // --- Watchdog Timer registers ---

            Registers.WdtConfig0.Define(this, 0x0004C000)
                .WithValueField(0, 12, name: "WDTCONFIG0_RESERVED_0_11")
                .WithFlag(12, name: "WDT_APPCPU_RESET_EN")
                .WithFlag(13, name: "WDT_PROCPU_RESET_EN")
                .WithFlag(14, name: "WDT_FLASHBOOT_MOD_EN")
                .WithValueField(15, 3, name: "WDT_SYS_RESET_LENGTH")
                .WithValueField(18, 3, name: "WDT_CPU_RESET_LENGTH")
                .WithFlag(21, name: "WDT_USE_XTAL")
                .WithFlag(22, mode: FieldMode.Write, name: "WDT_CONF_UPDATE_EN")
                .WithValueField(23, 2, name: "WDT_STG3")
                .WithValueField(25, 2, name: "WDT_STG2")
                .WithValueField(27, 2, name: "WDT_STG1")
                .WithValueField(29, 2, name: "WDT_STG0")
                .WithFlag(31, name: "WDT_EN");

            Registers.WdtConfig1.Define(this, 0x00010000)
                .WithFlag(0, mode: FieldMode.Write, name: "WDT_DIVCNT_RST",
                    writeCallback: (_, value) => { if (value) this.Log(LogLevel.Debug, "WDT divider counter reset"); })
                .WithValueField(1, 15, name: "WDTCONFIG1_RESERVED_1_15")
                .WithValueField(16, 16, name: "WDT_CLK_PRESCALE");

            Registers.WdtConfig2.Define(this, 0x018CBA80)
                .WithValueField(0, 32, name: "WDT_STG0_HOLD");

            Registers.WdtConfig3.Define(this, 0x07FFFFFF)
                .WithValueField(0, 32, name: "WDT_STG1_HOLD");

            Registers.WdtConfig4.Define(this, 0x000FFFFF)
                .WithValueField(0, 32, name: "WDT_STG2_HOLD");

            Registers.WdtConfig5.Define(this, 0x000FFFFF)
                .WithValueField(0, 32, name: "WDT_STG3_HOLD");

            Registers.WdtFeed.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Write, name: "WDT_FEED");

            Registers.WdtWProtect.Define(this, 0x50D83AA1)
                .WithValueField(0, 32, name: "WDT_WKEY",
                    writeCallback: (_, value) =>
                    {
                        wdtWriteProtect = (uint)value;
                        if (value == 0x50D83AA1)
                            this.Log(LogLevel.Debug, "WDT write-protect unlocked");
                        else
                            this.Log(LogLevel.Debug, "WDT write-protect locked");
                    },
                    valueProviderCallback: _ => wdtWriteProtect);

            // --- RTC Calibration registers ---

            Registers.RtcCaliCfg.Define(this, 0x00013000)
                .WithValueField(0, 12, name: "RTCCALICFG_RESERVED_0_11")
                .WithFlag(12, name: "RTC_CALI_START_CYCLING",
                    writeCallback: (_, val) => { if (val) rtcCaliStarted = true; })
                .WithValueField(13, 2, name: "RTC_CALI_CLK_SEL")
                .WithFlag(15, mode: FieldMode.Read, name: "RTC_CALI_RDY",
                    valueProviderCallback: _ => rtcCaliStarted)
                .WithValueField(16, 15, name: "RTC_CALI_MAX")
                .WithFlag(31, name: "RTC_CALI_START",
                    writeCallback: (_, val) => { if (val) rtcCaliStarted = true; });

            Registers.RtcCaliCfg1.Define(this)
                .WithFlag(0, mode: FieldMode.Read, name: "RTC_CALI_CYCLING_DATA_VLD",
                    valueProviderCallback: _ => rtcCaliStarted)
                .WithValueField(1, 6, name: "RTCCALICFG1_RESERVED_1_6")
                .WithValueField(7, 25, mode: FieldMode.Read, name: "RTC_CALI_VALUE",
                    valueProviderCallback: _ => rtcCaliStarted ? 0x20000u : 0u);

            // --- Interrupt registers ---

            Registers.IntEna.Define(this)
                .WithValueField(0, 2, name: "INT_ENA",
                    writeCallback: (_, value) => { intEna = (uint)value; UpdateInterrupt(); },
                    valueProviderCallback: _ => intEna);

            // INT_RAW: advance timer and check alarm before returning
            Registers.IntRaw.Define(this)
                .WithValueField(0, 2, mode: FieldMode.Read, name: "INT_RAW",
                    valueProviderCallback: _ => { AdvanceTimer(); CheckAlarm(); return intRaw; });

            // INT_ST: advance timer and check alarm before returning
            Registers.IntSt.Define(this)
                .WithValueField(0, 2, mode: FieldMode.Read, name: "INT_ST",
                    valueProviderCallback: _ => { AdvanceTimer(); CheckAlarm(); return intRaw & intEna; });

            Registers.IntClr.Define(this)
                .WithValueField(0, 2, mode: FieldMode.Write, name: "INT_CLR",
                    writeCallback: (_, value) => { intRaw &= ~(uint)value; UpdateInterrupt(); });

            // --- RTCCALICFG2: 0x80 ---
            Registers.RtcCaliCfg2.Define(this, 0xFFFFFF18)
                .WithFlag(0, mode: FieldMode.Read, name: "RTC_CALI_TIMEOUT",
                    valueProviderCallback: _ => false)
                .WithValueField(1, 2, name: "RTCCALICFG2_RESERVED_1_2")
                .WithValueField(3, 4, name: "RTC_CALI_TIMEOUT_RST_CNT")
                .WithValueField(7, 25, name: "RTC_CALI_TIMEOUT_THRES");

            // --- Date and Clock registers ---

            Registers.NTimersDate.Define(this, 0x02006191)
                .WithValueField(0, 28, name: "NTIMGS_DATE");

            Registers.RegClk.Define(this, 0x60000000)
                .WithValueField(0, 29, name: "REGCLK_RESERVED_0_28")
                .WithFlag(29, name: "WDT_CLK_IS_ACTIVE")
                .WithFlag(30, name: "TIMER_CLK_IS_ACTIVE")
                .WithFlag(31, name: "CLK_EN");
        }

        // Timer 0 state
        private ulong timerCounter;
        private ulong latchedCounter;
        private bool timerEnabled;
        private bool timerIncrease;
        private uint timerDivider;
        private uint alarmLo;
        private uint alarmHi;
        private bool alarmEnabled;
        private uint loadLo;
        private uint loadHi;

        // Interrupt state
        private uint intEna;
        private uint intRaw;

        // WDT state
        private uint wdtWriteProtect;

        // RTC calibration state
        private bool rtcCaliStarted;

        private const ulong CounterMask = (1UL << 54) - 1; // 54-bit counter
        // APB ticks advanced per register access (T0UPDATE write or INT_RAW read).
        // At 80MHz APB with divider=1, 1000000 ticks ≈ 12.5ms per access.
        // Large enough that alarms fire within 1-2 register reads, matching
        // real hardware behavior where significant time passes between accesses.
        // Counter values are timing-dependent and excluded from HW comparisons.
        private const ulong TicksPerAccess = 1_000_000;

        private enum Registers : long
        {
            T0Config    = 0x00,
            T0Lo        = 0x04,
            T0Hi        = 0x08,
            T0Update    = 0x0C,
            T0AlarmLo   = 0x10,
            T0AlarmHi   = 0x14,
            T0LoadLo    = 0x18,
            T0LoadHi    = 0x1C,
            T0Load      = 0x20,
            WdtConfig0  = 0x48,
            WdtConfig1  = 0x4C,
            WdtConfig2  = 0x50,
            WdtConfig3  = 0x54,
            WdtConfig4  = 0x58,
            WdtConfig5  = 0x5C,
            WdtFeed     = 0x60,
            WdtWProtect = 0x64,
            RtcCaliCfg  = 0x68,
            RtcCaliCfg1 = 0x6C,
            IntEna      = 0x70,
            IntRaw      = 0x74,
            IntSt       = 0x78,
            IntClr      = 0x7C,
            RtcCaliCfg2 = 0x80,
            NTimersDate = 0xF8,
            RegClk      = 0xFC,
        }
    }
}
