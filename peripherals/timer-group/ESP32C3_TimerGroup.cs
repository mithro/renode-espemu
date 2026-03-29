// ESP32-C3 Timer Group Peripheral for Renode
//
// Each ESP32-C3 has two timer groups (TIMG0 at 0x6001F000, TIMG1 at 0x60020000).
// Each group contains:
//   - One 54-bit general-purpose timer (Timer 0) with configurable divider
//   - One Main Watchdog Timer (MWDT) -- stubbed for emulation
//   - RTC calibration registers (used during boot for clock calibration)
//
// Register map (from timer_group_reg.h):
//   0x00: T0CONFIG     - Timer 0 configuration (divider, enable, direction, alarm)
//   0x04: T0LO         - Timer 0 counter low 32 bits (latched, read-only)
//   0x08: T0HI         - Timer 0 counter high 22 bits (latched, read-only)
//   0x0C: T0UPDATE     - Write to latch current counter into LO/HI
//   0x10: T0ALARMLO    - Alarm value low 32 bits
//   0x14: T0ALARMHI    - Alarm value high 22 bits
//   0x18: T0LOADLO     - Reload value low 32 bits
//   0x1C: T0LOADHI     - Reload value high 22 bits
//   0x20: T0LOAD       - Write any value to trigger reload from LOADLO/LOADHI
//   0x48: WDTCONFIG0   - WDT configuration (stubbed)
//   0x4C: WDTCONFIG1   - WDT prescaler (stubbed)
//   0x50: WDTCONFIG2   - WDT stage 0 timeout (stubbed)
//   0x54: WDTCONFIG3   - WDT stage 1 timeout (stubbed)
//   0x58: WDTCONFIG4   - WDT stage 2 timeout (stubbed)
//   0x5C: WDTCONFIG5   - WDT stage 3 timeout (stubbed)
//   0x60: WDTFEED      - WDT feed (stubbed)
//   0x64: WDTWPROTECT  - WDT write protect (stubbed)
//   0x68: RTCCALICFG   - RTC calibration config (bit 15 = RDY, must return 1)
//   0x6C: RTCCALICFG1  - RTC calibration result value
//   0x70: INT_ENA      - Interrupt enable (bit 0 = T0, bit 1 = WDT)
//   0x74: INT_RAW      - Raw interrupt status
//   0x78: INT_ST       - Masked interrupt status (RAW & ENA)
//   0x7C: INT_CLR      - Interrupt clear (write-1-to-clear)
//   0x80: RTCCALICFG2  - RTC calibration timeout config
//   0xF8: NTIMERS_DATE - Version control register
//   0xFC: REGCLK       - Clock gate register
//
// Boot-critical behavior:
//   ROM/bootloader reads RTCCALICFG to check RTC_CALI_RDY (bit 15).
//   We always return RDY=1 with a reasonable calibration value.

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class ESP32C3_TimerGroup : BasicDoubleWordPeripheral, IKnownSize
    {
        public ESP32C3_TimerGroup(Machine machine) : base(machine)
        {
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
            wdtWriteProtect = 0x50D83AA1; // default: locked
            DefineRegisters();
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
        }

        public long Size => 0x100;

        private void AdvanceTimer()
        {
            if (!timerEnabled)
                return;

            // Advance by a fixed step per access (emulation approximation)
            if (timerIncrease)
                timerCounter += 1000;
            else if (timerCounter >= 1000)
                timerCounter -= 1000;
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
            }
        }

        private void DefineRegisters()
        {
            // --- Timer 0 registers ---

            // T0CONFIG: 0x00
            // [9]=USE_XTAL, [10]=ALARM_EN, [12]=DIVCNT_RST, [28:13]=DIVIDER,
            // [29]=AUTORELOAD, [30]=INCREASE, [31]=EN
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

            // T0UPDATE: 0x0C (write bit 31 to latch counter)
            Registers.T0Update.Define(this)
                .WithValueField(0, 30, name: "T0UPDATE_RESERVED")
                .WithTag("T0UPDATE_RESERVED_30", 30, 1)
                .WithFlag(31, name: "T0_UPDATE",
                    writeCallback: (_, value) =>
                    {
                        // Any write triggers latch (per TRM: "After writing 0 or 1")
                        AdvanceTimer();
                        latchedCounter = timerCounter;
                        CheckAlarm();
                    },
                    valueProviderCallback: _ => false); // always reads 0 (latch complete)

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

            // --- Watchdog Timer registers (stubbed) ---

            // WDTCONFIG0: 0x48
            Registers.WdtConfig0.Define(this)
                .WithValueField(0, 32, name: "WDTCONFIG0");

            // WDTCONFIG1: 0x4C
            Registers.WdtConfig1.Define(this)
                .WithValueField(0, 32, name: "WDTCONFIG1");

            // WDTCONFIG2: 0x50 (stage 0 timeout, default 26000000)
            Registers.WdtConfig2.Define(this, 0x018CBA80)
                .WithValueField(0, 32, name: "WDT_STG0_HOLD");

            // WDTCONFIG3: 0x54 (stage 1 timeout, default 0x07FFFFFF)
            Registers.WdtConfig3.Define(this, 0x07FFFFFF)
                .WithValueField(0, 32, name: "WDT_STG1_HOLD");

            // WDTCONFIG4: 0x58 (stage 2 timeout, default 0x000FFFFF)
            Registers.WdtConfig4.Define(this, 0x000FFFFF)
                .WithValueField(0, 32, name: "WDT_STG2_HOLD");

            // WDTCONFIG5: 0x5C (stage 3 timeout, default 0x000FFFFF)
            Registers.WdtConfig5.Define(this, 0x000FFFFF)
                .WithValueField(0, 32, name: "WDT_STG3_HOLD");

            // WDTFEED: 0x60
            Registers.WdtFeed.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Write, name: "WDT_FEED");

            // WDTWPROTECT: 0x64 (write 0x50D83AA1 to unlock WDT registers)
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

            // RTCCALICFG: 0x68
            // [12]=START_CYCLING, [14:13]=CLK_SEL, [15]=RDY(RO), [30:16]=MAX, [31]=START
            Registers.RtcCaliCfg.Define(this, 0x00013000) // default: START_CYCLING=1, CLK_SEL=1
                .WithValueField(0, 12, name: "RTCCALICFG_RESERVED_0_11")
                .WithFlag(12, name: "RTC_CALI_START_CYCLING")
                .WithValueField(13, 2, name: "RTC_CALI_CLK_SEL")
                .WithFlag(15, mode: FieldMode.Read, name: "RTC_CALI_RDY",
                    valueProviderCallback: _ => true) // always ready
                .WithValueField(16, 15, name: "RTC_CALI_MAX")
                .WithFlag(31, name: "RTC_CALI_START");

            // RTCCALICFG1: 0x6C
            // [0]=CYCLING_DATA_VLD(RO), [31:7]=VALUE(RO)
            Registers.RtcCaliCfg1.Define(this)
                .WithFlag(0, mode: FieldMode.Read, name: "RTC_CALI_CYCLING_DATA_VLD",
                    valueProviderCallback: _ => true) // valid
                .WithValueField(1, 6, name: "RTCCALICFG1_RESERVED_1_6")
                .WithValueField(7, 25, mode: FieldMode.Read, name: "RTC_CALI_VALUE",
                    valueProviderCallback: _ =>
                    {
                        // Return a reasonable calibration value.
                        // For 8MHz internal RC vs XTAL: ~(8M/150kHz)*cycles
                        // The Python stub returned 0x01000000 which is value=0x20000 at bits [31:7].
                        // Use a value that indicates roughly correct clock ratio.
                        return 0x20000u;
                    });

            // --- Interrupt registers ---

            // INT_ENA: 0x70 (bit 0 = T0, bit 1 = WDT)
            Registers.IntEna.Define(this)
                .WithValueField(0, 2, name: "INT_ENA",
                    writeCallback: (_, value) => intEna = (uint)value,
                    valueProviderCallback: _ => intEna);

            // INT_RAW: 0x74 (bit 0 = T0, bit 1 = WDT; read-only, set by hardware)
            Registers.IntRaw.Define(this)
                .WithValueField(0, 2, mode: FieldMode.Read, name: "INT_RAW",
                    valueProviderCallback: _ => intRaw);

            // INT_ST: 0x78 (masked: RAW & ENA, read-only)
            Registers.IntSt.Define(this)
                .WithValueField(0, 2, mode: FieldMode.Read, name: "INT_ST",
                    valueProviderCallback: _ => intRaw & intEna);

            // INT_CLR: 0x7C (write-1-to-clear)
            Registers.IntClr.Define(this)
                .WithValueField(0, 2, mode: FieldMode.Write, name: "INT_CLR",
                    writeCallback: (_, value) => intRaw &= ~(uint)value);

            // --- RTCCALICFG2: 0x80 ---
            // [0]=TIMEOUT(RO), [6:3]=TIMEOUT_RST_CNT, [31:7]=TIMEOUT_THRES
            Registers.RtcCaliCfg2.Define(this, 0xFFFFFF18) // default: RST_CNT=3, THRES=0x1FFFFFF
                .WithFlag(0, mode: FieldMode.Read, name: "RTC_CALI_TIMEOUT",
                    valueProviderCallback: _ => false) // no timeout
                .WithValueField(1, 2, name: "RTCCALICFG2_RESERVED_1_2")
                .WithValueField(3, 4, name: "RTC_CALI_TIMEOUT_RST_CNT")
                .WithValueField(7, 25, name: "RTC_CALI_TIMEOUT_THRES");

            // --- Date and Clock registers ---

            // NTIMERS_DATE: 0xF8 (version register, default 0x02006191 = 33579409)
            Registers.NTimersDate.Define(this, 0x02006191)
                .WithValueField(0, 28, name: "NTIMGS_DATE");

            // REGCLK: 0xFC (clock gate)
            // [29]=WDT_CLK_IS_ACTIVE, [30]=TIMER_CLK_IS_ACTIVE, [31]=CLK_EN
            Registers.RegClk.Define(this, 0x60000000) // default: WDT + timer clocks active
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
