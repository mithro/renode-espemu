# ESP32 Ethernet MAC (EMAC)

## Overview

The ESP32 includes a built-in 10/100 Mbps Ethernet MAC controller based on the Synopsys DesignWare Ethernet MAC IP (DW_apb_ether / GMAC). This is notable because among the ESP32 family, only the original ESP32 and the newer ESP32-P4 have an integrated Ethernet MAC; other variants (ESP32-S2, S3, C3, C6, etc.) do not.

The EMAC supports RMII (Reduced Media Independent Interface) for connecting to an external PHY chip. It includes an internal DMA engine (IDMAC) that uses ring-buffer descriptors in memory for efficient packet transmission and reception without CPU intervention for each packet.

The Synopsys DesignWare MAC IP is widely used across many SoCs (including various STM32 families), which means existing open-source implementations and documentation can serve as reference material.

## Hardware Specifications

- **MAC standard**: IEEE 802.3 compliant, 10/100 Mbps
- **Interface**: RMII (Reduced Media Independent Interface) to external PHY
  - Requires external PHY chip (e.g., LAN8720, IP101, RTL8201, DP83848)
  - Optional external 50 MHz clock source or internal APLL-generated clock
- **DMA (IDMAC)**: Internal DMA controller with:
  - Separate TX and RX descriptor rings
  - Each descriptor: 4 x 32-bit words (16 bytes)
  - Supports normal and enhanced descriptor formats
  - Programmable burst length (1, 2, 4, 8, 16, 32 beats)
  - Separate programmable transmit and receive thresholds
- **Features**:
  - Full-duplex and half-duplex operation
  - Automatic CRC generation and checking
  - Automatic padding for short frames
  - VLAN tag detection
  - Hardware checksum offload (IPv4 header checksum, TCP/UDP/ICMP checksum)
  - Magic packet and wake-on-LAN detection
  - Multicast and unicast address filtering (hash-based and perfect match)
  - Flow control (IEEE 802.3x pause frames)
  - MII management interface (MDIO) for PHY register access
  - Jumbo frame support (up to 9018 bytes)
- **FIFO sizes**: 512-byte TX FIFO, 512-byte RX FIFO
- **Clock requirements**: 50 MHz RMII clock (from external oscillator or internal APLL)
- **Base address**: 0x3FF69000 (MAC registers), 0x3FF6A000 (DMA registers)

## TRM Chapter Reference

- **Chapter 10**: Ethernet
  - Section 10.1: Overview and features
  - Section 10.2: RMII interface
  - Section 10.3: Clock configuration
  - Section 10.4: MAC register description
  - Section 10.5: DMA register description
  - Section 10.6: Descriptor format
  - Section 10.7: Interrupt handling

TRM PDF: https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf

Note: The ESP32 TRM's Ethernet chapter is relatively brief because it relies on the Synopsys DesignWare MAC databook for detailed register descriptions. The Synopsys documentation is not publicly available but the register interface is well-known from other implementations.

## Register Map Summary

### MAC Configuration Registers (base 0x3FF69000)

| Register                     | Offset | Description                                                |
| ---------------------------- | ------ | ---------------------------------------------------------- |
| EMAC_MAC_CONFIG_REG (MACCFG) | 0x000  | MAC config (speed, duplex, TX/RX enable, checksum offload) |
| EMAC_MAC_FRAMEFILT_REG       | 0x004  | Frame filter (promiscuous, multicast, broadcast control)   |
| EMAC_MAC_HASHTABLE_HIGH_REG  | 0x008  | Hash table high (multicast filtering)                      |
| EMAC_MAC_HASHTABLE_LOW_REG   | 0x00C  | Hash table low                                             |
| EMAC_MAC_GMII_ADDR_REG       | 0x010  | MDIO address (PHY address, register, read/write)           |
| EMAC_MAC_GMII_DATA_REG       | 0x014  | MDIO data (data read from / written to PHY)                |
| EMAC_MAC_FLOWCTRL_REG        | 0x018  | Flow control configuration                                 |
| EMAC_MAC_VLANTAG_REG         | 0x01C  | VLAN tag identifier                                        |
| EMAC_MAC_DEBUG_REG           | 0x024  | Debug/status (TX/RX FIFO status, MAC state)                |
| EMAC_MAC_RWKFLTRREG          | 0x028  | Remote wake-up filter                                      |
| EMAC_MAC_PMTCTRLSTATUS_REG   | 0x02C  | PMT control/status (wake-on-LAN)                           |
| EMAC_MAC_INTR_REG            | 0x038  | MAC interrupt status                                       |
| EMAC_MAC_INTRMASK_REG        | 0x03C  | MAC interrupt mask                                         |
| EMAC_MAC_ADDR0_HIGH_REG      | 0x040  | MAC address 0 high (bytes 4-5)                             |
| EMAC_MAC_ADDR0_LOW_REG       | 0x044  | MAC address 0 low (bytes 0-3)                              |

