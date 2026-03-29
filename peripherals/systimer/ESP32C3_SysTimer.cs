// ESP32-C3 SYSTIMER peripheral for Renode
//
// High-resolution system timer with two 52-bit counters (UNIT0/UNIT1) and
// three alarm comparators (TARGET0/1/2). Used by ESP-IDF / FreeRTOS for
// the system tick.
//
// Base: DR_REG_SYSTIMER_BASE = 0x60023000, Size: 0x1000
//
// Counter clock: 16 MHz (XTAL_CLK / 2.5 on ESP32-C3).
// Each counter is 52 bits wide (20-bit HI + 32-bit LO).
//
// Alarm modes:
//   - One-shot (TARGET mode): fires when counter >= target value
//   - Periodic (PERIOD mode): auto-reloads target += period on each match
//
// GPIO outputs 0/1/2 map to TARGET0/1/2 alarm interrupts and connect to
// interrupt matrix sources 37/38/39.

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class ESP32C3_SysTimer : BasicDoubleWordPeripheral, IKnownSize, INumberedGPIOOutput
    {
        public ESP32C3_SysTimer(Machine machine) : base(machine)
        {
            // Counter state (52-bit counters)
            unitCounter = new ulong[NumUnits];
            unitLoadHi = new uint[NumUnits];
            unitLoadLo = new uint[NumUnits];
            latchedHi = new uint[NumUnits];
            latchedLo = new uint[NumUnits];
            unitValueValid = new bool[NumUnits];

            // Target/alarm state
            targetHi = new uint[NumTargets];
            targetLo = new uint[NumTargets];
            targetPeriod = new uint[NumTargets];
            targetPeriodMode = new bool[NumTargets];
            targetUnitSel = new uint[NumTargets];
            targetLoaded = new bool[NumTargets];

            // Interrupt state
            intEna = 0;
            intRaw = 0;

            // CONF register state
            confReg = DefaultConfReg;

            // GPIO outputs for alarm interrupts (0=TARGET0, 1=TARGET1, 2=TARGET2)
            var dict = new Dictionary<int, IGPIO>();
            for (int i = 0; i < NumTargets; i++)
            {
                dict[i] = new GPIO();
            }
            Connections = dict;

            // Create a periodic timer to advance counters and check alarms.
            // We use a Renode LimitTimer-like approach: a clock source timer that
            // fires periodically to simulate counter advancement.
            innerTimer = new LimitTimer(machine.ClockSource, CounterFrequencyHz, this,
                "systimer", limit: TimerTickInterval, direction: Direction.Ascending,
                enabled: false, autoUpdate: true, eventEnabled: true, workMode: WorkMode.Periodic);
            innerTimer.LimitReached += OnTimerTick;

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            Array.Clear(unitCounter, 0, unitCounter.Length);
            Array.Clear(unitLoadHi, 0, unitLoadHi.Length);
            Array.Clear(unitLoadLo, 0, unitLoadLo.Length);
            Array.Clear(latchedHi, 0, latchedHi.Length);
            Array.Clear(latchedLo, 0, latchedLo.Length);
            Array.Clear(unitValueValid, 0, unitValueValid.Length);
            Array.Clear(targetHi, 0, targetHi.Length);
            Array.Clear(targetLo, 0, targetLo.Length);
            Array.Clear(targetPeriod, 0, targetPeriod.Length);
            Array.Clear(targetPeriodMode, 0, targetPeriodMode.Length);
            Array.Clear(targetUnitSel, 0, targetUnitSel.Length);
            Array.Clear(targetLoaded, 0, targetLoaded.Length);
            intEna = 0;
            intRaw = 0;
            confReg = DefaultConfReg;
            innerTimer.Reset();
            UpdateTimerEnabled();
            UpdateAllInterrupts();
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public long Size => 0x1000;

        private void OnTimerTick()
        {
            // Advance enabled counters by the tick interval (number of 16MHz ticks per timer period)
            for (int unit = 0; unit < NumUnits; unit++)
            {
                if (IsUnitWorkEnabled(unit))
                {
                    unitCounter[unit] += TimerTickInterval;
                    // Wrap at 52 bits
                    unitCounter[unit] &= CounterMask;
                }
            }

            // Check alarms
            for (int target = 0; target < NumTargets; target++)
            {
                if (!IsTargetWorkEnabled(target))
                    continue;
                if (!targetLoaded[target])
                    continue;

                int unit = (int)targetUnitSel[target];
                ulong counterVal = unitCounter[unit];
                ulong targetVal = ((ulong)(targetHi[target] & 0xFFFFF) << 32) | targetLo[target];

                if (counterVal >= targetVal)
                {
                    // Alarm match
                    uint bit = 1u << target;
                    if ((intRaw & bit) == 0)
                    {
                        intRaw |= bit;
                        this.Log(LogLevel.Debug, "TARGET{0} alarm fired, counter=0x{1:X13} target=0x{2:X13}",
                            target, counterVal, targetVal);

                        if (targetPeriodMode[target])
                        {
                            // Periodic mode: advance target by period
                            ulong period = targetPeriod[target];
                            targetVal += period;
                            targetVal &= CounterMask;
                            targetHi[target] = (uint)((targetVal >> 32) & 0xFFFFF);
                            targetLo[target] = (uint)(targetVal & 0xFFFFFFFF);
                        }
                        else
                        {
                            // One-shot: disable the target
                            targetLoaded[target] = false;
                        }

                        UpdateInterrupt(target);
                    }
                }
            }
        }

        private void UpdateInterrupt(int target)
        {
            uint bit = 1u << target;
            bool active = (intRaw & bit) != 0 && (intEna & bit) != 0;
            Connections[target].Set(active);
        }

        private void UpdateAllInterrupts()
        {
            for (int i = 0; i < NumTargets; i++)
            {
                UpdateInterrupt(i);
            }
        }

        private bool IsUnitWorkEnabled(int unit)
        {
            // CONF register: bit 30 = UNIT0_WORK_EN, bit 29 = UNIT1_WORK_EN
            int bitPos = 30 - unit;
            return (confReg & (1u << bitPos)) != 0;
        }

        private bool IsTargetWorkEnabled(int target)
        {
            // CONF register: bit 24 = TARGET0_WORK_EN, bit 23 = TARGET1, bit 22 = TARGET2
            int bitPos = 24 - target;
            return (confReg & (1u << bitPos)) != 0;
        }

        private void UpdateTimerEnabled()
        {
            // Enable the inner timer if any unit or target is work-enabled
            bool anyEnabled = false;
            for (int i = 0; i < NumUnits; i++)
            {
                if (IsUnitWorkEnabled(i))
                {
                    anyEnabled = true;
                    break;
                }
            }
            innerTimer.Enabled = anyEnabled;
        }

        private void LatchUnit(int unit)
        {
            latchedLo[unit] = (uint)(unitCounter[unit] & 0xFFFFFFFF);
            latchedHi[unit] = (uint)((unitCounter[unit] >> 32) & 0xFFFFF);
            unitValueValid[unit] = true;
        }

        private void LoadUnit(int unit)
        {
            unitCounter[unit] = ((ulong)(unitLoadHi[unit] & 0xFFFFF) << 32) | unitLoadLo[unit];
            this.Log(LogLevel.Debug, "UNIT{0} loaded with 0x{1:X13}", unit, unitCounter[unit]);
        }

        private void LoadTarget(int target)
        {
            targetLoaded[target] = true;
            ulong val = ((ulong)(targetHi[target] & 0xFFFFF) << 32) | targetLo[target];
            this.Log(LogLevel.Debug, "TARGET{0} loaded, value=0x{1:X13} periodMode={2} period={3}",
                target, val, targetPeriodMode[target], targetPeriod[target]);
        }

        private void DefineRegisters()
        {
            // 0x00: SYSTIMER_CONF_REG
            // Default: bit 30 (UNIT0_WORK_EN) = 1 => 0x40000000
            Registers.Conf.Define(this, DefaultConfReg)
                .WithFlag(0, name: "SYSTIMER_CLK_FO")
                .WithReservedBits(1, 21)
                .WithFlag(22, name: "TARGET2_WORK_EN",
                    writeCallback: (_, val) => { SetConfBit(22, val); UpdateTimerEnabled(); },
                    valueProviderCallback: _ => (confReg & (1u << 22)) != 0)
                .WithFlag(23, name: "TARGET1_WORK_EN",
                    writeCallback: (_, val) => { SetConfBit(23, val); UpdateTimerEnabled(); },
                    valueProviderCallback: _ => (confReg & (1u << 23)) != 0)
                .WithFlag(24, name: "TARGET0_WORK_EN",
                    writeCallback: (_, val) => { SetConfBit(24, val); UpdateTimerEnabled(); },
                    valueProviderCallback: _ => (confReg & (1u << 24)) != 0)
                .WithFlag(25, name: "TIMER_UNIT1_CORE1_STALL_EN")
                .WithFlag(26, name: "TIMER_UNIT1_CORE0_STALL_EN")
                .WithFlag(27, name: "TIMER_UNIT0_CORE1_STALL_EN")
                .WithFlag(28, name: "TIMER_UNIT0_CORE0_STALL_EN")
                .WithFlag(29, name: "TIMER_UNIT1_WORK_EN",
                    writeCallback: (_, val) => { SetConfBit(29, val); UpdateTimerEnabled(); },
                    valueProviderCallback: _ => (confReg & (1u << 29)) != 0)
                .WithFlag(30, name: "TIMER_UNIT0_WORK_EN",
                    writeCallback: (_, val) => { SetConfBit(30, val); UpdateTimerEnabled(); },
                    valueProviderCallback: _ => (confReg & (1u << 30)) != 0)
                .WithFlag(31, name: "CLK_EN");

            // 0x04: SYSTIMER_UNIT0_OP_REG
            Registers.Unit0Op.Define(this)
                .WithReservedBits(0, 29)
                .WithFlag(29, mode: FieldMode.Read, name: "TIMER_UNIT0_VALUE_VALID",
                    valueProviderCallback: _ => unitValueValid[0])
                .WithFlag(30, mode: FieldMode.Write, name: "TIMER_UNIT0_UPDATE",
                    writeCallback: (_, val) => { if (val) LatchUnit(0); });

            // 0x08: SYSTIMER_UNIT1_OP_REG
            Registers.Unit1Op.Define(this)
                .WithReservedBits(0, 29)
                .WithFlag(29, mode: FieldMode.Read, name: "TIMER_UNIT1_VALUE_VALID",
                    valueProviderCallback: _ => unitValueValid[1])
                .WithFlag(30, mode: FieldMode.Write, name: "TIMER_UNIT1_UPDATE",
                    writeCallback: (_, val) => { if (val) LatchUnit(1); });

            // 0x0C: SYSTIMER_UNIT0_LOAD_HI_REG (20-bit)
            Registers.Unit0LoadHi.Define(this)
                .WithValueField(0, 20, name: "TIMER_UNIT0_LOAD_HI",
                    writeCallback: (_, val) => unitLoadHi[0] = (uint)val,
                    valueProviderCallback: _ => unitLoadHi[0]);

            // 0x10: SYSTIMER_UNIT0_LOAD_LO_REG
            Registers.Unit0LoadLo.Define(this)
                .WithValueField(0, 32, name: "TIMER_UNIT0_LOAD_LO",
                    writeCallback: (_, val) => unitLoadLo[0] = (uint)val,
                    valueProviderCallback: _ => unitLoadLo[0]);

            // 0x14: SYSTIMER_UNIT1_LOAD_HI_REG (20-bit)
            Registers.Unit1LoadHi.Define(this)
                .WithValueField(0, 20, name: "TIMER_UNIT1_LOAD_HI",
                    writeCallback: (_, val) => unitLoadHi[1] = (uint)val,
                    valueProviderCallback: _ => unitLoadHi[1]);

            // 0x18: SYSTIMER_UNIT1_LOAD_LO_REG
            Registers.Unit1LoadLo.Define(this)
                .WithValueField(0, 32, name: "TIMER_UNIT1_LOAD_LO",
                    writeCallback: (_, val) => unitLoadLo[1] = (uint)val,
                    valueProviderCallback: _ => unitLoadLo[1]);

            // 0x1C: SYSTIMER_TARGET0_HI_REG (20-bit)
            Registers.Target0Hi.Define(this)
                .WithValueField(0, 20, name: "TIMER_TARGET0_HI",
                    writeCallback: (_, val) => targetHi[0] = (uint)val,
                    valueProviderCallback: _ => targetHi[0]);

            // 0x20: SYSTIMER_TARGET0_LO_REG
            Registers.Target0Lo.Define(this)
                .WithValueField(0, 32, name: "TIMER_TARGET0_LO",
                    writeCallback: (_, val) => targetLo[0] = (uint)val,
                    valueProviderCallback: _ => targetLo[0]);

            // 0x24: SYSTIMER_TARGET1_HI_REG (20-bit)
            Registers.Target1Hi.Define(this)
                .WithValueField(0, 20, name: "TIMER_TARGET1_HI",
                    writeCallback: (_, val) => targetHi[1] = (uint)val,
                    valueProviderCallback: _ => targetHi[1]);

            // 0x28: SYSTIMER_TARGET1_LO_REG
            Registers.Target1Lo.Define(this)
                .WithValueField(0, 32, name: "TIMER_TARGET1_LO",
                    writeCallback: (_, val) => targetLo[1] = (uint)val,
                    valueProviderCallback: _ => targetLo[1]);

            // 0x2C: SYSTIMER_TARGET2_HI_REG (20-bit)
            Registers.Target2Hi.Define(this)
                .WithValueField(0, 20, name: "TIMER_TARGET2_HI",
                    writeCallback: (_, val) => targetHi[2] = (uint)val,
                    valueProviderCallback: _ => targetHi[2]);

            // 0x30: SYSTIMER_TARGET2_LO_REG
            Registers.Target2Lo.Define(this)
                .WithValueField(0, 32, name: "TIMER_TARGET2_LO",
                    writeCallback: (_, val) => targetLo[2] = (uint)val,
                    valueProviderCallback: _ => targetLo[2]);

            // 0x34: SYSTIMER_TARGET0_CONF_REG
            DefineTargetConfRegister(Registers.Target0Conf, 0);
            // 0x38: SYSTIMER_TARGET1_CONF_REG
            DefineTargetConfRegister(Registers.Target1Conf, 1);
            // 0x3C: SYSTIMER_TARGET2_CONF_REG
            DefineTargetConfRegister(Registers.Target2Conf, 2);

            // 0x40: SYSTIMER_UNIT0_VALUE_HI_REG (read-only, latched)
            Registers.Unit0ValueHi.Define(this)
                .WithValueField(0, 20, mode: FieldMode.Read, name: "TIMER_UNIT0_VALUE_HI",
                    valueProviderCallback: _ => latchedHi[0]);

            // 0x44: SYSTIMER_UNIT0_VALUE_LO_REG (read-only, latched)
            Registers.Unit0ValueLo.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "TIMER_UNIT0_VALUE_LO",
                    valueProviderCallback: _ => latchedLo[0]);

            // 0x48: SYSTIMER_UNIT1_VALUE_HI_REG (read-only, latched)
            Registers.Unit1ValueHi.Define(this)
                .WithValueField(0, 20, mode: FieldMode.Read, name: "TIMER_UNIT1_VALUE_HI",
                    valueProviderCallback: _ => latchedHi[1]);

            // 0x4C: SYSTIMER_UNIT1_VALUE_LO_REG (read-only, latched)
            Registers.Unit1ValueLo.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "TIMER_UNIT1_VALUE_LO",
                    valueProviderCallback: _ => latchedLo[1]);

            // 0x50: SYSTIMER_COMP0_LOAD_REG (write trigger)
            Registers.Comp0Load.Define(this)
                .WithFlag(0, mode: FieldMode.Write, name: "TIMER_COMP0_LOAD",
                    writeCallback: (_, val) => { if (val) LoadTarget(0); });

            // 0x54: SYSTIMER_COMP1_LOAD_REG
            Registers.Comp1Load.Define(this)
                .WithFlag(0, mode: FieldMode.Write, name: "TIMER_COMP1_LOAD",
                    writeCallback: (_, val) => { if (val) LoadTarget(1); });

            // 0x58: SYSTIMER_COMP2_LOAD_REG
            Registers.Comp2Load.Define(this)
                .WithFlag(0, mode: FieldMode.Write, name: "TIMER_COMP2_LOAD",
                    writeCallback: (_, val) => { if (val) LoadTarget(2); });

            // 0x5C: SYSTIMER_UNIT0_LOAD_REG (write trigger to load counter from LOAD_HI/LO)
            Registers.Unit0Load.Define(this)
                .WithFlag(0, mode: FieldMode.Write, name: "TIMER_UNIT0_LOAD",
                    writeCallback: (_, val) => { if (val) LoadUnit(0); });

            // 0x60: SYSTIMER_UNIT1_LOAD_REG
            Registers.Unit1Load.Define(this)
                .WithFlag(0, mode: FieldMode.Write, name: "TIMER_UNIT1_LOAD",
                    writeCallback: (_, val) => { if (val) LoadUnit(1); });

            // 0x64: SYSTIMER_INT_ENA_REG (3 bits: TARGET0/1/2)
            Registers.IntEna.Define(this)
                .WithValueField(0, 3, name: "INT_ENA",
                    writeCallback: (_, val) =>
                    {
                        intEna = (uint)val & 0x7;
                        UpdateAllInterrupts();
                    },
                    valueProviderCallback: _ => intEna);

            // 0x68: SYSTIMER_INT_RAW_REG (3 bits, read-only from software perspective;
            //        set by hardware on alarm match)
            Registers.IntRaw.Define(this)
                .WithValueField(0, 3, mode: FieldMode.Read, name: "INT_RAW",
                    valueProviderCallback: _ => intRaw);

            // 0x6C: SYSTIMER_INT_CLR_REG (write-1-to-clear)
            Registers.IntClr.Define(this)
                .WithValueField(0, 3, mode: FieldMode.Write, name: "INT_CLR",
                    writeCallback: (_, val) =>
                    {
                        intRaw &= ~((uint)val & 0x7);
                        UpdateAllInterrupts();
                    });

            // 0x70: SYSTIMER_INT_ST_REG (read-only: RAW & ENA)
            Registers.IntSt.Define(this)
                .WithValueField(0, 3, mode: FieldMode.Read, name: "INT_ST",
                    valueProviderCallback: _ => intRaw & intEna);

            // 0xFC: SYSTIMER_DATE_REG (default 0x02006171 = 33579377)
            Registers.Date.Define(this, 0x02006171)
                .WithValueField(0, 32, name: "DATE");
        }

        private void DefineTargetConfRegister(Registers reg, int target)
        {
            int t = target;
            reg.Define(this)
                .WithValueField(0, 26, name: $"TARGET{t}_PERIOD",
                    writeCallback: (_, val) => targetPeriod[t] = (uint)val,
                    valueProviderCallback: _ => targetPeriod[t])
                .WithReservedBits(26, 4)
                .WithFlag(30, name: $"TARGET{t}_PERIOD_MODE",
                    writeCallback: (_, val) => targetPeriodMode[t] = val,
                    valueProviderCallback: _ => targetPeriodMode[t])
                .WithFlag(31, name: $"TARGET{t}_TIMER_UNIT_SEL",
                    writeCallback: (_, val) => targetUnitSel[t] = val ? 1u : 0u,
                    valueProviderCallback: _ => targetUnitSel[t] != 0);
        }

        private void SetConfBit(int bit, bool value)
        {
            if (value)
                confReg |= 1u << bit;
            else
                confReg &= ~(1u << bit);
        }

        // Counter state
        private ulong[] unitCounter;
        private uint[] unitLoadHi;
        private uint[] unitLoadLo;
        private uint[] latchedHi;
        private uint[] latchedLo;
        private bool[] unitValueValid;

        // Target/alarm state
        private uint[] targetHi;
        private uint[] targetLo;
        private uint[] targetPeriod;
        private bool[] targetPeriodMode;
        private uint[] targetUnitSel;
        private bool[] targetLoaded;

        // Interrupt state
        private uint intEna;
        private uint intRaw;

        // CONF register backing field
        private uint confReg;

        // Inner Renode timer for counter advancement
        private readonly LimitTimer innerTimer;

        private const int NumUnits = 2;
        private const int NumTargets = 3;
        private const long CounterFrequencyHz = 16_000_000; // 16 MHz
        private const ulong CounterMask = (1UL << 52) - 1;   // 52-bit counter
        // Timer fires every 1600 counter ticks = 100 us at 16 MHz.
        // This gives reasonable alarm resolution without excessive overhead.
        private const ulong TimerTickInterval = 1600;
        // Default CONF: bit 30 (UNIT0_WORK_EN) = 1
        private const uint DefaultConfReg = 0x40000000;

        private enum Registers : long
        {
            Conf         = 0x00,
            Unit0Op      = 0x04,
            Unit1Op      = 0x08,
            Unit0LoadHi  = 0x0C,
            Unit0LoadLo  = 0x10,
            Unit1LoadHi  = 0x14,
            Unit1LoadLo  = 0x18,
            Target0Hi    = 0x1C,
            Target0Lo    = 0x20,
            Target1Hi    = 0x24,
            Target1Lo    = 0x28,
            Target2Hi    = 0x2C,
            Target2Lo    = 0x30,
            Target0Conf  = 0x34,
            Target1Conf  = 0x38,
            Target2Conf  = 0x3C,
            Unit0ValueHi = 0x40,
            Unit0ValueLo = 0x44,
            Unit1ValueHi = 0x48,
            Unit1ValueLo = 0x4C,
            Comp0Load    = 0x50,
            Comp1Load    = 0x54,
            Comp2Load    = 0x58,
            Unit0Load    = 0x5C,
            Unit1Load    = 0x60,
            IntEna       = 0x64,
            IntRaw       = 0x68,
            IntClr       = 0x6C,
            IntSt        = 0x70,
            Date         = 0xFC,
        }
    }
}
