# Interrupt Matrix -- Execution Tracker

## Status
| Phase              | Status      | Date       |
|--------------------|-------------|------------|
| Planning           | complete    | 2026-03-29 |
| Test firmware      | complete    | 2026-03-29 |
| Hardware baseline  | complete    | 2026-03-29 |
| Renode impl        | complete    | 2026-03-29 |
| Robot test         | complete    | 2026-03-29 |
| Code review        | complete    | 2026-03-29 |
| Merged             | complete    | 2026-03-29 |
| CLIC integration   | complete    | 2026-04-05 |

## Verification Levels
| Level | Check                        | Status | Evidence |
|-------|------------------------------|--------|----------|
| L1    | C# compiles                  | PASS   | Loads in Renode without error |
| L2    | Renode loads without crash    | PASS   | hello_world boots with intmatrix + CLIC |
| L3    | Boot progress >= previous     | PASS   | 5/5, FreeRTOS ticks via CLIC |

## Implementation Notes

C# peripheral at 0x600C2000, size 0x800. Routes 62 peripheral interrupt
sources to 32 CPU interrupt lines via mapping registers.

**Interrupt delivery via CLIC (2026-04-05):**
Outputs are wired to Renode's `CoreLocalInterruptController` (`[0-31] -> clic@[0-31]`).
CLIC automatically provides `mcause = 0x80000000 | cpu_line_number`, eliminating
the old PendingMcause property and Python mcause hook. CLIC also eliminates the
MIE force and manual kick workarounds.

Key registers:
- 0x000-0x0FF: Source mapping (62 sources, 5 bits each = CPU int line)
- 0x104: CPU_INT_ENABLE (bitmask of enabled lines)
- 0x108: CPU_INT_TYPE (level vs edge)
- 0x10C: CPU_INT_CLEAR (write-1-to-clear edge interrupts)
- 0x110: CPU_INT_EIP_STATUS (pending status)
- 0x114-0x190: CPU_INT_PRI_0-31 (4-bit priority per line)
- 0x194: CPU_INT_THRESH (priority threshold)

## Review Fixes Applied
- Removed dead `activeLine` field
- Simplified mcause assignment (removed unnecessary CPU enumeration loop)
- Removed PendingMcause property (CLIC handles mcause)
- Removed MEIP-specific logic (CLIC handles delivery)
- Changed GPIO outputs from single MEIP line to per-line CLIC inputs
