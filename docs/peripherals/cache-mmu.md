# ESP32 Cache and MMU

## Overview

The ESP32 has a sophisticated cache and memory management unit (MMU) system that enables Execute-In-Place (XIP) from external flash and data access to external PSRAM. Each CPU core (PRO_CPU and APP_CPU) has its own independent cache subsystem. The MMU translates virtual addresses in the CPU's address space to physical addresses in external flash or PSRAM, while the cache reduces the latency of repeated accesses by storing recently used data in fast internal SRAM.

This subsystem is **critical** for ESP32 operation because nearly all application code executes directly from flash via the cache/MMU (XIP). The bootloader configures the MMU early in the boot process, and the operating system relies on it continuously. Without a working cache/MMU model, no meaningful firmware execution is possible beyond the ROM bootloader.

## Hardware Specifications

### Cache
- **PRO CPU Cache**: 32 KB instruction cache + 32 KB data cache (from internal SRAM pool)
- **APP CPU Cache**: 32 KB instruction cache + 32 KB data cache (from internal SRAM pool)
- **Cache line size**: 32 bytes
- **Cache organization**: 4-way set associative (instruction cache), direct-mapped (data cache for PSRAM)
- **Instruction cache address range**: 0x400C_0000 - 0x40BF_FFFF (mapped to flash, up to ~11 MB window)
- **Data cache (flash) address range**: 0x3F40_0000 - 0x3F7F_FFFF (4 MB window for flash data)
- **Data cache (PSRAM) address range**: 0x3F80_0000 - 0x3FBF_FFFF (4 MB window for PSRAM)
- **Cache can be frozen/disabled** per CPU for low-power modes

### MMU
- **Page sizes**: 64 KB (configurable; 64 KB is the standard setting)
- **MMU entries**:
  - Instruction MMU: 64 entries per CPU for flash mapping (covers 4 MB per CPU)
  - Data MMU: 64 entries for flash data mapping
  - PSRAM MMU: 64 entries for PSRAM mapping
- **MMU entry format**: Each entry is a 32-bit value where:
  - Bits [7:0]: Physical page number in flash/PSRAM (identifies which 64 KB page)
  - Bit [8]: Valid bit (entry is active when set)
  - Upper bits: Reserved
- **Address translation**: Virtual address = base + (MMU_entry_index * 64KB), Physical address = page_number * 64KB
- **Flash mapping**: Maps SPI flash contents into the instruction and data address spaces
- **PSRAM mapping**: Maps external PSRAM into the data address space

### Memory Map (cache-related regions)

| Address Range             | Size   | Description                       |
| ------------------------- | ------ | --------------------------------- |
| 0x3F40_0000 - 0x3F7F_FFFF | 4 MB   | External flash data (via cache)   |
| 0x3F80_0000 - 0x3FBF_FFFF | 4 MB   | External PSRAM data (via cache)   |
| 0x400C_0000 - 0x40BF_FFFF | ~11 MB | External flash instructions (XIP) |

## TRM Chapter Reference

- **Chapter 9**: Cache and MMU
  - Section 9.1: Overview of cache and MMU architecture
  - Section 9.2: Cache operation modes
  - Section 9.3: MMU configuration
  - Section 9.4: Cache flush and invalidation
  - Section 9.5: Memory map and address translation

TRM PDF: https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf

## Register Map Summary

Cache and MMU registers are accessed through the DPORT register block (base 0x3FF00000).

### Cache Control Registers

