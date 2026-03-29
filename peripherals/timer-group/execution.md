# Timer Group -- Execution Tracker

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
| L1    | C# compiles                  | not started |          |
| L2    | Renode loads without crash    | not started |          |
| L3    | Boot progress >= previous     | not started |          |

## Planning Notes

Timer Group peripheral at TIMG0=0x6001F000, TIMG1=0x60020000, size 0x100 each.

Each timer group contains:
- Timer 0: 54-bit counter with 16-bit prescaler, count up/down, alarm, auto-reload
- Main Watchdog Timer (MWDT): stubbed (not needed for emulation)
- RTC calibration: always returns RDY=1 with reasonable calibration value
- Interrupt registers: INT_ENA/RAW/ST/CLR with bit 0=T0, bit 1=WDT

Register map (from timer_group_reg.h):
- 0x00: T0CONFIG (divider, enable, direction, alarm enable)
- 0x04: T0LO (latched counter low 32 bits, read-only)
- 0x08: T0HI (latched counter high 22 bits, read-only)
- 0x0C: T0UPDATE (write to latch counter)
- 0x10-0x14: T0ALARMLO/HI (alarm target value)
- 0x18-0x1C: T0LOADLO/HI (reload value)
- 0x20: T0LOAD (write-trigger reload)
- 0x48-0x64: WDT registers (stubbed, accept writes silently)
- 0x68: RTCCALICFG (bit 15 = RTC_CALI_RDY, always 1)
- 0x6C: RTCCALICFG1 (calibration result value)
- 0x70-0x7C: INT_ENA/RAW/ST/CLR
- 0x80: RTCCALICFG2 (calibration timeout, always 0)
- 0xF8: NTIMERS_DATE (version register)
- 0xFC: REGCLK (clock gate control)

Boot-critical path:
- ROM reads RTCCALICFG (0x68) bit 15 to check RTC calibration ready
- Previous Python stub at platforms/scripts/stub_timg0.py handled only 0x68 and 0x6C

## Implementation Log

C# peripheral replaces Python stub. Key features:
- Full Timer 0 with latch-based counter reading (write UPDATE, read LO/HI)
- 64-bit counter with configurable divider and count direction
- Alarm support with auto-clear on trigger
- RTC calibration always returns ready with reasonable value
- WDT registers accept writes silently (watchdog not needed for emulation)
- Interrupt registers with proper write-1-to-clear semantics
- Instantiable twice for TIMG0 and TIMG1
