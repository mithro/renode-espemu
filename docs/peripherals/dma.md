# ESP32 DMA (Peripheral DMA / PDMA)

## Overview

The ESP32 does **not** have a centralized DMA controller. Instead, it uses peripheral-specific DMA (PDMA) engines embedded within individual peripherals. Each SPI and I2S controller instance contains its own DMA engine that operates using linked-list descriptors in memory. UART DMA is handled through a separate UHCI (Universal Host Controller Interface) module. This architecture means DMA must be implemented per-peripheral rather than as a single shared block.

The linked-list descriptor approach allows scatter-gather transfers where non-contiguous memory buffers can be chained together for continuous data movement without CPU intervention.

## Hardware Specifications

- **Architecture**: Peripheral-embedded DMA (no central DMA controller)
- **SPI DMA**: Two DMA channels shared among SPI2 (HSPI) and SPI3 (VSPI); supports linked-list descriptors for both TX and RX
- **I2S DMA**: Each I2S peripheral (I2S0, I2S1) has its own DMA engine with linked-list descriptors
- **UART DMA**: Handled via UHCI0 and UHCI1 modules, which bridge UART data to/from DMA-capable linked-list engines
- **Descriptor format**: Each DMA descriptor is a 12-byte structure containing:
  - `size` (12 bits): Buffer size in bytes
  - `length` (12 bits): Number of valid bytes in the buffer
  - `owner` (1 bit): 0 = CPU owns buffer, 1 = DMA owns buffer
  - `eof` (1 bit): End-of-frame indicator
  - `buffer address` (32 bits): Pointer to data buffer
  - `next descriptor address` (32 bits): Pointer to next descriptor in the linked list (0 = end of chain)
- **Maximum single descriptor payload**: 4095 bytes
- **Burst transfer**: Supports 4-beat burst mode
- **Address space**: DMA can access internal SRAM only (not external PSRAM directly on base ESP32)

## TRM Chapter Reference

DMA is documented within each peripheral's chapter in the ESP32 Technical Reference Manual:
- **Chapter 7**: SPI (includes SPI DMA subsections)
- **Chapter 12**: I2S (includes I2S DMA subsections)
- **Chapter 13**: UHCI (UART DMA)

TRM PDF: https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf

## Register Map Summary

### SPI DMA Registers (per SPI instance, e.g., SPI2 base 0x3FF64000, SPI3 base 0x3FF65000)

| Register                     | Offset | Description                                         |
| ---------------------------- | ------ | --------------------------------------------------- |
| SPI_DMA_CONF_REG             | 0x100  | DMA configuration (enable, reset, burst mode)       |
| SPI_DMA_OUT_LINK_REG         | 0x104  | TX linked-list descriptor start address and control |
| SPI_DMA_IN_LINK_REG          | 0x108  | RX linked-list descriptor start address and control |
| SPI_DMA_STATUS_REG           | 0x10C  | DMA status (TX/RX active, error flags)              |
| SPI_DMA_INT_ENA_REG          | 0x110  | DMA interrupt enable                                |
| SPI_DMA_INT_RAW_REG          | 0x114  | DMA interrupt raw status                            |
| SPI_DMA_INT_ST_REG           | 0x118  | DMA interrupt masked status                         |
| SPI_DMA_INT_CLR_REG          | 0x11C  | DMA interrupt clear                                 |
| SPI_IN_ERR_EOF_DES_ADDR_REG  | 0x120  | Address of inlink descriptor with error             |
| SPI_IN_SUC_EOF_DES_ADDR_REG  | 0x124  | Address of inlink descriptor with EOF               |
| SPI_OUT_EOF_DES_ADDR_REG     | 0x128  | Address of outlink descriptor with EOF              |
| SPI_OUT_EOF_BFR_DES_ADDR_REG | 0x12C  | Address of buffer for outlink EOF desc              |
| SPI_DMA_RSTATUS_REG          | 0x130  | RX DMA FSM status                                   |
| SPI_DMA_TSTATUS_REG          | 0x134  | TX DMA FSM status                                   |

### UHCI Registers (UHCI0 base 0x3FF54000, UHCI1 base 0x3FF4C000)

| Register                         | Offset | Description                               |
| -------------------------------- | ------ | ----------------------------------------- |
| UHCI_CONF0_REG                   | 0x000  | Configuration (separator, UART selection) |
| UHCI_INT_RAW_REG                 | 0x004  | Interrupt raw status                      |
| UHCI_INT_ST_REG                  | 0x008  | Interrupt masked status                   |
| UHCI_INT_ENA_REG                 | 0x00C  | Interrupt enable                          |
| UHCI_INT_CLR_REG                 | 0x010  | Interrupt clear                           |
| UHCI_DMA_OUT_STATUS_REG          | 0x014  | TX DMA status                             |
| UHCI_DMA_OUT_PUSH_REG            | 0x018  | Push data into TX FIFO                    |
| UHCI_DMA_IN_STATUS_REG           | 0x01C  | RX DMA status                             |
| UHCI_DMA_IN_POP_REG              | 0x020  | Pop data from RX FIFO                     |
| UHCI_DMA_OUT_LINK_REG            | 0x024  | TX linked-list start address              |
| UHCI_DMA_IN_LINK_REG             | 0x028  | RX linked-list start address              |
| UHCI_DMA_OUT_EOF_DES_ADDR_REG    | 0x02C  | Address of TX EOF descriptor              |
| UHCI_DMA_IN_SUC_EOF_DES_ADDR_REG | 0x030  | Address of RX success EOF descriptor      |
| UHCI_DMA_IN_ERR_EOF_DES_ADDR_REG | 0x034  | Address of RX error EOF descriptor        |
| UHCI_DMA_IN_DSCR_REG             | 0x038  | Current inlink descriptor address         |
| UHCI_DMA_OUT_DSCR_REG            | 0x03C  | Current outlink descriptor address        |
| UHCI_ESC_CONF0_REG               | 0x040  | Escape sequence config 0                  |
| UHCI_ESC_CONF1_REG               | 0x044  | Escape sequence config 1                  |