| Register                        | Offset | Description                                      |
| ------------------------------- | ------ | ------------------------------------------------ |
| DPORT_PRO_CACHE_CTRL_REG        | 0x040  | PRO CPU cache enable, mode, size config          |
| DPORT_PRO_CACHE_CTRL1_REG       | 0x044  | PRO CPU cache mask (which address ranges cached) |
| DPORT_APP_CACHE_CTRL_REG        | 0x058  | APP CPU cache enable, mode, size config          |
| DPORT_APP_CACHE_CTRL1_REG       | 0x05C  | APP CPU cache mask                               |
| DPORT_PRO_CACHE_LOCK_0_ADDR_REG | 0x048  | PRO CPU cache lock region 0 address              |
| DPORT_PRO_CACHE_LOCK_1_ADDR_REG | 0x04C  | PRO CPU cache lock region 1 address              |
| DPORT_PRO_CACHE_LOCK_2_ADDR_REG | 0x050  | PRO CPU cache lock region 2 address              |
| DPORT_PRO_CACHE_LOCK_3_ADDR_REG | 0x054  | PRO CPU cache lock region 3 address              |
| DPORT_APP_CACHE_LOCK_0_ADDR_REG | 0x060  | APP CPU cache lock region 0 address              |

### MMU Table Registers

| Register / Region                   | Address Range           | Description          |
| ----------------------------------- | ----------------------- | -------------------- |
| PRO CPU Flash Instruction MMU Table | 0x3FF10000 - 0x3FF100FF | 64 entries x 4 bytes |
| APP CPU Flash Instruction MMU Table | 0x3FF12000 - 0x3FF120FF | 64 entries x 4 bytes |
| Flash Data MMU Table                | 0x3FF14000 - 0x3FF140FF | 64 entries x 4 bytes |
| PSRAM Data MMU Table (PRO)          | 0x3FF16000 - 0x3FF160FF | 64 entries x 4 bytes |

### Cache Status and Control

| Register                   | Offset | Description                           |
| -------------------------- | ------ | ------------------------------------- |
| DPORT_CACHE_IA_INT_EN_REG  | 0x1A0  | Cache illegal access interrupt enable |
| DPORT_CACHE_IA_INT_REG     | 0x1A4  | Cache illegal access interrupt status |
| DPORT_PRO_DCACHE_DBUG0_REG | 0x398  | PRO CPU data cache debug register 0   |
| DPORT_PRO_DCACHE_DBUG1_REG | 0x39C  | PRO CPU data cache debug register 1   |
| DPORT_APP_DCACHE_DBUG0_REG | 0x3B8  | APP CPU data cache debug register 0   |

## Source Code References

### HAL Layer
- **MMU low-level operations**: https://github.com/espressif/esp-idf/blob/master/components/hal/esp32/include/hal/mmu_ll.h
  - `mmu_ll_set_entry()` - Write an MMU table entry
  - `mmu_ll_get_entry()` - Read an MMU table entry
  - `mmu_ll_unset_entry()` - Invalidate an MMU entry
  - `mmu_ll_check_entry_valid()` - Check if entry is valid
- **Cache low-level operations**: https://github.com/espressif/esp-idf/blob/master/components/hal/esp32/include/hal/cache_ll.h
  - `cache_ll_l1_enable_bus()` - Enable cache bus for address range
  - `cache_ll_l1_disable_bus()` - Disable cache bus
  - `cache_ll_l1_is_cache_enabled()` - Check if cache is active

### SOC Register Definitions
- **DPORT registers (includes MMU/cache)**: https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/dport_reg.h

### QEMU Implementation
- **DPORT (includes MMU/cache model)**: https://github.com/espressif/qemu/blob/esp-develop/hw/misc/esp32_dport.c
  - Implements MMU table read/write
  - Handles cache enable/disable
  - Maps flash content into virtual address space
- **Flash encryption integration**: https://github.com/espressif/qemu/blob/esp-develop/hw/misc/esp32_flash_enc.c
  - Flash encryption interacts with cache/MMU since encrypted flash must be decrypted on the fly during XIP

## Renode Implementation Analysis

### Reference Peripherals in Renode
- Renode does not have a direct ESP32 cache/MMU equivalent, but the concept of memory mapping and address translation exists in:
  - `MappedMemory` for simple memory regions
  - `BusMultiRegistration` for registering peripherals at multiple address ranges
  - Various SoC models that implement MMU-like address translation

### Implementation Approach

