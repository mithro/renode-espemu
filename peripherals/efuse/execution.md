# eFuse -- Execution Tracker

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
| L1    | C# compiles                  | PASS   | Renode loads ESP32C3_eFuse.cs without error |
| L2    | Renode loads without crash    | PASS   | hello_world boots with eFuse peripheral |
| L3    | Boot progress >= previous     | PASS   | 5/5 maintained, chip rev now v0.3 (was v0.0) |
| L4    | UART matches hardware baseline|        |          |
| L5    | Register trace matches QEMU   |        |          |

## Branch
- Branch: peripheral/efuse
- Commits: (pending)

## Planning Notes

The ESP32-C3 eFuse controller at 0x60008800 provides one-time-programmable storage.
For emulation, only the **read data registers** matter — firmware reads chip revision,
MAC address, flash encryption status, etc. Programming (burn) is not needed for boot.

Key registers the firmware reads during boot:
- BLK0 read data (RD_WR_DIS, RD_REPEAT_DATA0-4): security config, SPI boot mode
- BLK1 read data (RD_MAC_SPI_SYS_0-4): MAC address, SPI pad config
- BLK2 read data (RD_SYS_PART1_DATA0-7): system parameters
- RD_REPEAT_DATA0: contains SPI_BOOT_CRYPT_CNT, DIS_DOWNLOAD_MANUAL_ENCRYPT, etc.

Current stub: returns 0 for all reads. This causes:
- Chip revision reported as v0.0 (should be v0.3 or v0.4)
- MAC address all zeros
- Flash encryption disabled (correct for dev, but wrong value)

## Test Firmware Design

Test firmware will:
1. Read all eFuse BLK0-BLK3 read data registers, print values
2. Read MAC address specifically via efuse_hal or direct register access
3. Read chip revision fields
4. Print structured output: `[EFUSE] REG_READ addr=0xNNN val=0xNNN`

## Hardware Baseline
(To be captured from rpi4-esp)

## Implementation Log
(To be filled during implementation)

## Review Notes
(To be filled during review)

## Issues
(None yet)