### DMA Registers (base 0x3FF6A000)

| Register                    | Offset | Description                                           |
| --------------------------- | ------ | ----------------------------------------------------- |
| EMAC_DMA_BUS_MODE_REG       | 0x000  | DMA bus mode (reset, burst length, descriptor skip)   |
| EMAC_DMA_TX_POLL_REG        | 0x004  | TX poll demand (write any value to resume TX DMA)     |
| EMAC_DMA_RX_POLL_REG        | 0x008  | RX poll demand (write any value to resume RX DMA)     |
| EMAC_DMA_RX_DESC_LIST_REG   | 0x00C  | RX descriptor list base address                       |
| EMAC_DMA_TX_DESC_LIST_REG   | 0x010  | TX descriptor list base address                       |
| EMAC_DMA_STATUS_REG         | 0x014  | DMA status (TX/RX status, bus error, interrupt flags) |
| EMAC_DMA_OP_MODE_REG        | 0x018  | DMA operation mode (start/stop TX/RX, thresholds)     |
| EMAC_DMA_INTR_EN_REG        | 0x01C  | DMA interrupt enable                                  |
| EMAC_DMA_MISS_FRAME_CNT_REG | 0x020  | Missed frame and overflow counter                     |
| EMAC_DMA_RI_WATCHDOG_REG    | 0x024  | Receive interrupt watchdog timer                      |
| EMAC_DMA_CURR_TX_DESC_REG   | 0x048  | Current TX descriptor address                         |
| EMAC_DMA_CURR_RX_DESC_REG   | 0x04C  | Current RX descriptor address                         |
| EMAC_DMA_CURR_TX_BUF_REG    | 0x050  | Current TX buffer address                             |
| EMAC_DMA_CURR_RX_BUF_REG    | 0x054  | Current RX buffer address                             |

### DMA Descriptor Format (Enhanced)

Each descriptor is 32 bytes (8 x 32-bit words) for enhanced format, 16 bytes (4 x 32-bit) for normal format:

**TX Descriptor (Normal)**:
| Word | Description                                                     |
| ---- | --------------------------------------------------------------- |
| 0    | Status: OWN bit, control flags (interrupt, CRC, checksum, etc.) |
| 1    | Control: Buffer sizes, end of ring, chained, buffer 2 size      |
| 2    | Buffer 1 address (pointer to TX data)                           |
| 3    | Buffer 2 address / Next descriptor address (if chained)         |

**RX Descriptor (Normal)**:
| Word | Description                                                    |
| ---- | -------------------------------------------------------------- |
| 0    | Status: OWN bit, frame length, error flags, first/last segment |
| 1    | Control: Buffer sizes, end of ring, chained                    |
| 2    | Buffer 1 address (pointer to RX data buffer)                   |
| 3    | Buffer 2 address / Next descriptor address (if chained)        |

The OWN bit (bit 31 of word 0) determines ownership: 1 = DMA owns descriptor, 0 = CPU/software owns descriptor.

## Source Code References

### SOC Register Definitions
- **MAC register structures**: https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/emac_mac_struct.h
  - Complete C struct definitions for MAC configuration registers
  - Bit field definitions for all MAC registers
- **DMA register structures**: https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/emac_dma_struct.h
  - Complete C struct definitions for DMA configuration registers
  - Descriptor format definitions

### HAL Layer
- **EMAC low-level operations**: https://github.com/espressif/esp-idf/blob/master/components/esp_hal_emac/esp32/include/hal/emac_ll.h
  - `emac_ll_set_speed()` - Set 10/100 Mbps
  - `emac_ll_set_duplex()` - Set full/half duplex
  - `emac_ll_transmit_enable()` / `emac_ll_receive_enable()` - Enable TX/RX
  - `emac_ll_set_addr()` - Configure MAC address
  - `emac_ll_read_phy_reg()` / `emac_ll_write_phy_reg()` - MDIO PHY access
  - `emac_ll_start_stop_dma_transmit()` / `emac_ll_start_stop_dma_receive()` - DMA control

### API Documentation and Examples
- **ESP-IDF Ethernet API**: https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-reference/network/esp_eth.html
  - High-level Ethernet driver API
  - PHY driver configuration
  - Network interface integration