The cache/MMU is one of the most complex subsystems to implement correctly. The recommended approach for Renode:

1. **MMU Table Model**:
   - Implement the MMU tables as arrays of 32-bit entries within the DPORT peripheral model
   - Support read/write access to MMU table entries at their mapped addresses (0x3FF1xxxx)
   - Parse entry format: extract page number and valid bit
   - Maintain a mapping from virtual page to physical flash/PSRAM offset

2. **Address Translation Layer**:
   - Register hook/mapping on the virtual address ranges (0x3F40_0000-0x3FBF_FFFF for data, 0x400C_0000-0x40BF_FFFF for instructions)
   - On access, look up the MMU table entry for the corresponding 64 KB page
   - If valid, translate to physical offset and read from the flash image or PSRAM backing store
   - If invalid, generate a cache illegal access exception

3. **Cache Behavior (Simplified)**:
   - For emulation purposes, the actual cache (hit/miss behavior, line fill, eviction) can likely be **omitted** or heavily simplified
   - The critical behavior is the address translation (MMU), not the caching layer itself
   - Cache enable/disable registers must be tracked since firmware checks them
   - Cache flush/invalidate operations can be no-ops in emulation (since we read directly from the flash image)

4. **Flash Integration**:
   - The MMU maps into the SPI flash image file loaded into the emulator
   - Physical page number N corresponds to offset (N * 64KB) in the flash image
   - Must support flash images of various sizes (typically 2 MB, 4 MB, 8 MB, 16 MB)

5. **PSRAM Integration**:
   - Similar to flash but maps into a PSRAM backing memory region
   - PSRAM pages are read-write (unlike flash which is read-only from the CPU's perspective)

6. **Dual-CPU Considerations**:
   - PRO and APP CPUs have independent MMU tables and cache controls
   - Each CPU's instruction cache maps independently
   - Data MMU tables may be shared depending on configuration
   - Cross-CPU cache coherency is handled by software (cache flush before sharing data)

7. **Boot Sequence Integration**:
   - ROM bootloader configures initial MMU entries to map the second-stage bootloader from flash
   - Second-stage bootloader reconfigures MMU to map application partitions
   - The MMU model must be functional before the bootloader can load application code

## Complexity Assessment

**Overall Complexity: VERY HIGH**

| Aspect                       | Difficulty | Notes                                              |
| ---------------------------- | ---------- | -------------------------------------------------- |
| MMU table management         | Medium     | Array of entries with simple format                |
| Address translation          | High       | Must intercept all accesses to mapped regions      |
| Flash image integration      | Medium     | Read from flash image at translated offset         |
| PSRAM integration            | Medium     | Read-write backing store with MMU translation      |
| Cache simulation             | Low-Medium | Can be simplified/omitted for functional emulation |
| Dual-CPU independence        | High       | Separate tables and controls per CPU core          |
| Boot sequence dependency     | Critical   | Must work correctly for ANY firmware to boot       |
| Flash encryption interaction | High       | Encrypted flash needs transparent decryption layer |
| Renode integration           | High       | Requires custom address translation hooks          |
| Testing                      | High       | Hard to test in isolation; needs full boot flow    |

**Estimated effort**: 4-6 weeks for a functional implementation that supports boot and XIP.

**Priority**: CRITICAL -- this is a boot-blocking dependency. Without cache/MMU, firmware cannot execute code from flash, which means nothing beyond the ROM bootloader will run. This should be one of the first subsystems implemented.

**Dependencies**:
- DPORT register model (MMU tables live in DPORT address space)
- SPI flash model (provides the backing data for flash-mapped pages)
- System bus infrastructure in Renode (for registering translated memory regions)

**Risk factors**:
- Address translation performance could be a concern if not implemented efficiently
- Subtle bugs in page mapping can cause hard-to-diagnose instruction fetch failures
- Flash encryption adds another layer of complexity
- Different ESP-IDF versions may configure MMU differently, requiring flexibility
