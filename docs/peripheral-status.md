# ESP32-C3 Peripheral Emulation Status

> Last updated: 2026-04-06

## Summary

| Metric | Value |
|---|---|
| Boot progress | **Full** — ROM init through FreeRTOS `app_main()` |
| C# peripherals implemented | 16 |
| C# peripherals reviewed + fixed | 12 (18+ bugs found and fixed) |
| Per-feature test firmware | 120 (93 functional + 27 stub reset-value) |
| Hardware baselines captured | 90 (from real ESP32-C3 on rpi4-esp) |
| Renode baselines captured | 90 |
| HW vs Renode comparison | 49/81 match (60%, excluding expected diffs) |
| Robot Framework test suites | 8 (12 test cases, all passing) |
| GitHub Actions CI | Passing (hello_world suite on all branches + PRs) |
| Python stubs remaining | 23 |
| Boot | Hardware-like — CPU starts at ROM `_init`, no patches |

## C# Peripherals (16 Implemented)

| # | Peripheral | Address | Size | Registers | Test FW | HW Baseline | Review |
|---|---|---|---|---|---|---|---|
| 1 | Interrupt Matrix | 0x600C2000 | 0x800 | 104 | 8 tests | 8 | Reviewed + fixed; CLIC delivery |
| 2 | eFuse | 0x60008800 | 0x200 | 115 | 9 tests | 9 | Reviewed + fixed |
| 3 | RTC Controller | 0x60008000 | 0x800 | 74 | 10 tests | 10 | Reviewed + fixed |
| 4 | SYSTIMER | 0x60023000 | 0x1000 | 30 | 9 tests | 9 | Reviewed + fixed |
| 5 | Timer Group | 0x6001F000 | 0x100 | 26 | 10 tests | 10 | Reviewed + fixed; alarm firing |
| 6 | SYSTEM (DPORT) | 0x600C0000 | 0x1000 | 40 | 8 tests | 8 | Reviewed + fixed |
| 7 | RNG / SYSCON | 0x60026000 | 0x1000 | 39 | 6 tests | 6 | Reviewed + fixed |
| 8 | GPIO | 0x60004000 | 0x1000 | 199 | 10 tests | 10 | Reviewed + fixed; FUNC_IN_SEL default |
| 9 | ExtMem (Cache) | 0x600C4000 | 0x1000 | 66 | 10 tests | 10 | Reviewed + fixed; FREEZE_DONE |
| 10 | UART0 | 0x60000000 | 0x100 | 33 | 10 tests | 10 | Reviewed + fixed |
| 11 | IO MUX | 0x60009000 | 0x1000 | 23 | 3 tests | N/A | New |
| 12 | SPI MEM (Flash) | 0x60002000 | 0x1000 | ~30 | N/A | N/A | RDID + status |
| 13 | Sensitive (PMS) | 0x600C1000 | 0x1000 | 94 | N/A | N/A | Full register bank |
| 14 | GDMA | 0x6003F000 | 0x1000 | 60+ | N/A | N/A | 3-channel register storage |
| 15 | Assist Debug | 0x600CE000 | 0x1000 | 40 | N/A | N/A | SP monitor, PC recording |
| 16 | MMU Table | 0x600C5000 | 0x1000 | 128 | N/A | N/A | Page table (128 entries) |

## Hardware vs Renode Comparison (Phase 3)

| Category | Count | Description |
|---|---|---|
| Match | 49 | Register values identical between HW and Renode |
| Expected diff (excluded) | 9 | eFuse OTP data, RNG sequences |
| Post-boot firmware state | ~20 | Registers configured differently by firmware |
| Timing-dependent | ~10 | Counter values (excluded from value comparison) |
| Infrastructure | ~7 | Output length differences |
| **Total compared** | **81** | (90 total - 9 excluded) |

## Python Stubs (23 Remaining)

Each has a reset-value test firmware that reads 16 registers. These establish
ground truth for future C# implementations.

| # | Peripheral | Address | Category |
|---|---|---|---|
| 1 | SPI0 | 0x60003000 | Storage |
| 2 | FE2 (RF Frontend 2) | 0x60005000 | Wireless |
| 3 | FE (RF Frontend) | 0x60006000 | Wireless |
| 4 | RTC I2C | 0x6000E000 | Analog |
| 5 | NRX | 0x6001C000 | Wireless |
| 6 | BB (Baseband) | 0x6001E000 | Wireless |
| 7 | UART1 | 0x60010000 | Communication |
| 8 | I2C | 0x60013000 | Communication |
| 9 | UHCI0 | 0x60014000 | Communication |
| 10 | RMT | 0x60016000 | Peripheral |
| 11 | LEDC | 0x60019000 | Peripheral |
| 12 | SPI2 (GP-SPI) | 0x60024000 | Communication |
| 13 | TWAI | 0x6002B000 | Communication |
| 14 | I2S | 0x6002D000 | Communication |
| 15 | AES | 0x6003A000 | Crypto |
| 16 | SHA | 0x6003B000 | Crypto |
| 17 | RSA | 0x6003C000 | Crypto |
| 18 | Digital Signature | 0x6003D000 | Crypto |
| 19 | HMAC | 0x6003E000 | Crypto |
| 20 | APB SAR ADC | 0x60040000 | Analog |
| 21 | USB Serial/JTAG | 0x60043000 | Debug |
| 22 | XTS-AES | 0x600CC000 | Crypto |
| 23 | World Controller | 0x600D0000 | Security |

## Boot Sequence

The CPU starts at the ROM reset vector (`_init` at 0x40001E90). The ROM
CRT0 runs fully, initializing all DRAM data structures from IRAM sources.
At the jump to ROM `main`, execution redirects to the firmware entry point
(`call_start_cpu0`) since we load firmware directly rather than booting
from flash.

All ROM function tables use original unmodified values:
- `rom_phyFuns` -- ROM PHY functions work with Python stubs for FE/FE2/NRX/BB
- `rom_cache_internal_table_ptr` -- ROM cache functions work with ExtMem peripheral
- `rom_spiflash_legacy_data` -- original ROM spiflash data works with SPI MEM

## CI Pipeline

GitHub Actions runs hello_world Robot tests on all branches and PRs.

Local tools:

```
uv run tools/run_renode_tests.py             # Run 12 Robot tests
uv run tools/build_all_firmware.py           # Build all 120 firmware
uv run tools/capture_renode_baselines.py     # Capture from Renode
uv run tools/compare_all_baselines.py [-v]   # Diff HW vs Renode
```
