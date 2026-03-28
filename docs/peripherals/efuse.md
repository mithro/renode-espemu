# ESP32 eFuse Controller

## Overview

The ESP32 eFuse controller manages 1024 bits of one-time-programmable (OTP) memory organized into four 256-bit blocks (BLK0 through BLK3). eFuses are non-volatile and, once programmed (blown), cannot be reverted. They store critical system configuration data including the chip's unique MAC address, chip revision, flash encryption keys, secure boot keys, and various hardware configuration bits that control security features and operating modes.

The eFuse controller is one of the most critical peripherals for ESP32 emulation because firmware reads eFuse data very early in the boot process. The ROM bootloader reads the MAC address from eFuse BLK0 during initialization, and ESP-IDF reads chip revision information, flash encryption status, and secure boot configuration from eFuses before proceeding with application startup. Without proper eFuse values, the firmware will either fail to boot, report incorrect hardware information, or behave unexpectedly.

Block 0 (BLK0) contains system parameters: MAC address (48 bits), chip revision, SPI flash configuration, write/read protection bits for all blocks, and security configuration (flash encryption enable, JTAG disable, secure boot, etc.). Block 1 (BLK1) is used for flash encryption keys. Block 2 (BLK2) is used for secure boot keys. Block 3 (BLK3) can store a custom MAC address or be used for user data. For emulation, Block 0 is essential (MAC address and chip revision), while Blocks 1-3 can typically be left at their default (all zeros) state unless flash encryption or secure boot is being emulated.

## Hardware Specifications

- **Register base address:** `0x3FF5A000` (size: `0x1000`)
- **Number of instances:** 1
- **eFuse capacity:** 1024 bits total (4 blocks x 256 bits)
  - BLK0 (256 bits): System parameters, MAC address, configuration
  - BLK1 (256 bits): Flash encryption key
  - BLK2 (256 bits): Secure boot key
  - BLK3 (256 bits): User/custom MAC
- **Key capabilities:**
  - Read: All eFuse bits can be read via registers at any time
  - Program: Bits can be programmed (0->1) but never cleared (1->0)
  - Write protection: Individual blocks can be write-protected via WR_DIS bits in BLK0
  - Read protection: BLK1, BLK2, BLK3 can be read-protected (reads return 0) via RD_DIS bits in BLK0
  - Programming requires specific timing and voltage sequences
- **Interrupt sources:** None
- **DMA support:** None

## TRM Chapter Reference

- **ESP32 Technical Reference Manual** Chapter 20: eFuse Controller
  - [PDF link](https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf)

## Register Map Summary

### eFuse Data Registers (read-only view of programmed bits)

| Offset        | Register                  | Purpose                                                          |
| ------------- | ------------------------- | ---------------------------------------------------------------- |
| `0x000`       | `EFUSE_BLK0_RDATA0_REG`   | BLK0 word 0: WR_DIS (write disable bits)                         |
| `0x004`       | `EFUSE_BLK0_RDATA1_REG`   | BLK0 word 1: RD_DIS, coding scheme, various config bits          |
| `0x008`       | `EFUSE_BLK0_RDATA2_REG`   | BLK0 word 2: WIFI_MAC_Address (bytes 0-3, low 32 bits of MAC)    |
| `0x00C`       | `EFUSE_BLK0_RDATA3_REG`   | BLK0 word 3: WIFI_MAC_Address (bytes 4-5) + chip revision + misc |
| `0x010`       | `EFUSE_BLK0_RDATA4_REG`   | BLK0 word 4: SPI pad config, CK8M frequency, XPD_SDIO            |
| `0x014`       | `EFUSE_BLK0_RDATA5_REG`   | BLK0 word 5: SPI pad config continued, flash encryption count    |
| `0x018`       | `EFUSE_BLK0_RDATA6_REG`   | BLK0 word 6: Key status, coding scheme, console debug, security  |
| `0x01C`       | `EFUSE_BLK0_RDATA7_REG`   | BLK0 word 7: Reserved / additional config                        |
| `0x020-0x03C` | `EFUSE_BLK1_RDATA0-7_REG` | BLK1 (8 words): Flash encryption key                             |
| `0x040-0x05C` | `EFUSE_BLK2_RDATA0-7_REG` | BLK2 (8 words): Secure boot key                                  |
| `0x060-0x07C` | `EFUSE_BLK3_RDATA0-7_REG` | BLK3 (8 words): User data / custom MAC                           |

### eFuse Programming Registers

