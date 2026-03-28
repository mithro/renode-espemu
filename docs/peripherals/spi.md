# ESP32 SPI

## Overview

The ESP32 contains four SPI controllers: SPI0, SPI1, SPI2 (HSPI), and SPI3 (VSPI). SPI0 and SPI1 are internally dedicated to flash memory and PSRAM access, while SPI2 and SPI3 are general-purpose SPI (GP-SPI) controllers available for user applications. SPI0 is the most critical peripheral on the chip as it provides the eXecute-In-Place (XIP) path for fetching instructions and data from external flash -- without a functioning SPI0 emulation, the CPU cannot boot.

SPI0 and SPI1 share the same bus signals but use different CS lines, with an arbiter controlling access between them. SPI1 is used by software for flash/PSRAM programming operations (erase, write) while SPI0 handles transparent cache-driven read access. SPI2 and SPI3 are fully independent controllers that can operate in master or slave mode, support DMA transfers via linked-list descriptors, and can drive up to 3 CS lines each.

## Hardware Specifications

| Feature | SPI0 | SPI1 | SPI2 (HSPI) | SPI3 (VSPI) |
|---|---|---|---|---|
| **Purpose** | Cache/Flash XIP | Flash/PSRAM programming | General-purpose | General-purpose |
| **User accessible** | No | Limited (flash API) | Yes | Yes |
| **Master mode** | Yes (fixed) | Yes (fixed) | Yes | Yes |
| **Slave mode** | No | No | Yes | Yes |
| **Max clock** | 80 MHz | 80 MHz | 80 MHz | 80 MHz |
| **Data width** | 1/2/4-bit (QIO/DIO) | 1/2/4-bit (QIO/DIO) | 1-bit standard | 1-bit standard |
| **FIFO size** | 64 bytes | 64 bytes | 64 bytes | 64 bytes |
| **DMA support** | No (cache-driven) | Yes (shared DMA) | Yes (DMA channel 1 or 2) | Yes (DMA channel 1 or 2) |
| **Base address** | 0x3FF43000 | 0x3FF42000 | 0x3FF64000 | 0x3FF65000 |

- **Clock modes**: Supports all 4 SPI modes (CPOL/CPHA combinations: mode 0, 1, 2, 3)
- **DMA**: Two DMA channels shared among SPI1/SPI2/SPI3. Uses linked-list descriptors for scatter-gather transfers. Maximum single DMA transfer is 4092 bytes per descriptor.
- **Buffer**: 64-byte (16 x 32-bit words) read/write buffer (W0-W15 registers)
- **CS timing**: Configurable setup time, hold time, and idle time for chip select signals
- **Flash commands**: SPI0/SPI1 support standard flash commands (read, fast read, dual/quad read, page program, sector erase, etc.)

## TRM Chapter Reference

**ESP32 Technical Reference Manual, Chapter 10: SPI Controller**

- https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf

Key sections:
- 10.1: Overview of the four SPI controllers and their roles
- 10.2: SPI0/SPI1 flash/PSRAM interface and command phases
- 10.3: GP-SPI (SPI2/SPI3) master and slave modes
- 10.4: SPI clock configuration and timing
- 10.5: SPI DMA operation and linked-list descriptors
- 10.6: Register descriptions

## Register Map Summary

The SPI register space is approximately 0x100 bytes per controller. Key registers include:

### Command and Control
| Register | Offset | Description |
|---|---|---|
| SPI_CMD_REG | 0x00 | Command register; set SPI_USR bit to start a transfer |
| SPI_CTRL_REG | 0x08 | Control register: read mode (QIO/DIO/QOUT/DOUT/fast/slow) |
| SPI_CTRL2_REG | 0x14 | Setup/hold time, MISO delay configuration |
| SPI_USER_REG | 0x1C | User-defined command phases (cmd/addr/dummy/data enable bits) |
| SPI_USER1_REG | 0x20 | Address bit length, dummy cycle count |
| SPI_USER2_REG | 0x24 | Command value and command bit length |

### Clock Configuration
| Register | Offset | Description |
|---|---|---|
| SPI_CLOCK_REG | 0x18 | Clock divider (pre-divider, count-N, count-H, count-L) |

### Data Buffers
| Register | Offset | Description |
|---|---|---|
| SPI_W0_REG - SPI_W15_REG | 0x80 - 0xBC | 16 x 32-bit data buffer words (64 bytes total) |

### DMA Registers
| Register | Offset | Description |
|---|---|---|
| SPI_DMA_CONF_REG | 0x100 | DMA configuration (TX/RX enable, reset) |
| SPI_DMA_OUT_LINK_REG | 0x104 | TX linked-list descriptor start address |
| SPI_DMA_IN_LINK_REG | 0x108 | RX linked-list descriptor start address |
| SPI_DMA_STATUS_REG | 0x10C | DMA status register |

### Status and Interrupts
| Register | Offset | Description |
|---|---|---|
| SPI_SLAVE_REG | 0x30 | Slave mode config and transfer-done status/interrupt |
| SPI_SLV_WRBUF_DLEN_REG | 0x34 | Slave write buffer length |
| SPI_SLV_RDBUF_DLEN_REG | 0x38 | Slave read buffer length |

