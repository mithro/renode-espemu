# ESP32-C3 Peripheral Emulation Status

> Last updated: 2026-03-29

## Summary

| Metric | Value |
|---|---|
| Boot progress | **5/5** — "Hello world!" prints via UART |
| C# peripherals implemented | 10 |
| C# peripherals reviewed + fixed | 10 (18 bugs found and fixed) |
| Robot Framework tests | 12 passing |
| Test firmware built | 8 (7 peripheral + hello_world) |
| Hardware baselines captured | 7 (from real ESP32-C3 on rpi4-esp) |
| Python stubs remaining | 29 (non-boot-critical) |
| Boot workarounds | 5 (see below) |

## C# Peripherals (Implemented)

| # | Peripheral | Address | C# File | Lines | Test FW | Robot Test | HW Baseline | Review |
|---|---|---|---|---|---|---|---|---|
| 1 | Interrupt Matrix | 0x600C2000 | `ESP32C3_InterruptMatrix.cs` | 305 | Built | Pass | Yes | Fixed (1 issue) |
| 2 | eFuse | 0x60008800 | `ESP32C3_eFuse.cs` | 202 | Built | 3 pass | Yes | Fixed (1 issue) |
| 3 | RTC Controller | 0x60008000 | `ESP32C3_RTC.cs` | 297 | N/A | N/A | N/A | Fixed (2 issues) |
| 4 | SYSTIMER | 0x60023000 | `ESP32C3_SysTimer.cs` | 518 | Built | Pass | Yes | Fixed (3 issues) |
| 5 | Timer Group | 0x6001F000 | `ESP32C3_TimerGroup.cs` | 367 | Built | Pass | Yes | Fixed (3 issues) |
| 6 | SYSTEM (DPORT) | 0x600C0000 | `ESP32C3_System.cs` | 223 | Built | Pass | Yes | Fixed (2 issues) |
| 7 | RNG / SYSCON | 0x60026000 | `ESP32C3_RNG.cs` | 156 | Built | Pass | Yes | Fixed (2 issues) |
| 8 | GPIO | 0x60004000 | `ESP32C3_GPIO.cs` | 352 | Built | Pass | Yes | Fixed (1 issue) |
| 9 | ExtMem (Cache) | 0x600C4000 | `ESP32C3_ExtMem.cs` | 496 | N/A | N/A | N/A | Fixed (1 issue) |
| 10 | UART0 | 0x60000000 | `ESP32C3_UART.cs` | 309 | N/A | 3 pass (hello_world) | N/A | Fixed (2 issues) |

**Total C# code: ~3,225 lines across 10 peripherals.**

### Key features per peripheral

- **Interrupt Matrix**: Routes 64 sources to 32 CPU lines. MEIP delivery via GPIO 11. PendingMcause for Python hook mcause override.
- **eFuse**: Read-only OTP storage. MAC address (7C:DF:A1:52:45:4E), chip revision v0.4. All BLK0-BLK10 read data registers.
- **RTC Controller**: RESET_STATE (POWERON), RTC time with latch semantics, STORE0-7 (XTAL_FREQ=40MHz in STORE4), CLK_CONF, WDT stubs, brownout, SWD.
- **SYSTIMER**: 16MHz counter, 3 alarm comparators (one-shot + periodic). GPIO outputs wired to interrupt matrix sources 37/38/39. LimitTimer-driven.
- **Timer Group**: 54-bit counter with latch reads, configurable divider, alarm. RTC calibration (RDY=1). WDT stubs. Instantiable for TIMG0 + TIMG1.
- **SYSTEM**: CPU_INTR_FROM_CPU_0-3 with GPIO outputs to interrupt matrix (pulse semantics). Clock enable/reset registers. SYSCLK_CONF.
- **RNG / SYSCON**: xorshift32 PRNG at offset 0xB0. 30+ SYSCON registers (WIFI_CLK_EN default 0xFFFFFFFF, memory power, clock gate).
- **GPIO**: 22 pins with W1TS/W1TC, per-pin config (PIN0-PIN21), 128 input mux, 22 output mux. Loopback IN reads.
- **ExtMem (Cache)**: 65 registers. ICACHE_ENABLE=1 default. All sync/preload/autoload/freeze done bits return true.
- **UART0**: Extends UARTBase for Terminal Tester compatibility. TX via TransmitCharacter, RX via TryGetCharacter. STATUS with FIFO counts.

