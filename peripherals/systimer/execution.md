# SYSTIMER -- Execution Tracker

## Status
| Phase              | Status      | Date       |
|--------------------|-------------|------------|
| Planning           | complete    | 2026-03-29 |
| Test firmware      | complete    | 2026-03-29 |
| Hardware baseline  | not started |            |
| QEMU baseline      | not started |            |
| Renode impl        | complete    | 2026-03-29 |
| Robot test         | not started |            |
| Code review        | not started |            |
| Merged             | not started |            |

## Verification Levels
| Level | Check                        | Status      | Evidence |
|-------|------------------------------|-------------|----------|
| L1    | C# compiles                  | not tested  |          |
| L2    | Renode loads without crash    | not tested  |          |
| L3    | Boot progress >= previous     | not tested  |          |

## Planning Notes

SYSTIMER at 0x60023000, size 0x1000. Provides:
- Two 52-bit counters (UNIT0, UNIT1) clocked at 16 MHz
- Three alarm comparators (TARGET0, TARGET1, TARGET2)
- One-shot and periodic alarm modes
- Three interrupt outputs (GPIO 0/1/2) mapping to interrupt matrix sources 37/38/39

Register map:
- 0x00: CONF (CLK_EN, UNIT_WORK_EN, TARGET_WORK_EN, stall enables)
- 0x04/0x08: UNIT0/1_OP (UPDATE trigger bit 30, VALUE_VALID bit 29)
- 0x0C-0x18: UNIT0/1 LOAD_HI/LO (values to load into counters)
- 0x1C-0x30: TARGET0/1/2 HI/LO (alarm comparison values)
- 0x34-0x3C: TARGET0/1/2 CONF (PERIOD bits 25:0, PERIOD_MODE bit 30, UNIT_SEL bit 31)
- 0x40-0x4C: UNIT0/1 VALUE_HI/LO (read-only latched counter values)
- 0x50-0x58: COMP0/1/2_LOAD (write trigger to arm comparator)
- 0x5C/0x60: UNIT0/1_LOAD (write trigger to load counter from LOAD_HI/LO)
- 0x64: INT_ENA (3 bits: TARGET0/1/2)
- 0x68: INT_RAW (set by hardware on alarm match)
- 0x6C: INT_CLR (write-1-to-clear)
- 0x70: INT_ST (RAW & ENA, read-only)
- 0xFC: DATE (default 0x02006171)

Key behaviors:
- Counter UNIT0 enabled by default (CONF bit 30 = 1)
- Writing UNIT_OP with bit 30 latches current counter into VALUE_HI/LO and sets VALUE_VALID
- Writing COMP_LOAD arms the corresponding alarm comparator
- Writing UNIT_LOAD loads counter from LOAD_HI/LO registers
- Periodic mode: on alarm match, target auto-advances by PERIOD value
- One-shot mode: alarm fires once, then disarms

Previous Python stub behavior:
- Counter advanced by 5000 on each UNIT_OP write (latch trigger)
- No actual timer-driven counter advancement
- No alarm comparison logic (interrupts injected from .resc script)
- INT_RAW writable from outside to simulate alarm (hack)

C# implementation improvements:
- Real timer-driven counter at 16 MHz via Renode LimitTimer
- Automatic alarm comparison with interrupt generation
- GPIO outputs for interrupt matrix integration (sources 37/38/39)
- Periodic and one-shot alarm modes
- Proper latch/load semantics for counters and targets

## Implementation Log

### 2026-03-29: Initial C# implementation
- Created ESP32C3_SysTimer.cs replacing Python stub
- Uses LimitTimer at 16 MHz with 100us tick interval (1600 ticks)
- Implements all registers from systimer_reg.h
- 3 GPIO outputs for interrupt matrix sources 37/38/39
- INT_ENA/RAW/CLR/ST with write-1-to-clear semantics
- Supports both one-shot and periodic alarm modes