| Offset        | Register                  | Purpose                               |
| ------------- | ------------------------- | ------------------------------------- |
| `0x098-0x0B4` | `EFUSE_BLK0_WDATA0-7_REG` | Write data for BLK0 (bits to program) |
| `0x0B8-0x0D4` | `EFUSE_BLK1_WDATA0-7_REG` | Write data for BLK1                   |
| `0x0D8-0x0F4` | `EFUSE_BLK2_WDATA0-7_REG` | Write data for BLK2                   |
| `0x0F8-0x114` | `EFUSE_BLK3_WDATA0-7_REG` | Write data for BLK3                   |

### eFuse Control Registers

| Offset  | Register               | Purpose                                                         |
| ------- | ---------------------- | --------------------------------------------------------------- |
| `0x118` | `EFUSE_CLK_REG`        | eFuse clock configuration                                       |
| `0x11C` | `EFUSE_CONF_REG`       | eFuse operation configuration (read/program command)            |
| `0x120` | `EFUSE_STATUS_REG`     | eFuse status (busy/idle)                                        |
| `0x124` | `EFUSE_CMD_REG`        | Command register: trigger read (0x1) or program (0x2) operation |
| `0x128` | `EFUSE_INT_RAW_REG`    | Raw interrupt status (program done, read done)                  |
| `0x12C` | `EFUSE_INT_ST_REG`     | Masked interrupt status                                         |
| `0x130` | `EFUSE_INT_ENA_REG`    | Interrupt enable                                                |
| `0x134` | `EFUSE_INT_CLR_REG`    | Interrupt clear                                                 |
| `0x138` | `EFUSE_DAC_CONF_REG`   | DAC configuration for programming voltage                       |
| `0x13C` | `EFUSE_DEC_STATUS_REG` | Decoding status (for 3/4 coding scheme)                         |
| `0x1FC` | `EFUSE_DATE_REG`       | Version/date register                                           |

## Source Code References

### ESP-IDF Register Definitions
- [`soc/efuse_reg.h`](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/efuse_reg.h) -- Register addresses, bit field masks, and definitions for all eFuse registers
- [`soc/efuse_struct.h`](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/efuse_struct.h) -- C struct overlay for eFuse registers

