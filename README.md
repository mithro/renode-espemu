# renode-espemu

Custom [Renode](https://renode.io/) peripheral models for emulating the
[ESP32-C3](https://www.espressif.com/en/products/socs/esp32-c3) RISC-V
microcontroller. Achieves full FreeRTOS boot through "Hello world!" with
register-level accuracy validated against real hardware.

## Status

| Metric | Value |
|---|---|
| Boot progress | **Full** -- ROM init through FreeRTOS `app_main()` |
| C# peripherals | 13 of 40 |
| Per-feature test firmware | 120 (93 functional + 27 stub reset-value) |
| HW vs Renode register match | 49/81 (60%, excluding expected diffs) |
| Robot Framework tests | All passing |
| Boot workarounds remaining | 7 |

### Boot workarounds

These CPU hooks patch around gaps where Renode doesn't yet match real
hardware. Each is a specific function or code path that gets skipped or
fixed up at runtime:

| Workaround | Description |
|---|---|
| `init_flash` | SPI flash init returns ESP_OK immediately |
| `delay` | Delay functions skipped (cycle counter doesn't increment) |
| `memprot` | Memory protection setup skipped |
| `brownout` | Brownout ISR skipped |
| `MIE` | Machine interrupt enable set manually |
| `kick` | SYSTIMER and interrupt matrix kicked to start tick |
| `mcause` | mcause register fixed at interrupt handler entry |

## Repository structure

```
renode-espemu/
+-- peripherals/          # Peripheral implementations
|   +-- efuse/            # C# peripheral + test firmware + baselines
|   +-- extmem/           # ...
|   +-- gpio/
|   +-- interrupt-matrix/
|   +-- iomux/
|   +-- rng/
|   +-- rtc/
|   +-- sensitive/
|   +-- spi-flash/
|   +-- stubs/            # 27 Python stub peripherals
|   +-- system/
|   +-- systimer/
|   +-- timer-group/
|   +-- uart/
+-- platforms/
|   +-- cpus/esp32c3.repl  # Renode platform description
|   +-- rom_data_segment.bin
|   +-- rom_func_stubs.bin
+-- hello_world/           # Full boot demo (ELF, Robot tests)
+-- tests/                 # Robot Framework test infrastructure
+-- scripts/               # CI, build, capture, and comparison tools
+-- tools/                 # Additional Python tooling
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

## C# peripherals (13 implemented)

| # | Peripheral | Address | Registers | Tests | Description |
|---|---|---|---|---|---|
| 1 | Interrupt Matrix | `0x600C2000` | 104 | 8 | Routes 62 sources to 32 CPU interrupt lines |
| 2 | eFuse | `0x60008800` | 115 | 9 | OTP storage: MAC, chip revision, keys |
| 3 | RTC Controller | `0x60008000` | 74 | 10 | Reset cause, RTC timer, brownout, STORE regs |
| 4 | SYSTIMER | `0x60023000` | 30 | 9 | 16 MHz system timer, 2 counters, 3 alarms |
| 5 | Timer Group 0/1 | `0x6001F000` | 26 | 10 | General-purpose timer with divider and alarm |
| 6 | SYSTEM (DPORT) | `0x600C0000` | 40 | 8 | Clock gating, reset, FROM_CPU interrupts |
| 7 | RNG / SYSCON | `0x60026000` | 39 | 6 | PRNG, WiFi/BT clock control |
| 8 | GPIO | `0x60004000` | 199 | 10 | 26 pins, output/input, interrupts, muxing |
| 9 | ExtMem (Cache) | `0x600C4000` | 66 | 10 | ICache control, MMU, preload/sync |
| 10 | UART0 | `0x60000000` | 33 | 10 | Serial TX/RX with interrupt support |
| 11 | IO MUX | `0x60009000` | 23 | 3 | Pin function select, pull-up/down, drive |
| 12 | SPI MEM | `0x60002000` | ~30 | -- | Flash controller (RDID, status, data buffer) |
| 13 | Sensitive (PMS) | `0x600C1000` | 94 | -- | Permission management / memory protection |

## Python stub peripherals (27 remaining)

Minimal placeholders that return reset values and log accesses. Each has a
reset-value test firmware that reads 16 registers to establish ground truth
for future C# implementations.

| Category | Peripherals |
|---|---|
| Communication | UART1, I2C, SPI2, UHCI0, TWAI, I2S |
| Wireless | FE, FE2, NRX, BB |
| Crypto | AES, SHA, RSA, Digital Signature, HMAC, XTS-AES |
| DMA | GDMA |
| Analog | RTC I2C, APB SAR ADC |
| Storage | SPI0 |
| Peripheral | RMT, LEDC |
| Memory | MMU Table |
| Security | World Controller |
| Debug | USB Serial/JTAG, Assist Debug |

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

- [Renode](https://renode.io/) (latest)
- [ESP-IDF](https://docs.espressif.com/projects/esp-idf/en/stable/esp32c3/get-started/)
  v5.4.1 at `~/esp/esp-idf`
- [uv](https://docs.astral.sh/uv/) for Python tooling
- ESP32-C3 ROM ELF at `~/esp/esp-rom-elfs/esp32c3_rev3_rom.elf`

## Quick start

Run the hello world demo in Renode:

```bash
renode hello_world/setup.resc
# In Renode monitor:
start
```

Build all test firmware:

```bash
uv run scripts/build_all_firmware.py
```

Run Robot Framework tests:

```bash
uv run scripts/run_renode_tests.py
```

Compare hardware vs Renode baselines:

```bash
uv run scripts/compare_all_baselines.py -v
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
