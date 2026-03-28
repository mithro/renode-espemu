# ESP Hardware Reference

All Espressif ESP chip families, their specifications, peripherals, and CPU architectures.

> **See also:**
> - [Emulation Platform Status](02-emulation-platform-status.md) — what Renode/QEMU/Wokwi support for these chips
> - [Wireless Hardware Documentation](03-wireless-hardware-documentation.md) — deep dive into WiFi/BLE/802.15.4 hardware
> - [Document Index](README.md)

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

### ESP32-H4 (New)

- **CPU:** RISC-V dual-core with DSP extensions @ up to 96 MHz
- **Cores:** 2
- **SRAM:** 384 KiB
- **ROM:** 128 KiB
- **PSRAM:** External support; 2 MiB in-package option
- **WiFi:** None
- **Bluetooth:** BLE 5.4 (certified Bluetooth 6.0), LE Audio (BIS/CIS), Connection Subrating, PAwR, Direction Finding (AoA/AoD)
- **IEEE 802.15.4:** Yes (Zigbee 3.0, Thread 1.4)
- **GPIO:** 40 pins
- **Peripherals:** SPI, I2C, I2S, UART, **CAN FD** (not just CAN 2.0), LED PWM, ADC, DMA, USB-OTG, MCPWM, touch sensor (15 capacitive channels), temperature sensor
- **Other:** Integrated DC-DC converter for ultra-low-power. Supports LE Audio and Matter-over-Thread. Significant upgrade over ESP32-H2 with dual-core, more GPIO, USB OTG, touch sensor, and CAN FD.
- **Package:** 6 mm x 6 mm (QFN)
- **Status:** New / Sampling (modules ESP32-H4-WROOM-1 available)

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

| Feature         | ESP8266  | ESP32       | ESP32-S2   | ESP32-S3   | ESP32-C2 | ESP32-C3    | ESP32-C5    | ESP32-C6    | ESP32-C61 | ESP32-H2    | ESP32-P4   |
| --------------- | -------- | ----------- | ---------- | ---------- | -------- | ----------- | ----------- | ----------- | --------- | ----------- | ---------- |
| **CPU**         | L106     | Xtensa LX6  | Xtensa LX7 | Xtensa LX7 | RV32IMAC | RV32IMC     | RV32IMAC    | RV32IMAC    | RV32IMAC  | RV32IMAC    | RV32IMAFC  |
| **Cores**       | 1        | 1 or 2      | 1          | 2          | 1        | 1           | 1           | 1+LP        | 1         | 1           | 2+LP       |
| **Max Freq**    | 160 MHz  | 240 MHz     | 240 MHz    | 240 MHz    | 120 MHz  | 160 MHz     | 240 MHz     | 160 MHz     | 160 MHz   | 96 MHz      | 400 MHz    |
| **SRAM**        | 80 KiB   | 520 KiB     | 320 KiB    | 512 KiB    | 272 KiB  | 400 KiB     | 384 KiB     | 512 KiB     | 320 KiB   | 256 KiB     | 768 KiB    |
| **WiFi**        | b/g/n    | b/g/n       | b/g/n      | b/g/n      | b/g/n    | b/g/n       | b/g/n/ac/ax | b/g/n/ax    | b/g/n/ax  | --          | --         |
| **Bluetooth**   | --       | 4.2+BR/EDR  | --         | 5.0 LE     | 5.0 LE   | 5.0 LE      | 5.0 LE      | 5.3 LE      | 6.0 LE    | 5.3 LE      | --         |
| **802.15.4**    | --       | --          | --         | --         | --       | --          | Yes         | Yes         | --        | Yes         | --         |
| **GPIO**        | 16       | 34          | 43         | 45         | 20       | 22          | ~30         | 30          | 30        | 26          | 50         |
| **UART**        | 2        | 3           | 2          | 3          | 2        | 2           | 3           | 3           | --        | 2           | 2          |
| **SPI**         | 2        | 4           | 4          | 4          | 2        | 3           | 3           | 3           | --        | 2           | 3          |
| **I2C**         | 1        | 2           | 1          | 2          | 1        | 1           | 2           | 2           | --        | 2           | 2          |
| **I2S**         | 1        | 2           | 1          | 2          | --       | 1           | 1           | 1           | --        | 1           | 3          |
| **USB**         | --       | --          | OTG        | OTG+JTAG   | --       | Serial/JTAG | Serial/JTAG | Serial/JTAG | --        | Serial/JTAG | OTG 2.0 HS |
| **ADC**         | 1x10-bit | 2x12-bit    | 2x13-bit   | 2x12-bit   | 1x12-bit | 2x12-bit    | 1x12-bit    | 1x12-bit    | --        | 1x12-bit    | --         |
| **DAC**         | --       | 2x8-bit     | 2x8-bit    | --         | --       | --          | --          | --          | --        | --          | --         |
| **Ethernet**    | --       | MAC+RMII    | --         | --         | --       | --          | --          | --          | --        | --          | MAC+RMII   |
| **TWAI/CAN**    | --       | Yes         | --         | Yes        | --       | Yes         | --          | Yes         | --        | Yes         | Yes        |
| **Touch**       | --       | 10 ch       | 14 ch      | 14 ch      | --       | --          | --          | --          | --        | --          | --         |
| **Temp Sensor** | --       | --          | --         | Yes        | --       | Yes         | --          | Yes         | --        | Yes         | Yes        |
| **DMA**         | --       | PDMA        | GDMA       | GDMA       | GDMA     | GDMA        | GDMA        | GDMA+UHCI   | --        | GDMA        | GDMA       |
| **Crypto HW**   | --       | AES/SHA/RSA | Full       | Full       | AES/SHA  | Full        | Full        | Full        | --        | Full+ECC    | Full+ECC   |

