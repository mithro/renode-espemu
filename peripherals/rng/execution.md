# RNG -- Execution Tracker

## Status
| Phase              | Status      | Date       |
|--------------------|-------------|------------|
| Planning           | complete    | 2026-03-29 |
| Test firmware      | complete    | 2026-03-29 |
| Hardware baseline  | blocked     | rpi4-esp unreachable |
| QEMU baseline      | not started |            |
| Renode impl        | complete    | 2026-03-29 |
| Robot test         | not started |            |
| Code review        | not started |            |
| Merged             | not started |            |

## Verification Levels
| Level | Check                        | Status | Evidence |
|-------|------------------------------|--------|----------|
| L1    | Implementation compiles      |        |          |
| L2    | Renode loads without crash    |        |          |
| L3    | Boot progress >= previous     |        |          |
| L4    | UART matches hardware baseline|        |          |
| L5    | Register trace matches QEMU   |        |          |

## Planning Notes

The ESP32-C3 RNG is a single register: WDEV_RND_REG at 0x600260B0.
It falls within the SYSCON address range (0x60026000-0x60026FFF).
Reading it returns a hardware-generated random 32-bit value.

Implementation: Python LFSR in the SYSCON stub at offset 0xB0.
Future: when SYSCON gets a proper C# peripheral, RNG becomes a register within it.

## Test Firmware Design

Reads WDEV_RND_REG 10 times, prints values. Checks:
- Values are not all zero
- Values are not all identical (basic randomness check)
- At least 3 distinct values out of 10 reads
