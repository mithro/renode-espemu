# renode-espemu

Custom [Renode](https://renode.io/) peripheral models for emulating the
[ESP32-C3](https://www.espressif.com/en/products/socs/esp32-c3) RISC-V
microcontroller. Achieves full FreeRTOS boot through "Hello world!" with
register-level accuracy validated against real hardware.

## Status

| Metric | Value |
|---|---|
| Boot progress | **Full** -- ROM init through FreeRTOS `app_main()` |
| C# peripherals | 16 of 39 |
| Per-feature test firmware | 120 (93 functional + 27 stub reset-value) |
| HW vs Renode register match | 49/81 (60%, excluding expected diffs) |
| Robot Framework tests | 12 tests, all passing |
| GitHub Actions CI | Passing (hello_world suite on all branches + PRs) |
| Boot workarounds | **0** -- CPU starts at ROM `_init`, no patches |

### Boot sequence

The CPU starts at the ROM reset vector (`_init` at 0x40001E90), matching
real hardware. The ROM CRT0 runs fully:

1. **HW init** -- CSR setup, mtvec, interrupt matrix, stack pointer
2. **Data copy** (`unpackloop`) -- copies ROM data from IRAM to DRAM
3. **BSS clear** (`clearloop`) -- zeroes ROM BSS sections
4. **ROM main** -- redirected to firmware entry (we load firmware directly
   rather than booting from flash)

No `WriteDoubleWord`, no `cpu AddHook` for ROM data patching. All ROM
function tables (PHY, cache, spiflash) use their original unmodified values.

### Interrupt delivery (CLIC)

The ESP32-C3 uses CLIC-style interrupt delivery where `mcause` encodes the
specific CPU interrupt line number. Renode's built-in
`CoreLocalInterruptController` provides this automatically:

```
Peripheral -> GPIO -> IntMatrix (source mapping) -> CLIC -> CPU
                                                      |
                                             mcause = 0x80000000 | line
```

The platform uses `PrivilegedArchitecture.PrivUnratified` with CLIC
configured for SHV=0 (non-vectored) mode and a dispatch hook at the
firmware's vector table entry point.

## Repository structure

```
renode-espemu/
+-- .github/workflows/    # GitHub Actions CI
+-- peripherals/          # Peripheral implementations
|   +-- assist-debug/     # C# peripheral
|   +-- efuse/            # C# peripheral + test firmware + baselines
|   +-- extmem/           # C# peripheral + test firmware + baselines
|   +-- gdma/             # C# peripheral
|   +-- gpio/             # C# peripheral + test firmware + baselines
|   +-- interrupt-matrix/ # C# peripheral + test firmware + baselines
|   +-- iomux/            # C# peripheral
|   +-- mmu/              # C# peripheral
|   +-- rng/              # C# peripheral + test firmware + baselines
|   +-- rtc/              # C# peripheral + test firmware + baselines
|   +-- sensitive/        # C# peripheral
|   +-- spi-flash/        # C# peripheral
|   +-- stubs/            # Python stub peripherals
|   +-- system/           # C# peripheral + test firmware + baselines
|   +-- systimer/         # C# peripheral + test firmware + baselines
|   +-- timer-group/      # C# peripheral + test firmware + baselines
|   +-- uart/             # C# peripheral + test firmware + baselines
+-- platforms/
|   +-- cpus/esp32c3.repl  # Renode platform description
|   +-- rom_data_segment.bin  # ROM data bus segment (0x3FF00000)
|   +-- rom_iram_data.bin     # ROM CRT0 data-copy sources (0x40059590)
+-- hello_world/           # Full boot demo (ELF, Robot tests)
+-- tests/                 # Robot Framework test infrastructure
+-- tools/                 # Python tooling (build, capture, compare, CI)
+-- docs/                  # Architecture docs and peripheral references
```

### Peripheral organisation

Each C# peripheral directory follows the same layout:

```
peripherals/<name>/
+-- ESP32C3_<Name>.cs          # Renode C# peripheral model
+-- firmware/
|   +-- test_<feature>/        # One test per logical feature
|   |   +-- main/test_<feature>.c
|   |   +-- CMakeLists.txt
|   |   +-- sdkconfig.defaults
|   +-- build/                 # Compiled ELF + BIN (gitignored)
+-- baselines/
    +-- hardware/<test>.log    # Captured from real ESP32-C3
    +-- renode/<test>.log      # Captured from Renode simulation
```

## C# peripherals (16 implemented)

| # | Peripheral | Address | Registers | Tests | Description |
|---|---|---|---|---|---|
| 1 | Interrupt Matrix | `0x600C2000` | 104 | 8 | Routes 62 sources to 32 CPU lines via CLIC |
| 2 | eFuse | `0x60008800` | 115 | 9 | OTP storage: MAC, chip revision, keys |
| 3 | RTC Controller | `0x60008000` | 74 | 10 | Reset cause, RTC timer, brownout, STORE regs |
| 4 | SYSTIMER | `0x60023000` | 30 | 9 | 16 MHz system timer, 2 counters, 3 alarms |
| 5 | Timer Group 0/1 | `0x6001F000` | 26 | 10 | General-purpose timer with divider and alarm |
| 6 | SYSTEM (DPORT) | `0x600C0000` | 40 | 8 | Clock gating, reset, FROM_CPU interrupts |
| 7 | RNG / SYSCON | `0x60026000` | 39 | 6 | PRNG, WiFi/BT clock control |
| 8 | GPIO | `0x60004000` | 199 | 10 | 26 pins, output/input, interrupts, muxing |
| 9 | ExtMem (Cache) | `0x600C4000` | 66 | 10 | ICache control, MMU, preload/sync, freeze |
| 10 | UART0 | `0x60000000` | 33 | 10 | Serial TX/RX with interrupt support |
| 11 | IO MUX | `0x60009000` | 23 | 3 | Pin function select, pull-up/down, drive |
| 12 | SPI MEM | `0x60002000` | ~30 | -- | Flash controller (RDID, status, data buffer) |
| 13 | Sensitive (PMS) | `0x600C1000` | 94 | -- | Permission management / memory protection |
| 14 | GDMA | `0x6003F000` | 60+ | -- | 3-channel DMA controller (register storage) |
| 15 | Assist Debug | `0x600CE000` | 40 | -- | SP monitoring, PC recording, exception logging |
| 16 | MMU Table | `0x600C5000` | 128 | -- | ICache/DCache page table (128 entries) |

## Python stub peripherals (23 remaining)

Minimal placeholders that return reset values and log accesses. Each has a
reset-value test firmware that reads 16 registers to establish ground truth
for future C# implementations.

| Category | Peripherals |
|---|---|
| Communication | UART1, I2C, SPI2, UHCI0, TWAI, I2S |
| Wireless | FE, FE2, NRX, BB |
| Crypto | AES, SHA, RSA, Digital Signature, HMAC, XTS-AES |
| Analog | RTC I2C, APB SAR ADC |
| Storage | SPI0 |
| Peripheral | RMT, LEDC |
| Security | World Controller |
| Debug | USB Serial/JTAG |

## Validation methodology

Peripheral accuracy is validated by comparing register reads between Renode
and a real ESP32-C3 chip:

1. **Test firmware** -- minimal C programs that read specific registers and
   print values in a structured format (`[TAG] REG_READ addr=0xXXX val=0xNNN`)
2. **Hardware baselines** -- captured from a real ESP32-C3 on an rpi4-esp
   board via UART
3. **Renode baselines** -- captured by running the same firmware in Renode
4. **Comparison** -- automated diffing identifies mismatches, with expected
   differences (eFuse OTP data, RNG sequences) excluded

## Prerequisites

- [Renode](https://renode.io/) (v1.16.1+)
- [ESP-IDF](https://docs.espressif.com/projects/esp-idf/en/stable/esp32c3/get-started/)
  v5.4.1 at `~/esp/esp-idf` (for building test firmware)
- [uv](https://docs.astral.sh/uv/) for Python tooling
- ESP32-C3 ROM ELF at `~/esp/esp-rom-elfs/esp32c3_rev3_rom.elf`
  (from [espressif/esp-rom-elfs](https://github.com/espressif/esp-rom-elfs/releases))

## Quick start

Run the hello world demo in Renode:

```bash
renode hello_world/boot.resc
```

Run Robot Framework tests:

```bash
uv run tools/run_renode_tests.py
```

Build all test firmware (requires ESP-IDF):

```bash
uv run tools/build_all_firmware.py
```

Compare hardware vs Renode baselines:

```bash
uv run tools/compare_all_baselines.py -v
```

## Documentation

See [`docs/`](docs/) for detailed documentation:

- [ESP hardware reference](docs/01-esp-hardware-reference.md) -- chip families, specs, peripherals
- [Emulation platform status](docs/02-emulation-platform-status.md) -- Renode/QEMU/Wokwi support matrix
- [Wireless hardware](docs/03-wireless-hardware-documentation.md) -- WiFi/BLE reverse engineering
- [Development methodology](docs/04-development-methodology.md) -- JTAG debugging, execution tracing
- [Gap analysis and roadmap](docs/05-gap-analysis-and-roadmap.md) -- missing peripherals, next steps
- [Test station hardware](docs/06-test-station-hardware.md) -- rpi4-esp board inventory
- [Peripheral status](docs/peripheral-status.md) -- detailed per-peripheral implementation state
- [Peripheral test plan](docs/peripheral-test-plan.md) -- test conventions and per-feature breakdown
- [RISC-V customization](docs/renode-riscv-customization.md) -- CSR handlers, CLIC, ROM CRT0 analysis