Legend: "Full" crypto = AES + SHA + RSA + HMAC + Digital Signature + XTS-AES + RNG. "--" = not present/not applicable.

---

## 3. Peripheral Comparison Across ESP Families

### Wireless Connectivity Summary

| Chip      | WiFi Standard      | WiFi Bands    | Bluetooth      | 802.15.4 (Zigbee/Thread) | Matter Support    |
| --------- | ------------------ | ------------- | -------------- | ------------------------ | ----------------- |
| ESP8266   | 802.11 b/g/n       | 2.4 GHz       | --             | --                       | --                |
| ESP32     | 802.11 b/g/n       | 2.4 GHz       | 4.2 Classic+LE | --                       | Via WiFi only     |
| ESP32-S2  | 802.11 b/g/n       | 2.4 GHz       | --             | --                       | Via WiFi only     |
| ESP32-S3  | 802.11 b/g/n       | 2.4 GHz       | 5.0 LE         | --                       | Via WiFi or BLE   |
| ESP32-C2  | 802.11 b/g/n       | 2.4 GHz       | 5.0 LE         | --                       | Via WiFi or BLE   |
| ESP32-C3  | 802.11 b/g/n       | 2.4 GHz       | 5.0 LE         | --                       | Via WiFi or BLE   |
| ESP32-C5  | 802.11 b/g/n/ac/ax | **2.4+5 GHz** | 5.0 LE         | **Yes**                  | **WiFi + Thread** |
| ESP32-C6  | 802.11 b/g/n/ax    | 2.4 GHz       | 5.3 LE         | **Yes**                  | **WiFi + Thread** |
| ESP32-C61 | 802.11 b/g/n/ax    | 2.4 GHz       | 6.0 LE         | --                       | Via WiFi or BLE   |
| ESP32-H2  | --                 | --            | 5.3 LE         | **Yes**                  | **Thread only**   |
| ESP32-P4  | --                 | --            | --             | --                       | Via co-processor  |

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

| Chip      | ISA       | Extensions                                       | Notes                            |
| --------- | --------- | ------------------------------------------------ | -------------------------------- |
| ESP32-C2  | RV32IMAC  | Integer + Multiply + Atomic + Compressed         |                                  |
| ESP32-C3  | RV32IMC   | Integer + Multiply + Compressed                  | No Atomic extension              |
| ESP32-C5  | RV32IMAC  | Integer + Multiply + Atomic + Compressed         |                                  |
| ESP32-C6  | RV32IMAC  | Integer + Multiply + Atomic + Compressed         | HP core; LP core also RV32IMAC   |
| ESP32-C61 | RV32IMAC  | Integer + Multiply + Atomic + Compressed         |                                  |
| ESP32-H2  | RV32IMAC  | Integer + Multiply + Atomic + Compressed         |                                  |
| ESP32-P4  | RV32IMAFC | Integer + Multiply + Atomic + Float + Compressed | Only ESP with HW float in RISC-V |