- **Examples**: https://github.com/espressif/esp-idf/blob/master/examples/ethernet
  - Basic Ethernet connection example
  - Ethernet-to-WiFi bridge example

## Renode Implementation Analysis

### Reference Peripherals in Renode
- Renode has existing Synopsys DesignWare EMAC / GMAC implementations for other SoCs that can serve as a starting point:
  - STM32 Ethernet models (STM32F4/F7/H7 use the same Synopsys DW MAC IP)
  - The register interface is largely compatible across all Synopsys DW MAC implementations
- Renode's `NetworkWithPHY` base class provides PHY emulation infrastructure
- `EthernetPhysicalLayer` and related classes handle network packet simulation

### Implementation Approach

1. **Leverage Existing Synopsys DW MAC Model**:
   - The ESP32 EMAC is based on Synopsys DesignWare, which is the same IP used in STM32F4xx/F7xx
   - If Renode already has a DW EMAC model, it may be adaptable with minimal ESP32-specific changes
   - Key ESP32 differences: base addresses, clock source configuration, RMII-only (no MII/GMII)

2. **MAC Register Block**:
   - Implement MAC configuration registers at 0x3FF69000
   - Key registers: MACCFG (speed, duplex, TX/RX enable), frame filter, MAC address
   - MDIO registers for PHY access (read/write external PHY registers)

3. **DMA Engine**:
   - Implement DMA registers at 0x3FF6A000
   - Support descriptor ring processing for both TX and RX
   - Parse descriptor format (OWN bit, buffer address, length, control flags)
   - **TX path**: When TX is started and descriptors are available, read packet data from descriptor buffers and send to Renode's virtual network
   - **RX path**: When a packet arrives from Renode's virtual network, write to next available RX descriptor buffer, set status flags, trigger interrupt

4. **PHY Emulation**:
   - Implement a virtual PHY that responds to MDIO read/write commands
   - Key PHY registers: Basic Control (reg 0), Basic Status (reg 1), PHY ID (regs 2-3), Auto-negotiation
   - PHY should report link-up, 100 Mbps, full-duplex to the MAC
   - PHY ID registers should match a known PHY (e.g., LAN8720 ID: 0x0007C0F0)

5. **Interrupt Integration**:
   - DMA status register generates interrupts for TX complete, RX complete, bus error, etc.
   - Must route through the ESP32 interrupt matrix (DPORT)

6. **Network Integration**:
   - Connect to Renode's virtual network infrastructure
   - Support TAP interface for bridging to host network
   - Support switch/hub models for connecting multiple emulated nodes

## Complexity Assessment

**Overall Complexity: HIGH**

| Aspect                       | Difficulty  | Notes                                                     |
| ---------------------------- | ----------- | --------------------------------------------------------- |
| Reuse from Synopsys DW model | Medium      | Significant reuse potential if Renode has existing DW MAC |
| MAC register implementation  | Medium      | Well-documented, many registers but straightforward       |
| DMA descriptor engine        | High        | Ring buffer with OWN bit handshake, error handling        |
| TX path                      | Medium      | Read descriptors, assemble packet, transmit               |
| RX path                      | Medium-High | Receive packet, find descriptor, write, update status     |
| PHY emulation (MDIO)         | Low-Medium  | Simple register model for basic PHY                       |
| Checksum offload             | Medium      | Hardware checksum calculation for IP/TCP/UDP              |
| Network integration          | Medium      | Renode has network infrastructure to leverage             |
| Clock configuration          | Low         | RMII clock source selection; can be simplified            |
| Testing                      | Medium      | Need network traffic generation/capture                   |

**Estimated effort**:
- If reusing existing Synopsys DW MAC model: 2-3 weeks for adaptation
- If implementing from scratch: 4-6 weeks

**Priority**: LOW-MEDIUM -- Ethernet is important for specific use cases (industrial IoT, wired connectivity) but many ESP32 applications use WiFi instead. Lower priority than WiFi/BLE, GPIO, SPI, UART, and timers.

**Dependencies**:
- DPORT (for clock gating and interrupt routing)
- System bus / memory (for DMA descriptor access)
- Renode network infrastructure
- Interrupt matrix

**Risk factors**:
- DMA descriptor processing must be precise; off-by-one errors cause packet corruption
- OWN bit handshake timing between DMA and CPU can be subtle
- PHY auto-negotiation sequence may trip up some drivers
- The Synopsys DW MAC IP has many optional features; the ESP32 may implement a subset
- Different PHY chip drivers have different MDIO register expectations
