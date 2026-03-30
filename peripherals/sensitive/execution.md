# Sensitive (PMS) Controller -- Execution Tracker

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

Sensitive (PMS) controller at 0x600C1000, size 0x1000. Manages:
- ROM table lock and value (0x000-0x004)
- Privilege mode selection and lock (0x008-0x00C)
- APB peripheral access control (0x010-0x014)
- Internal SRAM usage configuration (0x018-0x024)
- Cache tag/MMU access control (0x028-0x034)
- DMA peripheral PMS constraints for SPI2, UHCI0, I2S0, MAC, Backup, LC, AES, SHA, ADC_DAC (0x038-0x07C)
- DMA PMS violation monitor (0x080-0x08C)
- IRAM0/DRAM0 split line constraints (0x090-0x0A4)
- Core IRAM0/DRAM0 PMS constraints and monitors (0x0A8-0x0D4)
- Core PIF PMS constraints for all peripherals per world (0x0D8-0x100)
- Region PMS constraints with address boundaries (0x104-0x12C)
- Core PIF PMS violation monitors (0x130-0x148)
- Backup bus PMS constraints and monitor (0x14C-0x16C)
- Clock gate (0x170), date/version (0xFFC, default 0x2010200)

Previous Python stub returned 0 for all registers. Firmware configures PMS
boundaries and locks during boot; currently the memprot section is skipped
via PC redirect at 0x403804B2.

## Implementation Log

C# peripheral replaces Python stub. Key features:
- All 94 registers defined with correct offsets and default values from sensitive_reg.h
- Proper bit-level field definitions for all registers
- Lock registers (bit 0 R/W, default 0) accept writes for firmware to set lock=1
- DMA PMS constrain_1 registers default to 0x000FF0FF (all SRAM permissions granted)
- IRAM0 PMS constrain defaults to 0x001C7FFF (all world permissions granted)
- DRAM0 PMS constrain defaults to 0x0F0FF0FF (all world + ROM permissions granted)
- PIF PMS constrain registers have complex per-peripheral permission bits with gaps
- Region PMS address registers are 30-bit R/W (default 0)
- Monitor status registers are read-only with default 0
- Monitor enable/clear registers default to 0x3 (both enabled)
- CLOCK_GATE defaults to 1 (clock enabled)
- DATE register defaults to 0x02010200
