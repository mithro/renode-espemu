# SYSTEM Peripheral -- Execution Tracker

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

SYSTEM Controller at 0x600C0000, size 0x1000. Manages:
- Clock gating: PERIP_CLK_EN0/EN1 (0x10/0x14), PERIP_RST_EN0/EN1 (0x18/0x1C)
- CPU config: CPU_PER_CONF (0x08), SYSCLK_CONF (0x58)
- Cross-core interrupts: CPU_INTR_FROM_CPU_0-3 (0x28-0x34)
  - Writing 1 to bit 0 asserts GPIO output -> interrupt matrix source 50-53
  - Critical for FreeRTOS vPortYield (uses FROM_CPU_INTR0)
- Memory power: MEM_PD_MASK (0x0C), RSA_PD_CTRL (0x38)
- Cache control: CACHE_CONTROL (0x40)
- BT low-power clock: BT_LPCK_DIV_INT/FRAC (0x20/0x24)
- Date register: DATE (0xFFC, default 0x02007150)

Previous Python stub only handled offset 0x28 (returning 0 on read).

## Implementation Log

C# peripheral replaces Python stub. Key improvements:
- Full register map with correct default values from ESP-IDF header
- INumberedGPIOOutput with 4 outputs for FROM_CPU_INTR0-3
- CPU_INTR_FROM_CPU_0 properly asserts/deasserts GPIO on write
- All clock enable/reset registers with hardware defaults
- SYSCLK_CONF with XTAL=40MHz default