## Source Code References

### SOC Register Definitions
- **UHCI registers**: https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/uhci_reg.h
- **SPI registers (including DMA)**: https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/spi_reg.h

### HAL Layer
- SPI DMA operations are integrated into the SPI HAL layer within esp-idf
- UHCI operations are accessed through the UHCI register definitions directly

### API / Driver Level
- SPI Master driver handles DMA configuration transparently via `spi_bus_initialize()` with DMA channel selection
- UART driver can use UHCI for DMA-based transfers

### QEMU Reference
- Espressif's QEMU fork does not have a standalone DMA model; DMA behavior is embedded within each peripheral's QEMU model (SPI, I2S, etc.)

## Renode Implementation Analysis

### Reference Peripherals in Renode
- **STM32 DMA**: https://github.com/renode/renode-infrastructure/blob/master/src/Emulator/Peripherals/Peripherals/DMA/STM32DMA.cs
  - While this is a centralized DMA controller (unlike ESP32's PDMA), it demonstrates Renode's DMA modeling patterns including channel management, transfer modes, and memory access
- Renode's `DmaEngine` base class provides infrastructure for memory-to-memory and memory-to-peripheral transfers

### Implementation Approach

Since ESP32 uses peripheral-embedded DMA rather than a centralized controller, the recommended approach is:

1. **Create a shared DMA descriptor engine class** (`ESP32DmaDescriptorEngine`) that handles:
   - Linked-list descriptor parsing (reading the 12-byte descriptor structures from memory)
   - Buffer ownership management (owner bit toggling)
   - EOF detection and interrupt generation
   - Descriptor chain traversal (following next pointers)
   - Burst mode support

2. **Embed the descriptor engine in each peripheral** that uses DMA:
   - `ESP32_SPI` would instantiate the engine for TX and RX channels
   - `ESP32_I2S` would instantiate the engine for TX and RX channels
   - `ESP32_UHCI` would instantiate the engine for TX and RX, bridging to UART FIFOs

3. **Key behaviors to implement**:
   - Descriptor fetching from system bus (reading 3x 32-bit words)
   - Owner bit checking before processing a descriptor
   - Data transfer between descriptor buffers and peripheral FIFOs
   - EOF interrupt generation when `eof` bit is set in descriptor
   - Error handling (descriptor errors, timeout)
   - Link start/stop/restart control via the OUT_LINK and IN_LINK registers
   - DMA reset functionality

4. **Interrupts**: Each peripheral's DMA generates its own interrupts (not shared). These feed into the peripheral's interrupt status registers and then route through the interrupt matrix.

5. **Memory access constraints**: DMA can only access internal SRAM (addresses 0x3FFxxxxx for data, mapped appropriately). Attempts to DMA to/from flash or PSRAM addresses on the base ESP32 should be handled gracefully.

## Complexity Assessment

**Overall Complexity: HIGH**

| Aspect                     | Difficulty | Notes                                                    |
| -------------------------- | ---------- | -------------------------------------------------------- |
| Descriptor parsing         | Medium     | Well-documented 12-byte structure                        |
| Linked-list traversal      | Medium     | Standard linked-list with owner bit handshake            |
| Per-peripheral integration | High       | Must be tightly coupled with SPI/I2S/UHCI internals      |
| Multiple DMA flavors       | High       | SPI DMA, I2S DMA, and UHCI all differ in details         |
| Interrupt generation       | Medium     | Standard interrupt pattern per peripheral                |
| Testing                    | High       | Requires working peripheral + DMA + descriptor chain     |
| No centralized model       | High       | Cannot implement once and share; each peripheral differs |

**Estimated effort**: 3-4 weeks for a complete implementation across SPI, I2S, and UHCI peripherals.

**Priority**: High -- many ESP-IDF drivers use DMA for efficient data transfer. SPI DMA in particular is critical for flash access and common peripheral communication patterns. Without DMA support, peripherals fall back to CPU-polled mode which may not be how firmware is written.

**Dependencies**: Requires working SPI, I2S, and/or UART peripheral models first, since DMA is embedded within them. Also requires proper system bus memory access from the DMA engine.

**Risk factors**:
- Linked-list descriptor timing and synchronization can be subtle
- Owner bit handshake between CPU and DMA must be precise
- Different peripherals have slightly different DMA register layouts despite similar concepts
- SPI DMA channel sharing (two channels shared among SPI2/SPI3) adds complexity
