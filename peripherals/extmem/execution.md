# ExtMem (Cache/MMU) Controller -- Execution Tracker

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
| Level | Check                        | Status  | Evidence |
|-------|------------------------------|---------|----------|
| L1    | C# compiles                  | pending |          |
| L2    | Renode loads without crash    | pending |          |
| L3    | Boot progress >= previous     | pending |          |

## Planning Notes

ExtMem controller at 0x600C4000, size 0x1000. Manages:
- ICache enable/disable (ICACHE_CTRL at 0x00): bit 0 must return 1 during boot
- ICache bus control (ICACHE_CTRL1 at 0x04): IBUS/DBUS shut bits
- Tag memory power (ICACHE_TAG_POWER_CTRL at 0x08): power-up defaults
- Cache sync (ICACHE_SYNC_CTRL at 0x28): invalidate trigger, sync done status
- Cache preload (ICACHE_PRELOAD_CTRL at 0x34): preload trigger, done status
- Autoload (ICACHE_AUTOLOAD_CTRL at 0x40): autoload done status
- Cache state (CACHE_STATE at 0xB0): idle indicator
- Misc config (CACHE_CONF_MISC at 0xC8): MMU fault ignore bits
- Freeze control (ICACHE_FREEZE at 0xCC): freeze done status
- IBUS/DBUS PMS tables (0xD8-0xFC): permission boundaries and attributes
- MMU power/owner (0xAC, 0xC4): MMU memory power and ownership
- Interrupt registers (0x78-0x8C): illegal access and cache access interrupts

Previous Python stub (stub_extmem.py) handled only three registers:
- 0x00: ICACHE_CTRL (returned 0x1 for ICACHE_ENABLE)
- 0x28: ICACHE_SYNC_CTRL (returned 0x2 for sync done)
- 0x34: ICACHE_PRELOAD_CTRL (returned 0x2 for preload done)

## Implementation Log

C# peripheral replaces Python stub. Key improvements:
- All 65 registers defined with correct offsets and default values from TRM
- Proper bit-level field definitions matching extmem_reg.h
- "Done" status bits (sync, preload, autoload, lock, freeze) always return true
- CACHE_STATE returns idle (0x001) so firmware sees cache as ready
- ICACHE_ENABLE defaults to 1 for boot compatibility
- PMS boundary defaults match hardware (0x800 for boundary1/2)
- Encrypt/decrypt clock force-on defaults match hardware (all enabled)
