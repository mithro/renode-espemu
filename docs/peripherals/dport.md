# ESP32 DPORT (System Registers)

## Overview

DPORT is the central system register block of the ESP32. It controls peripheral clock gating, peripheral reset, interrupt routing and mapping, cache/MMU configuration, cross-core communication, and various system-level settings. Nearly every other subsystem depends on DPORT registers in some way.

DPORT is accessed by firmware from the very first instructions of the ROM bootloader through the entire lifecycle of the application. It is the single most critical peripheral block for emulation because it gates access to every other peripheral and controls the cache/MMU that enables code execution from flash.

The DPORT register space has a notable hardware quirk: concurrent access from both CPU cores can cause read errors due to a hardware bug. ESP-IDF works around this with special accessor macros (`DPORT_READ_PERI_REG`), but this quirk does not need to be emulated.

## Hardware Specifications

- **Base address**: 0x3FF00000
- **Register space size**: 0x1000 (4 KB of system registers) plus MMU table space
- **Bus**: Connected to both PRO CPU and APP CPU via AHB bus
- **Functions controlled**:
  - Peripheral clock enable/disable (clock gating)
  - Peripheral reset assertion/deassertion
  - Interrupt source to CPU interrupt matrix mapping
  - Cache and MMU configuration for both CPUs
  - Cross-core interrupt generation (PRO_CPU <-> APP_CPU)
  - Boot and security configuration readback
  - Memory region access permissions (PID controller)
  - Various system identification and status registers

## TRM Chapter Reference

- **Chapter 1**: System and Memory
  - Section 1.3: System registers (DPORT)
  - Section 1.4: Memory map and access permissions
- **Chapter 9**: Cache and MMU (DPORT-hosted MMU tables)
- **Chapter 7**: Interrupt Matrix (interrupt mapping registers in DPORT)

TRM PDF: https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf

## Register Map Summary

### System Configuration

| Register                          | Offset | Description                                        |
|-----------------------------------|--------|----------------------------------------------------|
| DPORT_PRO_BOOT_REMAP_CTRL_REG    | 0x000  | PRO CPU boot remap control                         |
| DPORT_APP_BOOT_REMAP_CTRL_REG    | 0x004  | APP CPU boot remap control                         |
| DPORT_PERI_CLK_EN_REG            | 0x008  | Secondary peripheral clock enable                  |
| DPORT_PERI_RST_EN_REG            | 0x00C  | Secondary peripheral reset                         |
| DPORT_APPCPU_CTRL_REG_A          | 0x02C  | APP CPU clock control                              |
| DPORT_APPCPU_CTRL_REG_B          | 0x030  | APP CPU clock gate                                 |
| DPORT_APPCPU_CTRL_REG_C          | 0x034  | APP CPU reset vector (low bits)                    |
| DPORT_APPCPU_CTRL_REG_D          | 0x038  | APP CPU reset control                              |
| DPORT_CPU_PER_CONF_REG           | 0x03C  | CPU period/frequency configuration                 |

### Cache and MMU Control

| Register                          | Offset | Description                                        |
|-----------------------------------|--------|----------------------------------------------------|
| DPORT_PRO_CACHE_CTRL_REG         | 0x040  | PRO CPU cache enable and mode                      |
| DPORT_PRO_CACHE_CTRL1_REG        | 0x044  | PRO CPU cache address mask                         |
| DPORT_APP_CACHE_CTRL_REG         | 0x058  | APP CPU cache enable and mode                      |
| DPORT_APP_CACHE_CTRL1_REG        | 0x05C  | APP CPU cache address mask                         |
| DPORT_CACHE_IA_INT_EN_REG        | 0x1A0  | Cache illegal access interrupt enable              |

### Peripheral Clock Gating and Reset

| Register                          | Offset | Description                                        |
|-----------------------------------|--------|----------------------------------------------------|
| DPORT_PERIP_CLK_EN_REG           | 0x0C0  | Main peripheral clock enable (one bit per periph)  |
| DPORT_PERIP_RST_EN_REG           | 0x0C4  | Main peripheral reset control (one bit per periph) |
| DPORT_WIFI_CLK_EN_REG            | 0x0CC  | WiFi/BT/crypto clock enables                       |
| DPORT_WIFI_RST_EN_REG            | 0x0D0  | WiFi/BT/crypto reset control                       |

