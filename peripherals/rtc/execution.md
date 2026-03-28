# RTC Controller -- Execution Tracker

## Status
| Phase              | Status      | Date       |
|--------------------|-------------|------------|
| Planning           | complete    | 2026-03-29 |
| Test firmware      | not started |            |
| Hardware baseline  | blocked     | rpi4-esp unreachable |
| QEMU baseline      | not started |            |
| Renode impl        | complete    | 2026-03-29 |
| Robot test         | not started |            |
| Code review        | not started |            |
| Merged             | not started |            |

## Verification Levels
| Level | Check                        | Status | Evidence |
|-------|------------------------------|--------|----------|
| L1    | C# compiles                  | PASS   | Renode loads ESP32C3_RTC.cs without error |
| L2    | Renode loads without crash    | PASS   | hello_world boots with RTC peripheral |
| L3    | Boot progress >= previous     | PASS   | 5/5 maintained, XTAL_FREQ warning eliminated |

## Planning Notes

RTC Controller at 0x60008000, size 0x800. Manages:
- Reset state (RESET_STATE at 0x38): boot reason
- RTC time (TIME_LOW0/HIGH0 at 0x10/0x14): incrementing counter
- Clock config (CLK_CONF at 0x70)
- STORE0-7: general-purpose registers used by ROM for boot state
- STORE4 (0xB8) = RTC_XTAL_FREQ_REG: crystal frequency (40MHz = 0x00280028)
- Brownout detection (BROWN_OUT at 0xD8)
- Interrupt registers (INT_ENA/RAW/ST/CLR at 0x40-0x4C)

Previous Python stub had incorrect offsets:
- BROWN_OUT was at 0x4C (actually INT_CLR, correct offset is 0xD8)
- INT_RAW was at 0x74 (actually SLOW_CLK_CONF, correct offset is 0x44)

## Implementation Log

C# peripheral replaces Python stub. Key improvements:
- Correct register offsets for all boot-critical registers
- STORE4 pre-configured with 40MHz XTAL frequency (eliminates boot warning)
- Proper INT_ENA/RAW/ST/CLR with write-1-to-clear semantics
- CLK_CONF and OPTIONS0 with reasonable default values
- RESET_STATE returns POWERON_RESET (1)