### Flash-Specific (SPI0/SPI1)
| Register | Offset | Description |
|---|---|---|
| SPI_ADDR_REG | 0x04 | Flash address for read/write/erase commands |
| SPI_MOSI_DLEN_REG | 0x28 | MOSI data bit length |
| SPI_MISO_DLEN_REG | 0x2C | MISO data bit length |

## Source Code References

### SOC Register Definitions
- **Register header**: https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/spi_reg.h
- **Register struct**: https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/spi_struct.h

### HAL Layer
- **GP-SPI HAL LL**: https://github.com/espressif/esp-idf/blob/master/components/esp_hal_gpspi/esp32/include/hal/spi_ll.h
- **Flash HAL LL**: https://github.com/espressif/esp-idf/blob/master/components/esp_hal_mspi/esp32/include/hal/spi_flash_ll.h

### API Documentation
- **SPI Master API**: https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-reference/peripherals/spi_master.html
- **SPI Slave API**: https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-reference/peripherals/spi_slave.html

### Examples
- **SPI Master example**: https://github.com/espressif/esp-idf/blob/master/examples/peripherals/spi_master

### QEMU Implementation
- **ESP32 SPI in QEMU**: https://github.com/espressif/qemu/blob/esp-develop/hw/ssi/esp32_spi.c

## Renode Implementation Analysis

### Existing Renode Models
- **STM32 SPI reference**: https://github.com/renode/renode-infrastructure/blob/master/src/Emulator/Peripherals/Peripherals/SPI/STM32SPI.cs

The STM32SPI model provides a useful reference for GP-SPI implementation. It demonstrates the standard Renode `ISPIPeripheral` interface pattern, register-based transfer initiation, and interrupt generation.

### Implementation Approach

SPI emulation for the ESP32 in Renode should be split into two distinct components due to the fundamentally different roles of SPI0/SPI1 vs SPI2/SPI3:

**Component 1: SPI Flash Controller (SPI0/SPI1) -- CRITICAL PRIORITY**

This is the highest-priority SPI component because it enables flash XIP boot:
1. SPI0 must respond to cache read requests transparently, mapping flash contents into the CPU address space (0x3F400000-0x3F7FFFFF for data, 0x400C2000+ for instructions via cache).
2. SPI1 must support flash programming commands (page program, sector erase, read status register) used by the flash API and bootloader.
3. The arbiter between SPI0 and SPI1 can likely be simplified since timing contention is not relevant in emulation.
4. Flash read modes (QIO, DIO, QOUT, DOUT, fast read) can be abstracted since the emulated flash model does not need actual multi-bit signaling -- all reads return data identically regardless of mode.
5. The QEMU implementation (`esp32_spi.c`) is a valuable reference for which registers and command sequences are essential for flash boot.

**Component 2: GP-SPI (SPI2/SPI3) -- MEDIUM PRIORITY**

GP-SPI follows a more standard SPI controller pattern:
1. Implement the `ISPIPeripheral` Renode interface for connecting external SPI devices (sensors, displays, SD cards via SPI mode, etc.).
2. Support the user-defined transfer phase model: CMD phase, ADDR phase, DUMMY phase, DATA phase (each individually enable-able).
3. Implement the 64-byte buffer (W0-W15) for non-DMA transfers.
4. DMA support via linked-list descriptors for larger transfers. Read descriptors from memory, chain through the linked list, transfer data to/from device.
5. Interrupt generation on transfer completion.
6. Slave mode is lower priority and can be deferred.

**Key implementation considerations:**
- SPI0 flash access is the boot-critical path; without it, no firmware can execute
- The 64-byte buffer and W0-W15 data registers are the primary data path for non-DMA transfers
- DMA linked-list descriptor parsing requires reading from system memory
- Clock configuration registers can be largely ignored (emulation does not model real timing)
- CS timing registers can be stubbed

## Complexity Assessment

| Component | Complexity | Priority | Rationale |
|---|---|---|---|
| **SPI0 Flash XIP** | HIGH | CRITICAL | Required for boot. Must integrate with cache/MMU and flash memory model. Complex interaction between cache requests and SPI flash commands. |
| **SPI1 Flash Programming** | MEDIUM | HIGH | Required for flash write/erase operations. Bootloader and OTA updates depend on this. Command set is well-defined. |
| **SPI0/SPI1 Arbiter** | LOW | HIGH | Can be simplified in emulation since there is no real bus contention. |
| **SPI2/SPI3 GP-SPI Master** | MEDIUM | MEDIUM | Standard SPI master with buffer and DMA. Phase-based transfer model adds some complexity. |
| **SPI2/SPI3 GP-SPI Slave** | MEDIUM | LOW | Rarely needed in typical emulation scenarios. |
| **SPI DMA Engine** | MEDIUM-HIGH | MEDIUM | Linked-list descriptor parsing, scatter-gather, shared DMA channels. Required for efficient large transfers. |

**Overall SPI complexity: HIGH**

The SPI subsystem is one of the most complex peripherals to emulate due to the dual nature of flash-access SPI vs general-purpose SPI, the tight coupling with the cache/MMU for XIP, and the DMA linked-list engine. However, the QEMU implementation provides a proven reference for the flash SPI path, which significantly de-risks the critical boot path.

**Estimated register count**: ~60 registers per instance, ~240 total across all 4 instances (though SPI0 and SPI1 share most register definitions).
