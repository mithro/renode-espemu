# ESP Chip Family Emulation Status: Renode, QEMU, and Other Platforms

**Date:** 2026-03-28
**Purpose:** Understand the work needed to enable Renode to fully emulate all common ESP WiFi/Bluetooth chipsets for faster software development.

---

## Table of Contents

1. [ESP Part Families Overview](#1-esp-part-families-overview)
2. [ESP Family Comparison Table](#2-esp-family-comparison-table)
3. [Peripheral Comparison Across ESP Families](#3-peripheral-comparison-across-esp-families)
4. [CPU Architecture Details](#4-cpu-architecture-details)
5. [Renode Emulation Support Status](#5-renode-emulation-support-status)
6. [Espressif QEMU Fork Support Status](#6-espressif-qemu-fork-support-status)
7. [Other Open-Source Emulation Platforms](#7-other-open-source-emulation-platforms)
8. [Emulator Peripheral Support Comparison Matrix](#8-emulator-peripheral-support-comparison-matrix)
9. [Gap Analysis and Roadmap](#9-gap-analysis-and-roadmap)
10. [Recommendations](#10-recommendations)

---

## 1. ESP Part Families Overview

Espressif Systems produces several families of WiFi/Bluetooth-capable SoCs. The families are grouped by CPU architecture and target market segment.

### ESP8266 / ESP8285 (Legacy)

- **CPU:** Tensilica Diamond Standard 106Micro (L106) @ 80 MHz (default) or 160 MHz
- **Cores:** 1
- **Memory:** 32 KiB instruction RAM, 80 KiB user data RAM
- **WiFi:** 802.11 b/g/n
- **Bluetooth:** None
- **GPIO:** 16 pins
- **Other:** SPI, I2C, I2S, UART, 10-bit ADC (1 channel)
- **Status:** Legacy; Espressif recommends upgrading to ESP32-C2 (ESP8684)
- **Notes:** ESP8285 is the same chip with 1 MiB built-in flash. No Bluetooth. Very limited peripherals compared to ESP32 family.

### ESP32 (Original)

- **CPU:** Xtensa LX6 @ up to 240 MHz
- **Cores:** 1 or 2 (dual-core is the common variant)
- **FPU:** Single precision
- **Coprocessor:** Ultra-low-power (ULP) FSM @ 20 MHz
- **SRAM:** 520 KiB
- **ROM:** 448 KiB
- **RTC SRAM:** 8 KiB
- **PSRAM:** Up to 4 MiB (Quad SPI)
- **WiFi:** 802.11 b/g/n/e/i
- **Bluetooth:** 4.2 BR/EDR + BLE
- **Ethernet:** MAC + RMII
- **GPIO:** 34 pins
- **ADC:** 2 x 12-bit SAR, 18 channels
- **DAC:** 2 x 8-bit
- **UART:** 3
- **SPI:** 4 (includes flash SPI)
- **I2C:** 2
- **I2S:** 2
- **PWM:** LEDC + MCPWM
- **Other:** Touch sensor (10 channels), RMT (IR), SDIO/SD host, TWAI/CAN 2.0, camera interface (I2S), Hall sensor
- **DMA:** PDMA (peripheral DMA)
- **Crypto:** AES, SHA, RSA, RNG (hardware accelerators)
- **Secure Boot:** V1, V2
- **Storage:** 4-bit SD, SPI SD
- **Status:** Active (mature, widely deployed)

### ESP32-S2

- **CPU:** Xtensa LX7 @ up to 240 MHz
- **Cores:** 1 (single-core only)
- **FPU:** Single precision
- **Coprocessor:** ULP FSM + ULP RISC-V @ 20 MHz
- **SRAM:** 320 KiB
- **ROM:** 128 KiB
- **RTC SRAM:** 16 KiB
- **PSRAM:** Up to 8 MiB (Octal SPI)
- **WiFi:** 802.11 b/g/n
- **Bluetooth:** None
- **Ethernet:** SPI only (no built-in MAC)
- **GPIO:** 43 pins
- **ADC:** 2 x 13-bit SAR, 20 channels
- **DAC:** 2 x 8-bit
- **UART:** 2
- **SPI:** 4 (includes flash SPI)
- **I2C:** 2 (1 per Wikipedia family table)
- **I2S:** 1
- **USB:** USB OTG 1.1 (Full Speed)
- **PWM:** LEDC (no MCPWM)
- **Other:** Touch sensor (14 channels), RMT, LCD interface (SPI), camera interface (DVP)
- **DMA:** GDMA
- **Crypto:** AES, SHA, RSA, HMAC, Digital Signature, RNG
- **Secure Boot:** V2
- **Status:** Active

### ESP32-S3

- **CPU:** Xtensa LX7 @ up to 240 MHz
- **Cores:** 2 (dual-core)
- **FPU:** Single precision
- **Coprocessor:** ULP FSM + ULP RISC-V (RV32IMC) @ 17.5 MHz
- **SRAM:** 512 KiB
- **ROM:** 384 KiB
- **RTC SRAM:** 8 KiB
- **PSRAM:** Up to 8 MiB (Octal SPI)
- **WiFi:** 802.11 b/g/n
- **Bluetooth:** 5.0 LE
- **Ethernet:** SPI only
- **GPIO:** 45 pins
- **ADC:** 2 x 12-bit SAR, 20 channels
- **DAC:** None
- **UART:** 3
- **SPI:** 4 (includes flash SPI)
- **I2C:** 2
- **I2S:** 2
- **USB:** USB OTG 1.1 (Full Speed) + USB Serial/JTAG
- **PWM:** LEDC + MCPWM
- **Other:** Touch sensor (14 channels), RMT, LCD interface (SPI + parallel), camera (DVP), AI/vector extensions
- **DMA:** GDMA
- **Crypto:** AES, SHA, RSA, HMAC, Digital Signature, XTS-AES, RNG
- **Secure Boot:** V2
- **Display:** SPI LCD, parallel LCD
- **AI:** Vector extensions for neural network acceleration
- **Status:** Active (flagship for AIoT/HMI applications)

### ESP32-C2 (ESP8684)

- **CPU:** RISC-V (RV32IMAC) @ up to 120 MHz
- **Cores:** 1
- **FPU:** None
- **Coprocessor:** None
- **SRAM:** 272 KiB
- **ROM:** 576 KiB
- **WiFi:** 802.11 b/g/n
- **Bluetooth:** 5.0 LE
- **Ethernet:** SPI only
- **GPIO:** 20 pins
- **ADC:** 1 x 12-bit SAR, 6 channels
- **DAC:** None
- **UART:** 2
- **SPI:** 2
- **I2C:** 1
- **I2S:** None
- **USB:** None
- **PWM:** LEDC (no MCPWM)
- **DMA:** GDMA
- **Crypto:** AES, SHA, RNG (no RSA)
- **Secure Boot:** V2
- **Status:** Active (cost-optimized replacement for ESP8266)

### ESP32-C3

- **CPU:** RISC-V (RV32IMC) @ up to 160 MHz
- **Cores:** 1
- **FPU:** None
- **Coprocessor:** None
- **SRAM:** 400 KiB
- **ROM:** 384 KiB
- **RTC SRAM:** 8 KiB
- **WiFi:** 802.11 b/g/n
- **Bluetooth:** 5.0 LE
- **Ethernet:** SPI only
- **GPIO:** 22 pins
- **ADC:** 2 x 12-bit SAR, 6 channels
- **DAC:** None
- **UART:** 2
- **SPI:** 3
- **I2C:** 1
- **I2S:** 1
- **USB:** USB Serial/JTAG (not OTG)
- **PWM:** LEDC (no MCPWM)
- **Other:** RMT, temperature sensor
- **DMA:** GDMA
- **Crypto:** AES, SHA, RSA, HMAC, Digital Signature, XTS-AES, RNG
- **Secure Boot:** V2
- **Status:** Active (popular cost-effective RISC-V option)

### ESP32-C5

- **CPU:** RISC-V (RV32IMAC) @ up to 240 MHz
- **Cores:** 1
- **FPU:** None
- **Coprocessor:** None
- **SRAM:** 384 KiB
- **ROM:** 256 KiB
- **WiFi:** 802.11 b/g/n/ac/ax (WiFi 6, **dual-band 2.4 + 5 GHz**)
- **Bluetooth:** 5.0 LE
- **Ethernet:** SPI only
- **GPIO:** 30 pins (estimated, based on similar C-series)
- **ADC:** 1 x 12-bit SAR, 7 channels
- **UART:** 3
- **SPI:** 3
- **I2C:** 2
- **I2S:** 1
- **USB:** USB Serial/JTAG
- **PWM:** LEDC
- **DMA:** GDMA
- **Crypto:** AES, SHA, RSA, HMAC, DS, XTS-AES, RNG
- **Secure Boot:** V2
- **Status:** Active (first ESP with dual-band WiFi 6)

### ESP32-C6

- **CPU:** RISC-V (RV32IMAC) @ up to 160 MHz
- **Cores:** 1 (HP core) + 1 low-power RISC-V core (RV32IMAC @ 20 MHz)
- **FPU:** None
- **SRAM:** 512 KiB
- **ROM:** 256 KiB (per Wikipedia table, noting some sources vary)
- **RTC SRAM:** 16 KiB
- **WiFi:** 802.11 b/g/n/ax (WiFi 6, 2.4 GHz only)
- **Bluetooth:** 5.3 LE
- **IEEE 802.15.4:** Yes (Thread, Zigbee, Matter)
- **Ethernet:** SPI only
- **GPIO:** 30 pins
- **ADC:** 1 x 12-bit SAR, 7 channels
- **UART:** 3 (incl. LP UART)
- **SPI:** 3
- **I2C:** 2 (incl. LP I2C)
- **I2S:** 1
- **USB:** USB Serial/JTAG
- **PWM:** LEDC + MCPWM
- **Other:** RMT, temperature sensor, TWAI/CAN 2.0
- **DMA:** GDMA + UHCI
- **Crypto:** AES, SHA, RSA, HMAC, DS, XTS-AES, RNG
- **Secure Boot:** V2
- **Status:** Active (key chip for Matter/Thread ecosystem)

### ESP32-C61

- **CPU:** RISC-V (RV32IMAC) @ up to 160 MHz
- **Cores:** 1
- **FPU:** None
- **SRAM:** 320 KiB
- **WiFi:** 802.11 b/g/n/ax (WiFi 6, 2.4 GHz)
- **Bluetooth:** 6.0 LE
- **GPIO:** 30 pins
- **Status:** Active (affordable WiFi 6 connectivity)

### ESP32-H2

- **CPU:** RISC-V (RV32IMAC) @ up to 96 MHz
- **Cores:** 1
- **FPU:** None
- **SRAM:** 256 KiB (per Wikipedia family table)
- **ROM:** 128 KiB
- **WiFi:** None
- **Bluetooth:** 5.3 LE
- **IEEE 802.15.4:** Yes (Thread, Zigbee, Matter)
- **GPIO:** 26 pins
- **ADC:** 1 x 12-bit SAR, 5 channels
- **UART:** 2
- **SPI:** 2
- **I2C:** 2
- **I2S:** 1
- **USB:** USB Serial/JTAG
- **PWM:** LEDC + MCPWM
- **Other:** RMT, temperature sensor, TWAI/CAN
- **DMA:** GDMA
- **Crypto:** AES, SHA, RSA, HMAC, DS, XTS-AES, ECC, RNG
- **Secure Boot:** V2
- **Status:** Active (designed for Thread/Zigbee border routers and end devices)

### ESP32-H4 (Announced)

- **CPU:** RISC-V dual-core
- **WiFi:** None (connectivity co-processor expected)
- **Bluetooth:** BLE
- **IEEE 802.15.4:** Yes (Thread, Zigbee)
- **Notes:** Next-gen ultra-low-power dual-core SoC for long battery life and HMI applications
- **Status:** Announced / Pre-release

### ESP32-H21 (Announced)

- **CPU:** RISC-V
- **Notes:** Ultra-low-power SoC designed for battery-powered devices
- **Status:** Announced / Pre-release

### ESP32-P4

- **CPU:** RISC-V (RV32IMAFC) @ up to 400 MHz
- **Cores:** 2 (dual-core HP) + 1 low-power RISC-V core (RV32IMC)
- **FPU:** Single precision (F extension)
- **SRAM:** 768 KiB
- **ROM:** 512 KiB
- **RTC SRAM:** 32 KiB
- **PSRAM:** Up to 16 MiB (Hex SPI)
- **WiFi:** None (requires external WiFi co-processor like ESP32-C6)
- **Bluetooth:** None
- **Ethernet:** MAC + RMII (built-in)
- **GPIO:** 50 pins
- **ADC:** None (per Wikipedia family table)
- **UART:** 2
- **SPI:** 3
- **I2C:** 2
- **I2S:** 3
- **USB:** USB OTG 2.0 (High Speed)
- **PWM:** LEDC + MCPWM
- **Other:** MIPI-CSI camera, MIPI-DSI display, SDIO, temperature sensor
- **DMA:** GDMA
- **Crypto:** AES, SHA, RSA, HMAC, DS, XTS-AES, ECC, RNG
- **Display:** SPI LCD, parallel LCD, MIPI-DSI (HMI-focused)
- **AI:** Vector extensions
- **Status:** Active (high-performance HMI SoC, no built-in wireless)

### ESP32-E22 (Announced)

- **Notes:** Tri-band WiFi 6E + dual-mode Bluetooth connectivity co-processor
- **Status:** Announced / Pre-release

---

## 2. ESP Family Comparison Table

| Feature | ESP8266 | ESP32 | ESP32-S2 | ESP32-S3 | ESP32-C2 | ESP32-C3 | ESP32-C5 | ESP32-C6 | ESP32-C61 | ESP32-H2 | ESP32-P4 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **CPU** | L106 | Xtensa LX6 | Xtensa LX7 | Xtensa LX7 | RV32IMAC | RV32IMC | RV32IMAC | RV32IMAC | RV32IMAC | RV32IMAC | RV32IMAFC |
| **Cores** | 1 | 1 or 2 | 1 | 2 | 1 | 1 | 1 | 1+LP | 1 | 1 | 2+LP |
| **Max Freq** | 160 MHz | 240 MHz | 240 MHz | 240 MHz | 120 MHz | 160 MHz | 240 MHz | 160 MHz | 160 MHz | 96 MHz | 400 MHz |
| **SRAM** | 80 KiB | 520 KiB | 320 KiB | 512 KiB | 272 KiB | 400 KiB | 384 KiB | 512 KiB | 320 KiB | 256 KiB | 768 KiB |
| **WiFi** | b/g/n | b/g/n | b/g/n | b/g/n | b/g/n | b/g/n | b/g/n/ac/ax | b/g/n/ax | b/g/n/ax | -- | -- |
| **Bluetooth** | -- | 4.2+BR/EDR | -- | 5.0 LE | 5.0 LE | 5.0 LE | 5.0 LE | 5.3 LE | 6.0 LE | 5.3 LE | -- |
| **802.15.4** | -- | -- | -- | -- | -- | -- | -- | Yes | -- | Yes | -- |
| **GPIO** | 16 | 34 | 43 | 45 | 20 | 22 | ~30 | 30 | 30 | 26 | 50 |
| **UART** | 2 | 3 | 2 | 3 | 2 | 2 | 3 | 3 | -- | 2 | 2 |
| **SPI** | 2 | 4 | 4 | 4 | 2 | 3 | 3 | 3 | -- | 2 | 3 |
| **I2C** | 1 | 2 | 1 | 2 | 1 | 1 | 2 | 2 | -- | 2 | 2 |
| **I2S** | 1 | 2 | 1 | 2 | -- | 1 | 1 | 1 | -- | 1 | 3 |
| **USB** | -- | -- | OTG | OTG+JTAG | -- | Serial/JTAG | Serial/JTAG | Serial/JTAG | -- | Serial/JTAG | OTG 2.0 HS |
| **ADC** | 1x10-bit | 2x12-bit | 2x13-bit | 2x12-bit | 1x12-bit | 2x12-bit | 1x12-bit | 1x12-bit | -- | 1x12-bit | -- |
| **DAC** | -- | 2x8-bit | 2x8-bit | -- | -- | -- | -- | -- | -- | -- | -- |
| **Ethernet** | -- | MAC+RMII | -- | -- | -- | -- | -- | -- | -- | -- | MAC+RMII |
| **TWAI/CAN** | -- | Yes | -- | Yes | -- | Yes | -- | Yes | -- | Yes | Yes |
| **Touch** | -- | 10 ch | 14 ch | 14 ch | -- | -- | -- | -- | -- | -- | -- |
| **Temp Sensor** | -- | -- | -- | Yes | -- | Yes | -- | Yes | -- | Yes | Yes |
| **DMA** | -- | PDMA | GDMA | GDMA | GDMA | GDMA | GDMA | GDMA+UHCI | -- | GDMA | GDMA |
| **Crypto HW** | -- | AES/SHA/RSA | Full | Full | AES/SHA | Full | Full | Full | -- | Full+ECC | Full+ECC |

Legend: "Full" crypto = AES + SHA + RSA + HMAC + Digital Signature + XTS-AES + RNG. "--" = not present/not applicable.

---

## 3. Peripheral Comparison Across ESP Families

### Wireless Connectivity Summary

| Chip | WiFi Standard | WiFi Bands | Bluetooth | 802.15.4 (Zigbee/Thread) | Matter Support |
|---|---|---|---|---|---|
| ESP8266 | 802.11 b/g/n | 2.4 GHz | -- | -- | -- |
| ESP32 | 802.11 b/g/n | 2.4 GHz | 4.2 Classic+LE | -- | Via WiFi only |
| ESP32-S2 | 802.11 b/g/n | 2.4 GHz | -- | -- | Via WiFi only |
| ESP32-S3 | 802.11 b/g/n | 2.4 GHz | 5.0 LE | -- | Via WiFi or BLE |
| ESP32-C2 | 802.11 b/g/n | 2.4 GHz | 5.0 LE | -- | Via WiFi or BLE |
| ESP32-C3 | 802.11 b/g/n | 2.4 GHz | 5.0 LE | -- | Via WiFi or BLE |
| ESP32-C5 | 802.11 b/g/n/ac/ax | **2.4+5 GHz** | 5.0 LE | -- | Via WiFi or BLE |
| ESP32-C6 | 802.11 b/g/n/ax | 2.4 GHz | 5.3 LE | **Yes** | **WiFi + Thread** |
| ESP32-C61 | 802.11 b/g/n/ax | 2.4 GHz | 6.0 LE | -- | Via WiFi or BLE |
| ESP32-H2 | -- | -- | 5.3 LE | **Yes** | **Thread only** |
| ESP32-P4 | -- | -- | -- | -- | Via co-processor |

### Key Peripheral Groups by Use Case

**For WiFi/BT IoT development (most common):** ESP32, ESP32-S3, ESP32-C3, ESP32-C6 are the workhorses.

**For Thread/Zigbee/Matter:** ESP32-C6 and ESP32-H2 are the primary targets.

**For HMI/Display applications:** ESP32-S3 (LCD) and ESP32-P4 (MIPI-DSI) are the targets.

---

## 4. CPU Architecture Details

### Tensilica L106 (ESP8266)

- 32-bit, single-issue, in-order pipeline
- Proprietary Tensilica Diamond Standard 106Micro
- Not the same ISA as Xtensa LX6/LX7 used in ESP32 family
- Very limited toolchain availability

### Xtensa LX6 (ESP32)

- 32-bit, configurable processor with windowed registers
- Up to 64 physical registers, 16 visible at a time (register windows)
- 7-stage pipeline
- Optional single-precision FPU
- Custom DSP instructions possible
- **Key ISA features:** register windowing, variable-length instructions (16/24-bit), MAC16, boolean registers
- Cadence-proprietary ISA (configurable per licensee, meaning the ESP32's Xtensa config differs from, e.g., Intel's HiFi DSP Xtensa configs)

### Xtensa LX7 (ESP32-S2, ESP32-S3)

- Evolution of LX6 with improved pipeline and performance
- Same windowed register model
- ESP32-S3 adds vector extensions (for AI/ML inference)
- Binary-incompatible with LX6 at the configuration level (different register overlays and extensions)

### RISC-V (ESP32-C series, ESP32-H series, ESP32-P4)

The ESP32 RISC-V chips use different ISA extension combinations:

| Chip | ISA | Extensions | Notes |
|---|---|---|---|
| ESP32-C2 | RV32IMAC | Integer + Multiply + Atomic + Compressed | |
| ESP32-C3 | RV32IMC | Integer + Multiply + Compressed | No Atomic extension |
| ESP32-C5 | RV32IMAC | Integer + Multiply + Atomic + Compressed | |
| ESP32-C6 | RV32IMAC | Integer + Multiply + Atomic + Compressed | HP core; LP core also RV32IMAC |
| ESP32-C61 | RV32IMAC | Integer + Multiply + Atomic + Compressed | |
| ESP32-H2 | RV32IMAC | Integer + Multiply + Atomic + Compressed | |
| ESP32-P4 | RV32IMAFC | Integer + Multiply + Atomic + Float + Compressed | Only ESP with HW float in RISC-V |

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
- The sample controller has only: memory regions + Xtensa CPU + SemihostingUart
- **No ESP32-specific Xtensa configuration** (register windows, interrupts, memory map, etc.) has been implemented
- **Gap:** Xtensa is configurable per licensee. The ESP32's specific Xtensa configuration (interrupt matrix, memory protection, register windows overlay, etc.) would need to be carefully implemented to match the ESP32 hardware. The LX7 used in ESP32-S2/S3 is a different configuration again.

#### RISC-V (ESP32-C series, ESP32-H series, ESP32-P4)

- **Status: Good (architecture), Missing (ESP-specific peripherals)**
- Renode has **excellent general RISC-V support** with many RISC-V platforms already working (SiFive, LiteX, etc.)
- Supports RV32/RV64, I/M/A/F/D/C extensions
- The RISC-V CPU core itself should work for ESP32-C3/C6/H2 etc. with minimal adaptation
- **Gap:** While the CPU core would likely work, none of the ESP32 peripheral models exist, so a RISC-V ESP32-C3 platform cannot boot ESP-IDF firmware

### ESP-Specific Peripheral Support in Renode

| Peripheral | Renode Status | Source File |
|---|---|---|
| **UART** | **Implemented** | `ESP32_UART.cs` |
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

Renode does have wireless simulation infrastructure, but not for ESP-specific radios:

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

---

## 9. Gap Analysis and Roadmap

### Critical Gaps for Renode ESP Emulation

#### Tier 1: Must-Have for Basic Firmware Execution

These are required to boot ESP-IDF firmware and run basic applications:

1. **ESP32 platform definition (.repl)** - Memory map, peripheral register addresses, interrupt routing
2. **Interrupt Matrix / Controller** - ESP32's custom interrupt routing (not standard Xtensa INTC)
3. **Timer Groups** - Used by FreeRTOS tick and ESP-IDF system timer
4. **System Timer** (for C-series/H-series) - Primary system time source
5. **Flash SPI + MMU** - Execute-in-place from flash, critical for ESP-IDF
6. **Cache controller** - Instruction and data cache management
7. **RTC Control** - Clock configuration, reset reasons, boot mode
8. **DPORT / System Registers** - System configuration, peripheral clock gating
9. **Clock control** - PLL, CPU frequency, peripheral clocks
10. **GPIO (basic)** - Pin muxing, boot strapping pins

#### Tier 2: Required for Common Application Development

11. **SPI Master/Slave** - External device communication
12. **I2C Master/Slave** - Sensor communication
13. **DMA (GDMA/PDMA)** - Required by many peripheral drivers
14. **eFuse** - MAC address, calibration data, secure boot config
15. **ADC** - Analog sensor reading
16. **LEDC / PWM** - LED control, motor control
17. **RMT** - IR remote, WS2812 LED strips (very popular in maker community)
18. **TWAI / CAN** - Automotive and industrial applications
19. **Watchdog Timers** - System reliability

#### Tier 3: Required for WiFi/Bluetooth (the stated goal)

20. **WiFi Radio Peripheral** - The ESP32 WiFi hardware is largely undocumented (proprietary blob). Options:
    - **Approach A:** Model the WiFi HAL interface (intercept at the ESP-IDF `esp_wifi` API layer)
    - **Approach B:** Model the WiFi peripheral registers (requires reverse engineering the binary WiFi blob)
    - **Approach C:** Use Renode's network abstraction (virtual Ethernet, like QEMU's OpenCores approach)
21. **Bluetooth/BLE Radio Peripheral** - Similarly proprietary. Options:
    - Leverage Renode's existing `BLEMedium` infrastructure
    - Model at the HCI (Host Controller Interface) level
    - Intercept at the ESP-IDF Bluetooth API layer
22. **IEEE 802.15.4 Radio** (for ESP32-C6, ESP32-H2) - Options:
    - Leverage Renode's existing `IEEE802_15_4Medium` infrastructure
    - Model at the 802.15.4 MAC layer
23. **Coexistence Manager** - WiFi/BT/802.15.4 radio time-sharing

#### Tier 4: Nice to Have

24. USB OTG / USB Serial/JTAG
25. I2S / Audio
26. Camera interface
27. LCD/Display interface
28. Touch sensor
29. Hardware crypto accelerators (AES, SHA, RSA, etc.)
30. Secure Boot chain
31. Flash Encryption
32. Deep sleep / power management
33. Temperature sensor
34. MCPWM
35. PCNT (Pulse Counter)

### Estimated Effort

| Work Item | Complexity | Notes |
|---|---|---|
| ESP32-C3 platform (.repl) | Medium | RISC-V core already works; need memory map + peripherals |
| ESP32 platform (.repl) | High | Xtensa config needs ESP32-specific tuning |
| Interrupt matrix | Medium-High | Custom to ESP32, well-documented in TRM |
| Timer groups + SysTimer | Medium | Well-documented, straightforward register model |
| Flash SPI + MMU | High | Complex memory mapping, XIP |
| GPIO + pin mux | Medium | Register model, boot strapping |
| WiFi (HAL intercept) | Very High | Proprietary blob, would need API-level interception |
| WiFi (virtual Ethernet) | Medium | Follow QEMU's OpenCores approach |
| BLE (HCI level) | High | Could leverage existing BLEMedium |
| 802.15.4 | Medium-High | Could leverage existing IEEE802_15_4Medium |

### Recommended Priority Path

**Phase 1: ESP32-C3 (lowest friction)**
- RISC-V core already works in Renode
- Use Espressif QEMU C3 source as reference for peripheral models
- Target: boot ESP-IDF "hello_world" with UART output

**Phase 2: ESP32 (highest demand)**
- Complete Xtensa LX6 ESP32-specific configuration
- Port key peripheral models from Phase 1 (many are shared)
- Target: boot ESP-IDF with FreeRTOS on dual-core

**Phase 3: Networking**
- Implement virtual Ethernet approach (like QEMU's OpenCores) for basic TCP/IP
- Implement BLE at HCI level using Renode's BLEMedium
- Implement 802.15.4 using Renode's IEEE802_15_4Medium
- Target: ESP-IDF networking stack operational

**Phase 4: ESP32-C6 + ESP32-S3**
- ESP32-C6: RISC-V + WiFi 6 + 802.15.4 (Matter-ready)
- ESP32-S3: Xtensa LX7 + AI extensions (HMI applications)

---

## 10. Recommendations

### Strategic Observations

1. **Start with RISC-V (ESP32-C3):** Renode already has strong RISC-V support. The ESP32-C3 is the easiest path to a working ESP platform in Renode. Espressif's QEMU fork provides excellent reference implementations for all peripheral register models.

2. **Leverage QEMU source as reference:** The Espressif QEMU fork is Apache-2.0 / GPLv2 licensed. While Renode peripheral models are written in C# (vs QEMU's C), the QEMU source provides authoritative register layouts, behaviour specifications, and test cases that can guide Renode implementation.

3. **WiFi is the hardest problem:** No open-source emulator has achieved true WiFi emulation for ESP32. The WiFi hardware is controlled by a proprietary binary blob. Realistic options are:
   - **Virtual Ethernet** (QEMU's approach): Replace WiFi with a virtual Ethernet MAC. Applications using TCP/IP work, but WiFi-specific APIs (scanning, AP mode, mesh, etc.) do not.
   - **API-level interception:** Hook ESP-IDF's `esp_wifi` API functions in the emulator to provide simulated responses. More complete but requires maintenance as ESP-IDF evolves.
   - **Wokwi's approach:** Wokwi has WiFi simulation, but their implementation is closed-source.

4. **BLE is more tractable:** The BLE stack in ESP-IDF uses a well-defined HCI interface between the host stack (NimBLE/Bluedroid) and the controller. Renode already has BLE medium simulation. An ESP BLE controller model that speaks HCI would enable BLE application development.

5. **802.15.4 (Thread/Zigbee) is the most tractable wireless:** The IEEE 802.15.4 radio interface is well-documented, and Renode already has 802.15.4 medium simulation with several radio models. This is the lowest-hanging fruit for wireless emulation on ESP32-C6 and ESP32-H2.

6. **No emulator is complete:** Even Wokwi (the most complete) lacks BLE and has limited crypto. This is an area where Renode could differentiate by being the first open-source platform with comprehensive ESP wireless emulation.

### Key Resources

- **Espressif Technical Reference Manuals:** [ESP32 TRM](https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf) (and similar for each chip)
- **Espressif QEMU fork:** [github.com/espressif/qemu](https://github.com/espressif/qemu) (branch `esp-develop`)
- **Renode Xtensa support:** [Antmicro blog](https://antmicro.com/blog/2022/01/xtensa-isa-in-renode-for-sof-project/)
- **Renode GitHub issues:** [#262 (ESP32 feature request)](https://github.com/renode/renode/issues/262), [#704 (ESP32 CPU status)](https://github.com/renode/renode/issues/704)
- **ESP-IDF source:** [github.com/espressif/esp-idf](https://github.com/espressif/esp-idf) (HAL layer shows register-level access patterns)
- **Renode infrastructure:** [github.com/renode/renode-infrastructure](https://github.com/renode/renode-infrastructure) (wireless medium classes)

---

## Appendix: Data Sources

This document was compiled from:
- Wikipedia ESP32 article (family comparison table, chip variant tables)
- Espressif official product pages (espressif.com/en/products/socs/*)
- Espressif QEMU fork README (github.com/espressif/esp-toolchain-docs/blob/main/qemu/README.md)
- Espressif QEMU source tree (github.com/espressif/qemu, esp-develop branch)
- Renode documentation (renode.readthedocs.io)
- Renode source tree (github.com/renode/renode, github.com/renode/renode-infrastructure)
- Renode GitHub issues #262, #704
- Antmicro blog: "Adding Xtensa ISA support in Renode for the Sound Open Firmware project" (2022-01-28)
- Wokwi documentation (docs.wokwi.com/guides/esp32)