#### DPORT_PERIP_CLK_EN_REG Bit Map (offset 0x0C0)

| Bit  | Peripheral          | Bit  | Peripheral            |
|------|---------------------|------|-----------------------|
| 0    | TIMERS              | 16   | UHCI1                 |
| 1    | SPI01 (SPI/SPI1)    | 17   | TIMERGROUP1           |
| 2    | UART                | 18   | EFUSE                 |
| 3    | WDG (Watchdog)      | 19   | TIMERGROUP0           |
| 4    | I2S0                | 20   | SPI3 (VSPI)          |
| 5    | UART1               | 21   | PWM0                  |
| 6    | SPI2 (HSPI)         | 22   | I2C_EXT1              |
| 7    | I2C_EXT0            | 23   | CAN (TWAI)            |
| 8    | UHCI0               | 24   | PWM1                  |
| 9    | RMT                 | 25   | I2S1                  |
| 10   | PCNT                | 26   | SPI_DMA               |
| 11   | LEDC                | 27   | UART2                 |
| 12   | UHCI1_DMA (unused)  | 28   | UART_MEM              |
| 13   | TIMERGROUP           | 29   | PWM2                  |
| 14   | EFUSE_CLK           | 30   | PWM3                  |
| 15   | TIMERGROUP_CLK      | 31   | -                     |

### Interrupt Matrix Mapping

| Register Range                           | Offset Range  | Description                          |
|------------------------------------------|---------------|--------------------------------------|
| DPORT_PRO_*_MAP_REG (various)            | 0x104 - 0x19C | PRO CPU interrupt source mapping     |
| DPORT_APP_*_MAP_REG (various)            | 0x200 - 0x298 | APP CPU interrupt source mapping     |

Each interrupt source has a dedicated register that specifies which CPU interrupt line (0-31) it should be routed to. There are approximately 70+ interrupt sources.

### Cross-Core Communication

| Register                          | Offset | Description                                        |
|-----------------------------------|--------|----------------------------------------------------|
| DPORT_CPU_INTR_FROM_CPU_0_REG    | 0x0DC  | Cross-core interrupt 0 (generate IRQ to other CPU) |
| DPORT_CPU_INTR_FROM_CPU_1_REG    | 0x0E0  | Cross-core interrupt 1                             |
| DPORT_CPU_INTR_FROM_CPU_2_REG    | 0x0E4  | Cross-core interrupt 2                             |
| DPORT_CPU_INTR_FROM_CPU_3_REG    | 0x0E8  | Cross-core interrupt 3                             |

### MMU Table Regions

| Address Range           | Description                              |
|-------------------------|------------------------------------------|
| 0x3FF10000 - 0x3FF100FF | PRO CPU instruction flash MMU (64 entries) |
| 0x3FF12000 - 0x3FF120FF | APP CPU instruction flash MMU (64 entries) |
| 0x3FF14000 - 0x3FF140FF | Data flash MMU (64 entries)                |
| 0x3FF16000 - 0x3FF160FF | PSRAM MMU (64 entries)                     |

## Source Code References

### SOC Register Definitions
- **DPORT register definitions**: https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/dport_reg.h
  - Comprehensive register and bit field definitions
  - Clock gate bit positions
  - Interrupt mapping register offsets
  - Cache/MMU control fields

### Interrupt Mapping
- **Interrupt source definitions**: https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/include/soc/interrupts.h
  - Enumeration of all interrupt sources (ETS_*_INTR_SOURCE)
  - Maps interrupt source names to their mapping register indices

### QEMU Implementation
- **DPORT model**: https://github.com/espressif/qemu/blob/esp-develop/hw/misc/esp32_dport.c
  - Extensive implementation covering:
    - Register read/write handling
    - Cache/MMU configuration
    - Peripheral clock gating tracking
    - APP CPU boot control
    - Cross-core interrupt generation
    - Interrupt matrix routing
  - This is one of the largest and most complete peripheral models in Espressif's QEMU fork