## Watchdog & Clock Control

These are not standalone peripherals on ESP32-C3. Their registers are embedded in other peripherals:

| Feature | Implemented In | Registers |
|---|---|---|
| RTC WDT | RTC Controller | WDTCONFIG0-4 (0x90-0xA0), WDTFEED (0xA4), WDTWPROTECT (0xA8) |
| Timer WDT | Timer Group | WDT registers (stubbed, accept writes silently) |
| Super WDT | RTC Controller | SWD_CONF (0xAC), SWD_WPROTECT (0xB0) |
| Clock Config | RTC Controller | CLK_CONF (0x70), STORE4/XTAL_FREQ (0xB8) |
| CPU Clock | SYSTEM | SYSCLK_CONF (0x58), PERIP_CLK_EN0/1, PERIP_RST_EN0/1 |
| WiFi Clock | SYSCON (RNG) | WIFI_CLK_EN (0x14), WIFI_RST_EN (0x18) |

## Python Stubs (Not Yet Implemented)

These peripherals return 0 on all reads and absorb writes. They don't affect boot but would be needed for specific firmware features.

| # | Peripheral | Address | Category | Boot Impact |
|---|---|---|---|---|
| 1 | SPI1 (Flash Controller) | 0x60002000 | Storage | init_flash skipped |
| 2 | SPI0 | 0x60003000 | Storage | None |
| 3 | FE2 (RF Frontend 2) | 0x60005000 | Wireless | None |
| 4 | FE (RF Frontend) | 0x60006000 | Wireless | None |
| 5 | RTC I2C | 0x6000E000 | Analog | None |
| 6 | NRX | 0x6001C000 | Wireless | None |
| 7 | BB (Baseband) | 0x6001E000 | Wireless | None |
| 8 | IO MUX | 0x60009000 | Pin Config | None (accepts writes) |
| 9 | UART1 | 0x60010000 | Communication | None |
| 10 | I2C | 0x60013000 | Communication | None |
| 11 | UHCI0 | 0x60014000 | Communication | None |
| 12 | RMT | 0x60016000 | Peripheral | None |
| 13 | LEDC | 0x60019000 | Peripheral | None |
| 14 | SPI2 (GP-SPI) | 0x60024000 | Communication | None |
| 15 | TWAI | 0x6002B000 | Communication | None |
| 16 | I2S | 0x6002D000 | Communication | None |
| 17 | AES | 0x6003A000 | Crypto | None |
| 18 | SHA | 0x6003B000 | Crypto | None |
| 19 | RSA | 0x6003C000 | Crypto | None |
| 20 | Digital Signature | 0x6003D000 | Crypto | None |
| 21 | HMAC | 0x6003E000 | Crypto | None |
| 22 | GDMA | 0x6003F000 | DMA | None |
| 23 | APB SAR ADC | 0x60040000 | Analog | None |
| 24 | USB Serial/JTAG | 0x60043000 | Debug | None |
| 25 | Sensitive | 0x600C1000 | Security | None |
| 26 | MMU Table | 0x600C5000 | Memory | None |
| 27 | XTS-AES | 0x600CC000 | Crypto | None |
| 28 | Assist Debug | 0x600CE000 | Debug | None |
| 29 | World Controller | 0x600D0000 | Security | None |

## Boot Workarounds

These workarounds in the boot script compensate for missing functionality:

| # | Workaround | Reason | Removal Requires |
|---|---|---|---|
| 1 | Patch ROM function tables after BSS clear | rom_phyFuns and rom_cache_internal_table_ptr are uninitialised | PHY stub library or proper ROM function table init |
| 2 | Patch init_flash to return ESP_OK | SPI Flash controller not implemented | Full SPI Flash peripheral (SPI0/SPI1) |
| 3 | Skip delay functions | Cycle counter CSR (0x802) doesn't increment | Proper RISC-V cycle counter or PMAADDR CSR emulation |
| 4 | Skip memprot section | PMS (Permission Management System) peripheral not implemented | Sensitive/PMS peripheral registers |
| 5 | Skip brownout ISR | Brownout interrupt fires spuriously | Proper brownout detection disable or analog model |
| 6 | Force MIE/MSTATUS enable | ESP32-C3 uses CLIC-style interrupts, not standard RISC-V mie/mip | Proper CLIC interrupt controller support in Renode |
| 7 | Single manual interrupt kick | SYSTIMER auto-fires after first kick, but initial delivery needs manual trigger | Fix SYSTIMER auto-fire timing during init |
| 8 | mcause override hook | Standard RISC-V MEIP sets mcause=0x8000000B, firmware expects CPU line number | Renode support for custom mcause values per interrupt source |

