# Emulation Platform Status: Renode, QEMU, and Wokwi

Current state of ESP32 emulation support across open-source platforms.

> **See also:**
> - [ESP Hardware Reference](01-esp-hardware-reference.md) — chip specs and peripherals being emulated
> - [Wireless Hardware Documentation](03-wireless-hardware-documentation.md) — WiFi/BLE/802.15.4 register-level details
> - [Gap Analysis and Roadmap](05-gap-analysis-and-roadmap.md) — what's missing and the plan to fix it
> - [Document Index](README.md)

---

## 5. Renode Emulation Support Status

### Overview

As of early 2026, Renode's ESP32 support is **extremely minimal**. The Renode team confirmed in November 2024 (GitHub issue [renode/renode#704](https://github.com/renode/renode/issues/704)):

> "The note you mention concerns the **core only**, so not the SoC and peripherals. We have recently added the ESP32_UART model, but it's **not the whole platform yet**."

### CPU Architecture Support

#### Xtensa (ESP32, ESP32-S2, ESP32-S3)

- **Status: Early / Incomplete**
- Xtensa ISA support was added to Renode in **January 2022**, in collaboration with Google and the Sound Open Firmware (SOF) project ([Antmicro blog post](https://antmicro.com/blog/2022/01/xtensa-isa-in-renode-for-sof-project/))
- The Xtensa implementation was designed for **Intel audio DSP cores (HiFi2/3/4)**, not specifically for ESP32
- Renode source files:
  - `src/Emulator/Cores/Xtensa/Xtensa.cs` - Main Xtensa CPU class
  - `src/Emulator/Cores/Xtensa/XtensaRegisters.cs` - Register definitions
  - `src/Emulator/Cores/renode/arch/xtensa/renode_xtensa_callbacks.c` - Native callbacks
- Platform file: `platforms/cpus/xtensa-sample-controller.repl` - A **generic** Xtensa sample controller, **NOT** an ESP32 platform
- The Xtensa.cs CPU class (6.4 KB) supports: semihosting UART, 3 internal comparing timers, GPIO interrupt handling, and simcall operations (exit, read, write, open, close, lseek)
- The `cpuType` is `"sample_controller"` -- a generic Xtensa config. ESP32 would need type `"esp32"` (LX6), ESP32-S2/S3 would need LX7 variants. These **do not exist**.
- The only Xtensa test (`xtensa.robot`) has 2 cases: a manual opcode division test and a Zephyr hello\_world that checks for `"Hello World! qemu_xtensa"` output
- The sample controller platform has only: memory regions + Xtensa CPU + SemihostingUart
- **No ESP32-specific Xtensa configuration** (register windows, interrupts, memory map, etc.) has been implemented
- **Gap:** Xtensa is configurable per licensee. The ESP32's specific Xtensa configuration (interrupt matrix, memory protection, register windows overlay, etc.) would need to be carefully implemented to match the ESP32 hardware. The LX7 used in ESP32-S2/S3 is a different configuration again.

#### RISC-V (ESP32-C series, ESP32-H series, ESP32-P4)

- **Status: Good (architecture), Missing (ESP-specific peripherals)**
- Renode has **excellent general RISC-V support** with many RISC-V platforms already working (SiFive, LiteX, etc.)
- Supports RV32/RV64, with extensive ISA extensions: **I, E, M, A, F, D, C, S, U, V, B, G** plus standard extensions **Zba, Zbb, Zbc, Zbs** (bit manipulation), **Zicsr, Zifencei**, **Zfh/Zvfh** (half-precision float), **Zve32x/f, Zve64x/f/d** (vector subsets), **Zcb, Zcmp, Zcmt** (code-size reduction), **Zacas** (atomic CAS), and more
- ESP32-C3 needs RV32IMC -- **fully covered** by Renode
- ESP32-C6/H2 need RV32IMAC -- **fully covered** by Renode
- ESP32-P4 needs RV32IMAFC -- **fully covered** by Renode
- Specialized cores available in Renode: VexRiscv, Ibex, PicoRV32, CV32E40P, Minerva, VeeR EL2, Ri5cy
- **Gap:** While the CPU core works, none of the ESP32 peripheral models exist, so a RISC-V ESP32-C3 platform cannot boot ESP-IDF firmware

### ESP-Specific Peripheral Support in Renode

| Peripheral | Renode Status | Source File |
|---|---|---|
| **UART** | **Implemented** (v1.16.0, Aug 2025; co-authored by Sean "xobs" Cross) | `ESP32_UART.cs` |
| GPIO | Not implemented | -- |
| SPI / SPI Flash | Not implemented | -- |
| I2C | Not implemented | -- |
| I2S | Not implemented | -- |
| Timer Groups | Not implemented | -- |
| System Timer | Not implemented | -- |
| Watchdog | Not implemented | -- |
| Interrupt Matrix | Not implemented | -- |
| DMA (PDMA/GDMA) | Not implemented | -- |
| WiFi | Not implemented | -- |
| Bluetooth / BLE | Not implemented | -- |
| IEEE 802.15.4 | Not implemented | -- |
| ADC / DAC | Not implemented | -- |
| eFuse | Not implemented | -- |
| RNG | Not implemented | -- |
| AES / SHA / RSA | Not implemented | -- |
| HMAC / DS | Not implemented | -- |
| Flash Encryption | Not implemented | -- |
| USB OTG | Not implemented | -- |
| USB Serial/JTAG | Not implemented | -- |
| RMT | Not implemented | -- |
| LEDC / PWM | Not implemented | -- |
| MCPWM | Not implemented | -- |
| TWAI / CAN | Not implemented | -- |
| Touch Sensor | Not implemented | -- |
| Camera Interface | Not implemented | -- |
| LCD Interface | Not implemented | -- |
| SDIO / SD Host | Not implemented | -- |
| RTC Control | Not implemented | -- |
| DPORT / System registers | Not implemented | -- |
| Cache / MMU | Not implemented | -- |
| Brownout Detector | Not implemented | -- |
| Power Management | Not implemented | -- |
| Clock Control | Not implemented | -- |

**Summary: 1 out of ~35 key peripherals implemented (UART only).**

### Renode Wireless Emulation Infrastructure

Renode does have wireless simulation infrastructure, but not for ESP-specific radios (see [Wireless Hardware Documentation](03-wireless-hardware-documentation.md) for what's needed):

- `BLEMedium.cs` - BLE medium simulation (used for nRF52840, not ESP)
- `IEEE802_15_4Medium.cs` - 802.15.4 medium (used for CC2538, AT86RF233, not ESP)
- `WirelessMedium.cs` - Base wireless medium class
- Existing wireless peripheral models: CC2538RF, CC1200, CC2520, AT86RF233, NRF52840_Radio, EFR32xG24_Radio, EmberRadio

The wireless infrastructure could potentially be reused for ESP WiFi/BLE, but ESP-specific radio peripheral models would need to be written.

### Renode Platform Files for ESP

**None exist.** There are no `.repl` or `.resc` files for any ESP32 variant. The only Xtensa-related files are:
- `platforms/cpus/xtensa-sample-controller.repl` (generic Xtensa, not ESP)
- `scripts/single-node/xtensa.resc` (generic test script)
- `tests/platforms/xtensa.robot` (basic Xtensa test)

---

## 6. Espressif QEMU Fork Support Status

Espressif maintains a fork of QEMU at [github.com/espressif/qemu](https://github.com/espressif/qemu) (branch: `esp-develop`) with the most comprehensive open-source ESP emulation available.

### Supported Chips

| Chip | CPU Emulation | SoC Emulation |
|---|---|---|
| **ESP32** | Xtensa (full) | Yes |
| **ESP32-S3** | Xtensa (full) | Yes |
| **ESP32-C3** | RISC-V (full) | Yes |
| ESP32-S2 | Not supported | Not supported |
| ESP32-C2 | Not supported | Not supported |
| ESP32-C5 | Not supported | Not supported |
| ESP32-C6 | Not supported | Not supported |
| ESP32-H2 | Not supported | Not supported |
| ESP32-P4 | Not supported | Not supported |

### Peripheral Support Matrix (Espressif QEMU)

| Peripheral | ESP32 | ESP32-S3 | ESP32-C3 |
|---|---|---|---|
| **Dual-Core CPU** | Yes | Yes | N/A (single) |
| **UART** | Yes | Yes | Yes |
| **Interrupt Matrix** | Yes | Yes | Yes |
| **GPIO Strap** | Yes | Yes | Yes |
| **NOR Flash (SPI)** | Yes | Yes | Yes |
| **NOR Flash (MMU)** | Yes | Yes* | Yes |
| **Flash Encryption** | Yes | Yes | Yes |
| **PSRAM (QPI)** | Yes | Yes | N/A |
| **PSRAM (OPI)** | Yes* | Yes | N/A |
| **PSRAM (MMU)** | N/A | Yes* | N/A |
| **eFuse** | Yes | Yes | Yes |
| **RNG** | Yes | Yes | Yes |
| **GDMA** | N/A | Yes | Yes |
| **AES** | Yes | Yes | Yes |
| **SHA** | Yes | Yes | Yes |
| **RSA** | Yes | Yes | Yes |
| **HMAC** | N/A | Yes | Yes |
| **Digital Signature** | N/A | Yes | Yes |
| **SysTimer** | N/A | Yes | Yes |
| **Timer Groups** | Yes | Yes | Yes |
| **TWAI/CAN** | Yes | Yes | Yes |
| **SD/MMC** | Yes | -- | N/A |
| **LEDC** | Yes | -- | -- |
| **Ethernet** | Yes* | Yes* | Yes* |
| **WiFi** | **No** | **No** | **No** |
| **Bluetooth** | **No** | **No** | **No** |
| **USB** | **No** | **No** | **No** |
| **RMT** | **No** | **No** | **No** |
| **GP SPI** | **No** | **No** | **No** |
| **I2C** | **No** | **No** | **No** |
| **ADC** | **No** | **No** | **No** |
| **DAC** | **No** | **No** | N/A |
| **I2S** | **No** | **No** | **No** |
| **PCNT** | **No** | **No** | **No** |
| **MCPWM** | **No** | **No** | N/A |
| **Touch Sensor** | **No** | N/A | N/A |

Notes:
- `*` = Emulated with caveats (e.g., uses host MMU for flash mapping, or uses OpenCores Ethernet instead of real ESP Ethernet MAC)
- **Ethernet** support uses OpenCores, a virtual peripheral not present on real hardware, as a workaround for the lack of WiFi emulation. This allows TCP/IP networking in emulated targets.
- **RGB Framebuffer** is another virtual peripheral (not real HW) included for GUI testing.

### QEMU Source Files for ESP

The Espressif QEMU fork contains extensive ESP-specific source code:

**ESP32 (Xtensa):**
- `hw/xtensa/esp32.c` - ESP32 SoC definition
- `hw/xtensa/esp32_intc.c` - Interrupt controller
- `hw/char/esp32_uart.c`, `hw/gpio/esp32_gpio.c`, `hw/ssi/esp32_spi.c`
- `hw/misc/esp32_aes.c`, `hw/misc/esp32_sha.c`, `hw/misc/esp32_rsa.c`
- `hw/misc/esp32_dport.c`, `hw/misc/esp32_rtc_cntl.c`, `hw/misc/esp32_rng.c`
- `hw/misc/esp32_flash_enc.c`, `hw/misc/esp32_ledc.c`
- `hw/nvram/esp32_efuse.c`, `hw/timer/esp32_timg.c`, `hw/timer/esp32_frc_timer.c`
- `hw/net/can/esp32_twai.c`, `hw/i2c/esp32_i2c.c`

**ESP32-S3 (Xtensa):**
- `hw/xtensa/esp32s3.c`, `hw/xtensa/esp32s3_intc.c`, `hw/xtensa/esp32s3_clk.c`
- `hw/char/esp32s3_uart.c`, `hw/gpio/esp32s3_gpio.c`, `hw/ssi/esp32s3_spi.c`
- `hw/dma/esp32s3_gdma.c`
- `hw/misc/esp32s3_aes.c`, `hw/misc/esp32s3_sha.c`, `hw/misc/esp32s3_rsa.c`
- `hw/misc/esp32s3_hmac.c`, `hw/misc/esp32s3_ds.c`, `hw/misc/esp32s3_xts_aes.c`
- `hw/misc/esp32s3_cache.c`, `hw/misc/esp32s3_pms.c`, `hw/misc/esp32s3_rng.c`
- `hw/misc/esp32s3_rtc_cntl.c`
- `hw/nvram/esp32s3_efuse.c`
- `hw/timer/esp32s3_timg.c`, `hw/timer/esp32s3_systimer.c`
- `hw/net/can/esp32s3_twai.c`

**ESP32-C3 (RISC-V):**
- `hw/riscv/esp32c3.c`, `hw/riscv/esp32c3_clk.c`, `hw/riscv/esp32c3_intmatrix.c`
- `hw/char/esp32c3_uart.c`, `hw/gpio/esp32c3_gpio.c`, `hw/ssi/esp32c3_spi.c`
- `hw/dma/esp32c3_gdma.c`
- `hw/misc/esp32c3_aes.c`, `hw/misc/esp32c3_sha.c`, `hw/misc/esp32c3_rsa.c`
- `hw/misc/esp32c3_hmac.c`, `hw/misc/esp32c3_ds.c`, `hw/misc/esp32c3_xts_aes.c`
- `hw/misc/esp32c3_cache.c`, `hw/misc/esp32c3_jtag.c`, `hw/misc/esp32c3_rtc_cntl.c`
- `hw/nvram/esp32c3_efuse.c`
- `hw/timer/esp32c3_timg.c`, `hw/timer/esp32c3_systimer.c`
- `hw/net/can/esp32c3_twai.c`

### ESP-IDF Integration

Espressif's QEMU fork is tightly integrated with ESP-IDF:

- **`idf.py qemu monitor`** and **`idf.py qemu gdb`** commands are built into ESP-IDF
- Pre-built QEMU binaries installable via `idf_tools.py install qemu-xtensa qemu-riscv32`
- **pytest-embedded-qemu** plugin enables automated testing of ESP-IDF apps in QEMU
- ESP-IDF CI pipelines use QEMU for running test apps with sdkconfig.ci configurations

### Known QEMU Limitations and Open Issues

- **I2C only implemented for ESP32** (not ESP32-S3 or ESP32-C3)
- **No ULP coprocessor emulation** on any target
- SD/MMC not working on ESP32-S3 (issue #139)
- eFuse coding scheme support incomplete (issue #143)
- No TCG plugins support (issue #134)
- Windows DLL dependency issues (issue #146)
- The OpenCores Ethernet workaround, while functional for TCP/IP, means WiFi-specific APIs (scanning, AP mode, mesh networking) cannot be tested

---

## 7. Other Open-Source Emulation Platforms

### Wokwi (Proprietary, Free Tier)

[Wokwi](https://wokwi.com) is a browser-based electronics simulator (closed source) that offers the broadest ESP chip coverage of any emulator:

- **Supported chips:** ESP32, ESP32-S2, ESP32-S3, ESP32-C3, ESP32-C5 (alpha), ESP32-C6, ESP32-H2, ESP32-P4 (beta)
- **WiFi simulation:** Yes (virtual network, can make real HTTP requests)
- **Frameworks:** Arduino, ESP-IDF, MicroPython, CircuitPython, Rust (no\_std and std)
- **Peripheral simulation:** GPIO, UART, SPI, I2C, ADC, PWM, and many external components
- **Limitations:** Closed source, browser-based only, cannot run in CI/CD pipelines without paid plan, no BLE simulation, timing may not be cycle-accurate
- **IDE integration:** VS Code extension available

### Upstream QEMU

- Upstream QEMU has **general Xtensa ISA support** (contributed by Cadence/Tensilica and Max Filippov)
- The `xtensa-softmmu` target can emulate generic Xtensa cores
- However, upstream QEMU does **not** include ESP32-specific SoC definitions or peripherals
- Upstream QEMU has excellent **RISC-V support** (`riscv32-softmmu`, `riscv64-softmmu`)
- ESP32-C3/C6/H2 could potentially run on upstream QEMU's RISC-V if ESP peripherals were ported

### Unicorn Engine

- Based on QEMU's CPU emulation layer
- Has Xtensa support (from QEMU)
- CPU emulation only, no peripheral/SoC support
- Not useful for ESP development without significant extension work

### ESPTOOL / ESP-IDF Virtual Testing

- Espressif's own CI uses their QEMU fork for automated testing
- `pytest-embedded-qemu` plugin supports running ESP-IDF tests in QEMU
- Limited to peripherals that QEMU supports (no WiFi/BT tests)

---

## 8. Emulator Peripheral Support Comparison Matrix

This table compares what each open-source emulator supports for the **most popular ESP chips** (ESP32, ESP32-S3, ESP32-C3, ESP32-C6).

### ESP32 (Xtensa LX6, Dual-Core)

| Peripheral | Renode | QEMU (Espressif) | Wokwi |
|---|---|---|---|
| CPU (Xtensa LX6) | Partial | **Full** | **Full** |
| Dual-Core SMP | No | **Yes** | **Yes** |
| UART | **Yes** (1 model) | **Yes** | **Yes** |
| GPIO | No | **Yes** | **Yes** |
| SPI | No | **Yes** (flash only) | **Yes** |
| I2C | No | **Yes** | **Yes** |
| Timer Groups | No | **Yes** | **Yes** |
| Interrupt Matrix | No | **Yes** | **Yes** |
| eFuse | No | **Yes** | Partial |
| Flash SPI + MMU | No | **Yes** | **Yes** |
| Flash Encryption | No | **Yes** | No |
| PSRAM | No | **Yes** | **Yes** |
| AES/SHA/RSA | No | **Yes** | No |
| RNG | No | **Yes** | **Yes** |
| DMA | No | N/A (PDMA) | Partial |
| TWAI/CAN | No | **Yes** | **Yes** |
| SD/MMC | No | **Yes** | No |
| LEDC | No | **Yes** | **Yes** |
| DPORT | No | **Yes** | Unknown |
| RTC Control | No | **Yes** | Partial |
| **WiFi** | **No** | **No** | **Yes** |
| **Bluetooth** | **No** | **No** | **No** |
| ADC | No | No | **Yes** |
| DAC | No | No | Partial |
| I2S | No | No | Partial |
| RMT | No | No | **Yes** |
| Touch | No | No | **Yes** |
| USB | N/A | N/A | N/A |
| Ethernet (real) | No | No | No |

### ESP32-C3 (RISC-V, Single-Core)

| Peripheral | Renode | QEMU (Espressif) | Wokwi |
|---|---|---|---|
| CPU (RV32IMC) | **Yes** (core) | **Full** | **Full** |
| UART | **Yes** (model) | **Yes** | **Yes** |
| GPIO | No | **Yes** | **Yes** |
| SPI | No | **Yes** | **Yes** |
| I2C | No | No | **Yes** |
| Timer Groups | No | **Yes** | **Yes** |
| System Timer | No | **Yes** | **Yes** |
| Interrupt Matrix | No | **Yes** | **Yes** |
| eFuse | No | **Yes** | Partial |
| Flash SPI + MMU | No | **Yes** | **Yes** |
| Flash Encryption | No | **Yes** | No |
| AES/SHA/RSA | No | **Yes** | No |
| HMAC/DS | No | **Yes** | No |
| GDMA | No | **Yes** | Partial |
| RNG | No | **Yes** | **Yes** |
| TWAI/CAN | No | **Yes** | **Yes** |
| RTC Control | No | **Yes** | Partial |
| USB Serial/JTAG | No | No | Partial |
| **WiFi** | **No** | **No** | **Yes** |
| **Bluetooth** | **No** | **No** | **No** |
| ADC | No | No | **Yes** |
| I2S | No | No | Partial |
| RMT | No | No | **Yes** |
| LEDC | No | No | **Yes** |

### ESP32-C6 (RISC-V, HP+LP cores, WiFi 6 + 802.15.4)

| Peripheral | Renode | QEMU (Espressif) | Wokwi |
|---|---|---|---|
| CPU (RV32IMAC) | **Yes** (core) | Not supported | **Yes** |
| LP Core | No | Not supported | Unknown |
| UART | Likely works | Not supported | **Yes** |
| GPIO | No | Not supported | **Yes** |
| **WiFi 6** | **No** | **No** | **Yes** |
| **BLE 5.3** | **No** | **No** | **No** |
| **802.15.4** | **No** | **No** | **Yes** |
| All others | No | Not supported | Partial-Yes |

