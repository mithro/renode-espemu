# ESP32-C3 Peripheral Emulation Status

> Last updated: 2026-04-07

## Summary

| Metric | Value |
|---|---|
| Boot | Hardware-like — CPU starts at ROM `_init`, no patches |
| C# peripherals | 16 (5,655 lines) |
| Python placeholders | 23 |
| ESP-IDF register headers (total scope) | 34 |
| Test firmware programs | 120 (93 functional + 27 reset-value) |
| Paired HW/Renode baselines | 93 (across 10 peripherals) |
| HW vs Renode register match | 49/81 (60%, excluding expected diffs) |
| Robot Framework tests | 12 test cases across 8 suites, all passing |
| GitHub Actions CI | hello_world suite on all branches + PRs |

## C# Peripherals (16 Implemented)

| # | Peripheral | Address | Lines | Test FW | HW Baselines | Renode Baselines | Robot Tests |
|---|---|---|---:|---:|---:|---:|---:|
| 1 | Interrupt Matrix | 0x600C2000 | 321 | 8 | 8 | 8 | 1 |
| 2 | eFuse | 0x60008800 | 260 | 9 | 9 | 9 | 3 |
| 3 | RTC Controller | 0x60008000 | 401 | 10 | 10 | 10 | 0 |
| 4 | SYSTIMER | 0x60023000 | 541 | 9 | 9 | 9 | 1 |
| 5 | Timer Group | 0x6001F000 | 359 | 10 | 10 | 10 | 1 |
| 6 | SYSTEM (DPORT) | 0x600C0000 | 326 | 8 | 8 | 8 | 1 |
| 7 | RNG / SYSCON | 0x60026000 | 356 | 6 | 6 | 6 | 1 |
| 8 | GPIO | 0x60004000 | 431 | 10 | 10 | 10 | 1 |
| 9 | ExtMem (Cache) | 0x600C4000 | 501 | 10 | 10 | 10 | 0 |
| 10 | UART0 | 0x60000000 | 348 | 10 | 10 | 10 | 0 |
| 11 | IO MUX | 0x60009000 | 204 | 3 | **0** | **0** | 0 |
| 12 | SPI MEM (Flash) | 0x60002000 | 280 | **0** | -- | -- | 0 |
| 13 | Sensitive (PMS) | 0x600C1000 | 913 | **0** | -- | -- | 0 |
| 14 | GDMA | 0x6003F000 | 182 | **0** | -- | -- | 0 |
| 15 | Assist Debug | 0x600CE000 | 175 | **0** | -- | -- | 0 |
| 16 | MMU Table | 0x600C5000 | 57 | **0** | -- | -- | 0 |

## Python Placeholders (23 Remaining)

Each has a reset-value test firmware that reads 16 registers and a hardware
baseline. None have Renode baselines yet.

| # | Peripheral | Address | Category | HW Baseline |
|---|---|---|---|---|
| 1 | SPI0 | 0x60003000 | Storage | Yes |
| 2 | FE2 (RF Frontend 2) | 0x60005000 | Wireless | Yes |
| 3 | FE (RF Frontend) | 0x60006000 | Wireless | Yes |
| 4 | RTC I2C | 0x6000E000 | Analog | Yes |
| 5 | NRX | 0x6001C000 | Wireless | Yes |
| 6 | BB (Baseband) | 0x6001E000 | Wireless | Yes |
| 7 | UART1 | 0x60010000 | Communication | Yes |
| 8 | I2C | 0x60013000 | Communication | Yes |
| 9 | UHCI0 | 0x60014000 | Communication | Yes |
| 10 | RMT | 0x60016000 | Peripheral | Yes |
| 11 | LEDC | 0x60019000 | Peripheral | Yes |
| 12 | SPI2 (GP-SPI) | 0x60024000 | Communication | Yes |
| 13 | TWAI | 0x6002B000 | Communication | Yes |
| 14 | I2S | 0x6002D000 | Communication | Yes |
| 15 | AES | 0x6003A000 | Crypto | Yes |
| 16 | SHA | 0x6003B000 | Crypto | Yes |
| 17 | RSA | 0x6003C000 | Crypto | Yes |
| 18 | Digital Signature | 0x6003D000 | Crypto | Yes |
| 19 | HMAC | 0x6003E000 | Crypto | Yes |
| 20 | APB SAR ADC | 0x60040000 | Analog | Yes |
| 21 | USB Serial/JTAG | 0x60043000 | Debug | Yes |
| 22 | XTS-AES | 0x600CC000 | Crypto | Yes |
| 23 | World Controller | 0x600D0000 | Security | Yes |