## Robot Framework Tests

| # | Test Suite | Tests | Status | Duration |
|---|---|---|---|---|
| 1 | hello_world/test.robot | 3 | All pass | ~10s |
| 2 | efuse/test.robot | 3 | All pass | ~5s |
| 3 | rng/test.robot | 1 | Pass | ~7s |
| 4 | gpio/test.robot | 1 | Pass | ~1.4s |
| 5 | system/test.robot | 1 | Pass | ~1.4s |
| 6 | systimer/test.robot | 1 | Pass | ~1.4s |
| 7 | timer-group/test.robot | 1 | Pass | ~1.4s |
| 8 | interrupt-matrix/test.robot | 1 | Pass | ~6.5s |
| | **Total** | **12** | **All pass** | **~35s** |

## CI Pipeline

```
python3 tools/ci.py                # Build all firmware + run all Renode tests
python3 tools/ci.py --hardware     # Also flash to rpi4-esp + capture baselines
python3 tools/ci.py --skip-build   # Tests only
python3 tools/build_all_firmware.py # Build 8 firmware images
python3 tools/run_renode_tests.py   # Run 12 Robot tests
python3 tools/capture_hardware_baseline.py <peripheral>  # Flash + capture UART
python3 tools/compare_output.py --hardware hw.log --renode renode.log  # Diff
```

## Hardware vs Renode Output Comparison

Renode UART output for hello_world:
```
I (0) main_task: Started on CPU0
I (0) main_task: Calling app_main()
Hello world!
This is esp32c3 chip with 1 CPU core(s), WiFi/BLE, silicon revision v0.4, ...
```

Hardware UART output includes full boot chain (ROM → bootloader → app) which Renode skips because we load the app directly. Key differences:
- Hardware: 70+ lines of boot messages; Renode: 4 lines (app only)
- Hardware: timestamps like "I (24)"; Renode: "I (0)" (no time advance during boot)
- Hardware: full flash info; Renode: "Get flash size failed" (init_flash skipped)
- Both: silicon revision v0.4, "Hello world!" printed correctly

## File Layout

```
renode-espemu/
├── platforms/cpus/esp32c3.repl        # Platform definition (10 C# + 29 stubs)
├── peripherals/
│   ├── efuse/                         # eFuse controller
│   │   ├── ESP32C3_eFuse.cs           # C# peripheral (202 lines)
│   │   ├── execution.md               # Execution tracker
│   │   ├── firmware/                  # ESP-IDF test app (built)
│   │   ├── baselines/hardware.log     # Real ESP32-C3 output (92 lines)
│   │   └── test.robot                 # 3 Robot tests
│   ├── extmem/                        # Cache/MMU controller
│   ├── gpio/                          # GPIO controller
│   ├── interrupt-matrix/              # Interrupt matrix
│   ├── rng/                           # RNG / SYSCON
│   ├── rtc/                           # RTC controller
│   ├── system/                        # SYSTEM registers
│   ├── systimer/                      # System timer
│   ├── timer-group/                   # Timer groups (TIMG0/TIMG1)
│   └── uart/                          # UART0 (UARTBase)
├── hello_world/
│   ├── setup.resc                     # Robot test setup script
│   ├── test.robot                     # 3 Robot tests
│   └── firmware/                      # Pre-built hello_world
├── tests/
│   ├── esp32c3_setup.resc             # Common setup for peripheral tests
│   └── esp32c3_keywords.robot         # Common Robot keywords
├── tools/
│   ├── ci.py                          # Master CI pipeline
│   ├── build_all_firmware.py          # Build all 8 firmware
│   ├── run_renode_tests.py            # Run all 12 Robot tests
│   ├── capture_hardware_baseline.py   # Flash + capture from rpi4-esp
│   ├── serial_capture.py              # pyserial UART capture helper
│   └── compare_output.py              # Diff hardware vs Renode output
└── tmp/run_boot_test.resc             # Standalone boot test script
```