### ESP-IDF HAL (Low-Level Driver)
- [`hal/efuse_ll.h`](https://github.com/espressif/esp-idf/blob/master/components/hal/esp32/include/hal/efuse_ll.h) -- Shows exactly how eFuse registers are read and programmed: reading data registers, writing to program registers, issuing read/program commands, checking status

### ESP-IDF API Documentation
- [eFuse API Reference](https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-reference/system/efuse.html)

### ESP-IDF Examples

There are no dedicated eFuse examples in the standard examples directory. eFuse usage is primarily internal to ESP-IDF system components. The `espefuse.py` tool (part of esptool) is used to read/program eFuses on real hardware.

### Espressif QEMU Implementation
- [`hw/nvram/esp32_efuse.c`](https://github.com/espressif/qemu/blob/esp-develop/hw/nvram/esp32_efuse.c) -- QEMU's eFuse model

QEMU's implementation models the complete eFuse register interface including:
- All four data blocks (BLK0-BLK3) readable via RDATA registers
- Write data registers for programming
- Command register for triggering read/program operations
- Status register indicating busy/idle
- eFuse data is stored in a backing store (file or memory block) that persists across resets
- Programming operations OR new bits into the existing eFuse values (simulating the one-time-programmable behavior)
- Initial eFuse values can be loaded from a file, allowing configuration of MAC address and other parameters
- Read protection is modeled (protected blocks return 0 on read)

Limitations: The DAC programming voltage configuration is not meaningfully modeled. Timing of programming operations is instant. Coding scheme error detection is simplified.

## Renode Implementation Analysis

### Existing Renode Model

No ESP32 eFuse model exists in the main Renode repository.

### Recommended Renode Reference Peripherals

There is no directly analogous peripheral in Renode's existing library. However:

- Renode's `BasicDoubleWordPeripheral` with `DoubleWordRegisterCollection` provides the register framework needed
- For persistence of eFuse values across resets, Renode's `IPeripheralWithState` or similar save/restore interface can be used
- OTP/fuse controllers in other SoCs modeled by Renode (if any) would be the most relevant reference

### Implementation Approach

**Architecture:**
The eFuse controller should be implemented as a single peripheral class that manages the 1024-bit fuse array and exposes it through the register interface.

**Internal state:**
- A 1024-bit array (or 32 x 32-bit words) representing the programmed eFuse values
- Write protection flags (from BLK0 WR_DIS)
- Read protection flags (from BLK0 RD_DIS)

**Registers that MUST be implemented for basic functionality:**
- `EFUSE_BLK0_RDATA0-7_REG` -- BLK0 read data (MAC address, chip revision, config)
- `EFUSE_BLK1_RDATA0-7_REG` through `EFUSE_BLK3_RDATA0-7_REG` -- can return 0 initially
- `EFUSE_STATUS_REG` -- must report "idle" (not busy) so firmware does not hang waiting
- `EFUSE_CMD_REG` -- accepting read command (trigger re-read of fuse data into RDATA registers)

**Critical BLK0 fields that must have correct values:**
- MAC address (RDATA2/RDATA3): Firmware reads this at boot. A valid MAC is 6 bytes; providing a sensible default like `AA:BB:CC:DD:EE:FF` or `24:0A:C4:XX:XX:XX` (Espressif OUI) is important.
- Chip revision (bits in RDATA3/RDATA5): ESP-IDF reads `EFUSE_RD_CHIP_VER_REV1` and `EFUSE_RD_CHIP_VER_REV2` to determine chip silicon revision. Revision 1 or 3 are common values.
- CHIP_VER_PKG (RDATA3): Chip package type, affects pin availability.
- Flash encryption count (RDATA5): Should be 0 (flash encryption disabled) for normal emulation.
- Coding scheme (RDATA6): Should be 0 (no coding) for simplest emulation.

**Registers that can be stubbed initially:**
- All WDATA (write/program) registers -- accept writes but no need to actually program
- `EFUSE_CLK_REG`, `EFUSE_CONF_REG`, `EFUSE_DAC_CONF_REG` -- store value but no side effects
- `EFUSE_INT_*` registers -- can be stubbed (interrupts are optional)
- `EFUSE_DEC_STATUS_REG` -- return 0 (no decoding errors)

**Interrupts that need to work:**
- None are strictly required. The program-done and read-done interrupts exist but firmware typically polls the status register rather than using interrupts for eFuse operations.

**DMA considerations:**
- None -- eFuse has no DMA.

**Configuration:**
The eFuse controller should support loading initial eFuse values from a configuration file or Renode platform script, allowing users to set:
- MAC address
- Chip revision
- Package type
- Flash encryption / secure boot status

**Estimated complexity:** Medium (moderate register count, need correct default values, no complex state machines)

### Key Firmware Interactions

**During boot (ROM bootloader):**
1. ROM reads `EFUSE_BLK0_RDATA2_REG` and `EFUSE_BLK0_RDATA3_REG` to get the MAC address
2. ROM reads chip revision bits to apply silicon-revision-specific workarounds
3. ROM reads secure boot configuration to determine boot verification mode
4. ROM reads flash encryption count to determine if flash decryption is needed
5. ROM reads SPI pad configuration to determine flash connection (affects SPI pin routing)

**During second-stage bootloader:**
1. Bootloader reads chip revision for compatibility checks
2. Bootloader logs MAC address
3. Bootloader checks flash encryption and secure boot status

**During ESP-IDF startup:**
1. `esp_efuse_mac_get_default()` reads MAC address -- used for WiFi, BT, Ethernet MAC configuration
2. `esp_chip_info()` reads chip revision, number of cores, features from eFuse
3. Various driver initializations read eFuse for calibration data
4. ESP-IDF logs "MAC: xx:xx:xx:xx:xx:xx" early in boot

**Critical register accesses that MUST succeed:**
- `EFUSE_BLK0_RDATA2_REG` and `EFUSE_BLK0_RDATA3_REG` must return a valid MAC address (firmware may assert on all-zeros MAC)
- `EFUSE_STATUS_REG` must report not-busy (firmware waits for eFuse operations to complete)
- Chip revision fields must return a recognized value (unknown revisions may cause assertion failures or incorrect behavior in ESP-IDF)

## Complexity Assessment

- **Estimated difficulty:** Medium
- **Estimated register count:** ~50 registers (32 RDATA + 32 WDATA + ~10 control/status)
- **Dependencies:** None (eFuse is standalone, though its values affect many other peripherals)
- **Priority:** Critical for boot -- MAC address is read from eFuse during ROM bootloader execution, chip revision affects many code paths in ESP-IDF. Without correct eFuse values, boot will fail or produce incorrect system identification.