## Renode Implementation Analysis

### Reference Peripherals in Renode
- Various SoC system control blocks (e.g., STM32 RCC, NRF52 CLOCK) demonstrate patterns for clock gating and reset control
- Renode interrupt controllers show interrupt routing patterns

### Implementation Approach

DPORT should be one of the first peripherals implemented due to its central role. Recommended approach:

1. **Core Register Block**:
   - Implement as a `BasicDoubleWordPeripheral` covering the 0x3FF00000 base address
   - Support read/write for all documented registers
   - Unknown/unimplemented registers should be logged and return 0 (many registers are accessed speculatively by firmware)

2. **Peripheral Clock Gating (DPORT_PERIP_CLK_EN_REG)**:
   - Track the clock enable bits
   - Initially, do not enforce clock gating (let all peripherals be accessible regardless)
   - Later, optionally wire clock gate status to peripheral models for accuracy
   - Important: firmware reads this register back to verify clock is enabled

3. **Peripheral Reset (DPORT_PERIP_RST_EN_REG)**:
   - Track reset bits
   - Optionally trigger reset on peripheral models when their reset bit transitions
   - Firmware toggles reset bits during peripheral initialization

4. **Interrupt Matrix**:
   - Implement the ~70 interrupt mapping registers for each CPU
   - Each register holds a 5-bit value selecting which CPU interrupt line (0-31) to route to
   - Must integrate with Renode's interrupt infrastructure to properly deliver interrupts to CPU models
   - This is critical for any interrupt-driven firmware to work

5. **Cache/MMU Control**:
   - Implement cache enable/disable registers
   - Host the MMU table arrays (see cache-mmu.md for details)
   - Wire to the address translation system

6. **APP CPU Control**:
   - `DPORT_APPCPU_CTRL_REG_D` controls APP CPU reset/stall
   - Boot vector registers set APP CPU's initial PC
   - Critical for dual-core operation and SMP boot

7. **Cross-Core Interrupts**:
   - Writing 1 to `CPU_INTR_FROM_CPU_x_REG` generates an interrupt on the target CPU
   - Used by FreeRTOS for cross-core task scheduling
   - Must be implemented for SMP operation

8. **Phased Implementation**:
   - Phase 1: Clock gating, reset, basic register read/write, cache control
   - Phase 2: Interrupt matrix routing
   - Phase 3: APP CPU boot control, cross-core interrupts
   - Phase 4: Full MMU integration, memory access permissions

## Complexity Assessment

**Overall Complexity: HIGH**

| Aspect                        | Difficulty | Notes                                                |
|-------------------------------|------------|------------------------------------------------------|
| Basic register read/write     | Low        | Large register space but mostly simple storage       |
| Clock gating tracking         | Low        | Bit field tracking                                   |
| Reset control                 | Medium     | Need to wire reset signals to peripheral models      |
| Interrupt matrix              | High       | ~70 sources x 2 CPUs, must integrate with Renode IRQ |
| Cache/MMU registers           | High       | Complex interaction with address translation         |
| APP CPU boot control          | Medium     | State machine for CPU stall/unstall/reset            |
| Cross-core interrupts         | Medium     | Must reliably deliver IRQ across CPU models          |
| Register space size           | Medium     | Very large number of registers to implement          |
| Boot criticality              | Critical   | DPORT must be functional for ROM bootloader          |

**Estimated effort**: 3-5 weeks for a comprehensive implementation across all phases.

**Priority**: CRITICAL -- DPORT is the single most important peripheral for emulation. It is accessed in the first instructions of boot, controls all peripheral access, hosts the MMU tables, and routes all interrupts. No other peripheral can function correctly without DPORT.

**Dependencies**:
- Renode's interrupt infrastructure (for interrupt matrix)
- CPU models (for cross-core interrupts and APP CPU control)
- Cache/MMU implementation (closely coupled)

**Risk factors**:
- Very large register space means many firmware accesses to handle
- Interrupt matrix correctness is critical for all interrupt-driven code
- APP CPU boot sequence has precise timing/ordering requirements
- Many registers have interdependencies (e.g., clock gate must be enabled before peripheral reset is deasserted)