## HW vs Renode Comparison

Comparison covers 10 peripherals with paired baselines (90 test pairs,
81 after excluding 9 expected-diff tests for eFuse OTP data and RNG):

| Category | Count | Description |
|---|---|---|
| Match | 49 | Register values identical between HW and Renode |
| Expected diff (excluded) | 9 | eFuse OTP data, RNG sequences |
| Post-boot firmware state | ~20 | Registers set by firmware during boot |
| Timing-dependent | ~10 | Counter values (excluded from comparison) |
| Infrastructure | ~7 | Output length differences |
| **Total compared** | **81** | |

## Gaps

### Missing test firmware (5 C# peripherals)

These peripherals have C# implementations but no test firmware to validate
register accuracy against real hardware:

- **Sensitive (PMS)** — 913 lines, 94 registers. Needs tests for boundary
  config, lock bits, DMA PMS constraints.
- **SPI MEM (Flash)** — 280 lines. Needs tests for RDID, status register,
  user command interface.
- **GDMA** — 182 lines. Needs tests for channel config, interrupt registers,
  peripheral selection.
- **Assist Debug** — 175 lines. Needs tests for SP monitoring, recording
  enable, interrupt registers.
- **MMU Table** — 57 lines. Needs tests for entry read/write, invalid flag
  behavior.

### Missing baselines (IO MUX + 27 stubs)

- **IO MUX** — 3 test firmware exist but zero baselines (hardware or Renode).
  Need to capture on both.
- **23 Python placeholders** — all have hardware baselines but zero Renode
  baselines. Capturing Renode baselines would establish the comparison
  baseline for future C# implementations.

### Missing Robot tests (9 C# peripherals)

These peripherals have no automated Robot Framework test for regression
prevention:

- RTC Controller (10 test firmware, 10 baselines — just needs Robot file)
- UART0 (10 test firmware, 10 baselines — just needs Robot file)
- ExtMem Cache (10 test firmware, 10 baselines — just needs Robot file)
- IO MUX (3 test firmware, 0 baselines)
- SPI MEM Flash (0 test firmware)
- Sensitive PMS (0 test firmware)
- GDMA (0 test firmware)
- Assist Debug (0 test firmware)
- MMU Table (0 test firmware)

### Known register mismatches (~32 across 10 peripherals)

The 49/81 match rate leaves ~32 register value differences. Major categories:

- **SYSTIMER** — CONF_REG bit 25 (ROM sets stall-enable), INT_RAW timing
- **Timer Group** — RTC calibration register values differ from firmware config
- **GPIO** — GPIO_IN_REG reflects real board pin states on HW
- **ExtMem** — SYNC_CTRL bit 0 firmware-interaction difference
- **UART** — CONF0/CONF1 defaults, FSM_STATUS, STATUS (HW UART active)

### ESP-IDF headers with no peripheral coverage (3)

- `apb_ctrl_reg.h` — APB controller (may overlap with SYSCON)
- `gpio_sd_reg.h` — GPIO sigma-delta modulator
- `regi2c_dig_reg.h` / `wdev_reg.h` — internal analog/wireless registers

## Boot Sequence

The CPU starts at the ROM reset vector (`_init` at 0x40001E90). The ROM
CRT0 runs fully, initializing all DRAM data structures from IRAM sources.
At the jump to ROM `main`, execution redirects to the firmware entry point
(`call_start_cpu0`) since we load firmware directly rather than booting
from flash.

All ROM function tables use original unmodified values.

## CI

GitHub Actions runs hello_world Robot tests on all branches and PRs.

```
uv run tools/run_renode_tests.py             # Run 12 Robot tests
uv run tools/build_all_firmware.py           # Build all 120 firmware
uv run tools/capture_renode_baselines.py     # Capture from Renode
uv run tools/compare_all_baselines.py [-v]   # Diff HW vs Renode
```
