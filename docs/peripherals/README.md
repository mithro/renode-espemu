# ESP32 Peripheral Analysis for Renode Emulation

Detailed analysis of every ESP32 peripheral, ordered from simplest to most complex for implementation in Renode.

Each document covers hardware specifications, register maps, source code references (all verified HTTP 200), QEMU reference implementations, Renode reference models, and implementation guidance.

> **See also:**
> - [Emulation Platform Status](../02-emulation-platform-status.md) — what Renode supports today
> - [Gap Analysis and Roadmap](../05-gap-analysis-and-roadmap.md) — prioritised implementation plan
> - [Development Methodology](../04-development-methodology.md) — JTAG and coverage-guided development approach
> - [Document Index](../README.md)

---

## Peripheral Index (Simplest to Most Complex)

### Tier 1: Simple (< 1 day each)

| #   | Peripheral                                  | Registers | QEMU | Renode | Boot Critical | Doc                                       |
| --- | ------------------------------------------- | --------- | ---- | ------ | ------------- | ----------------------------------------- |
| 1   | [RNG](rng.md)                               | 1         | Yes  | No     | No            | Random number generator (single register) |
| 2   | [Temperature Sensor](temperature-sensor.md) | 3-4       | No   | No     | No            | SAR ADC TSENS (rarely used on ESP32)      |
| 3   | [DAC](dac.md)                               | ~5        | No   | No     | No            | 2x 8-bit DAC channels                     |

### Tier 2: Medium-Simple (1-2 days each)

| #   | Peripheral              | Registers | QEMU | Renode  | Boot Critical | Doc                                        |
| --- | ----------------------- | --------- | ---- | ------- | ------------- | ------------------------------------------ |
| 4   | [eFuse](efuse.md)       | ~50       | Yes  | No      | **Yes**       | MAC address, chip revision (read at boot)  |
| 5   | [UART](uart.md)         | ~30       | Yes  | **Yes** | **Yes**       | 3 instances; Renode has ESP32_UART.cs      |
| 6   | [PCNT](pcnt.md)         | ~40       | No   | No      | No            | 8 pulse counter units                      |
| 7   | [Watchdog](watchdog.md) | ~20       | Yes  | No      | **Yes**       | MWDT + RWDT (must feed or disable at boot) |
| 8   | [TWAI/CAN](twai.md)     | ~32       | Yes  | No      | No            | SJA1000-compatible CAN 2.0B                |

### Tier 3: Medium (2-4 days each)

| #   | Peripheral                        | Registers | QEMU | Renode | Boot Critical | Doc                                        |
| --- | --------------------------------- | --------- | ---- | ------ | ------------- | ------------------------------------------ |
| 9   | [LEDC](ledc.md)                   | ~60       | Yes  | No     | No            | 16-channel LED PWM controller              |
| 10  | [Timer Groups](timer-group.md)    | ~40       | Yes  | No     | **Yes**       | 2 groups, 2x 64-bit timers (FreeRTOS tick) |
| 11  | [ADC](adc.md)                     | ~40       | No   | No     | No            | 2x 12-bit SAR ADC, 18 channels             |
| 12  | [Touch Sensor](touch-sensor.md)   | ~30       | No   | No     | No            | 10 capacitive touch channels               |
| 13  | [I2C](i2c.md)                     | ~40       | Yes  | No     | No            | 2 instances, command-queue architecture    |
| 14  | [RMT](rmt.md)                     | ~50       | No   | No     | No            | 8-channel pulse encoder/decoder            |
| 15  | [Crypto (AES/SHA/RSA)](crypto.md) | ~30       | Yes  | No     | No            | Hardware crypto accelerators               |

### Tier 4: Medium-Complex (3-5 days each)

| #   | Peripheral                              | Registers | QEMU    | Renode | Boot Critical | Doc                                   |
| --- | --------------------------------------- | --------- | ------- | ------ | ------------- | ------------------------------------- |
| 16  | [Interrupt Matrix](interrupt-matrix.md) | ~70       | Yes     | No     | **Yes**       | 71 sources → 26 CPU lines (per core)  |
| 17  | [Clock Control](clock-control.md)       | ~50       | Partial | No     | **Yes**       | PLL, CPU freq, peripheral clocks      |
| 18  | [SPI](spi.md)                           | ~60       | Yes     | No     | **Yes**       | 4 instances (SPI0/1 flash, SPI2/3 GP) |
| 19  | [Ethernet MAC](ethernet-mac.md)         | ~80       | No      | No     | No            | Synopsys DW MAC, RMII, internal DMA   |
| 20  | [SDMMC](sdmmc.md)                       | ~45       | Partial | No     | No            | SD/SDIO/MMC host with IDMAC           |
| 21  | [DMA (PDMA)](dma.md)                    | Varies    | Partial | No     | No            | Per-peripheral linked-list DMA        |
| 22  | [I2S](i2s.md)                           | ~50       | No      | No     | No            | 2 instances (audio + camera DVP)      |

