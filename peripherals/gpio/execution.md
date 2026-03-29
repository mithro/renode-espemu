# GPIO Controller -- Execution Tracker

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

GPIO Controller at 0x60004000, size 0x1000. Manages:
- Output level (OUT at 0x04, W1TS at 0x08, W1TC at 0x0C)
- Output enable (ENABLE at 0x20, W1TS at 0x24, W1TC at 0x28)
- Input level (IN at 0x3C, read-only)
- Boot strapping (STRAP at 0x38, read-only)
- Interrupt status (STATUS at 0x44, W1TS at 0x48, W1TC at 0x4C)
- Per-pin config (PIN0-21 at 0x74+n*4: int type, wakeup, pad driver)
- Input function mux (FUNC0-127_IN_SEL_CFG at 0x154+n*4)
- Output function mux (FUNC0-21_OUT_SEL_CFG at 0x554+n*4, default OUT_SEL=0x80)
- Clock gate (0x62C), date/version (0x6FC, default 0x2006130)

ESP32-C3 has 22 GPIO pins (0-21). Register data fields use 26 bits (bits [25:0])
but only pins 0-21 are valid. W1TS/W1TC registers are write-only (read returns 0).

During boot, firmware configures GPIO pins for sleep isolation via PINn_REG
and function mux registers.

## Implementation Log

C# peripheral replaces Python stub. Key features:
- Full W1TS/W1TC semantics for OUT, ENABLE, and STATUS register groups
- Per-pin configuration registers (PIN0-PIN21) with all documented bit fields
- 128 input function mux registers (FUNC_IN_SEL_CFG)
- 22 output function mux registers (FUNC_OUT_SEL_CFG) with default OUT_SEL=0x80
- Read-only registers: STRAP, IN, PCPU_INT, PCPU_NMI_INT, CPUSDIO_INT
- IN_REG returns loopback of output data ANDed with enable (no external pin sim yet)
- CLOCK_GATE defaults to enabled (CLK_EN=1)
- DATE register with hardware version 0x2006130