### Tier 5: Complex (5-10 days each)

| #   | Peripheral                        | Registers | QEMU | Renode | Boot Critical | Doc                                      |
| --- | --------------------------------- | --------- | ---- | ------ | ------------- | ---------------------------------------- |
| 23  | [RTC Controller](rtc.md)          | ~100      | Yes  | No     | **Yes**       | Clocks, resets, power domains, brownout  |
| 24  | [DPORT](dport.md)                 | ~200      | Yes  | No     | **Yes**       | System regs, clock gating, cache config  |
| 25  | [GPIO + IO MUX + Matrix](gpio.md) | ~350      | Yes  | No     | **Yes**       | 34 pins, GPIO Matrix, IO MUX, interrupts |
| 26  | [MCPWM](mcpwm.md)                 | ~180      | No   | No     | No            | 2 units, 3 pairs PWM + capture + fault   |
| 27  | [Cache / MMU](cache-mmu.md)       | ~50       | Yes  | No     | **Yes**       | Flash XIP, PSRAM mapping, page tables    |

---

## Summary Statistics

| Metric                     | Value    |
| -------------------------- | -------- |
| Total peripherals          | 27       |
| Boot-critical              | 11       |
| With QEMU reference        | 17       |
| With existing Renode model | 1 (UART) |
| Simple (< 1 day)           | 3        |
| Medium (1-4 days)          | 12       |
| Medium-Complex (3-5 days)  | 7        |
| Complex (5-10 days)        | 5        |

## Recommended Implementation Order

For getting ESP-IDF `hello_world` to boot in Renode, implement in this order:

1. **eFuse** (#4) — firmware reads MAC and chip revision at boot
2. **Timer Groups** (#10) — FreeRTOS tick source
3. **Watchdog** (#7) — must be fed/disabled or firmware resets
4. **Interrupt Matrix** (#16) — all interrupts route through this
5. **DPORT** (#24) — peripheral clock gating, reset control
6. **RTC Controller** (#23) — clock config, reset reason
7. **Clock Control** (#17) — CPU and peripheral clock setup
8. **Cache/MMU** (#27) — flash XIP (execute-in-place)
9. **SPI** (#18) — flash access (SPI0/1)
10. **GPIO** (#25) — boot strapping, pin muxing for UART/SPI

UART (#5) already has a Renode model. RNG (#1) is trivial to add alongside any of the above.

## QEMU as Reference

17 of 27 peripherals have QEMU implementations in the [Espressif QEMU fork](https://github.com/espressif/qemu/tree/esp-develop). These provide register-level reference implementations that can guide Renode C# model development:

| QEMU File                    | Peripherals Covered |
| ---------------------------- | ------------------- |
| `hw/char/esp32_uart.c`       | UART                |
| `hw/gpio/esp32_gpio.c`       | GPIO                |
| `hw/ssi/esp32_spi.c`         | SPI (flash + GP)    |
| `hw/i2c/esp32_i2c.c`         | I2C                 |
| `hw/timer/esp32_timg.c`      | Timer Groups + WDT  |
| `hw/timer/esp32_frc_timer.c` | FRC Timer           |
| `hw/nvram/esp32_efuse.c`     | eFuse               |
| `hw/misc/esp32_aes.c`        | AES accelerator     |
| `hw/misc/esp32_sha.c`        | SHA accelerator     |
| `hw/misc/esp32_rsa.c`        | RSA/MPI             |
| `hw/misc/esp32_rng.c`        | RNG                 |
| `hw/misc/esp32_dport.c`      | DPORT               |
| `hw/misc/esp32_rtc_cntl.c`   | RTC Controller      |
| `hw/misc/esp32_ledc.c`       | LEDC                |
| `hw/misc/esp32_flash_enc.c`  | Flash Encryption    |
| `hw/xtensa/esp32_intc.c`     | Interrupt Matrix    |
| `hw/net/can/esp32_twai.c`    | TWAI/CAN            |
