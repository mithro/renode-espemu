# ESP32 TWAI/CAN Controller

## Overview

The ESP32 includes a Two-Wire Automotive Interface (TWAI) controller that is compatible with the CAN 2.0 protocol (both CAN 2.0A with 11-bit identifiers and CAN 2.0B with 29-bit extended identifiers). The TWAI controller is based on the Philips SJA1000 CAN controller operating in PelCAN (Pelican) mode. This SJA1000 compatibility is significant for emulation because it means existing SJA1000 models can potentially be adapted.

TWAI/CAN is widely used in automotive and industrial applications. The ESP32's TWAI controller requires an external CAN transceiver chip (e.g., SN65HVD230, TJA1050, MCP2551) to interface with the physical CAN bus.

Espressif's QEMU fork includes a TWAI implementation, providing a solid reference.

## Hardware Specifications

- **Protocol**: CAN 2.0A (11-bit ID) and CAN 2.0B (29-bit extended ID)
- **Bit rates**: Up to 1 Mbps (standard CAN rates: 25, 50, 100, 125, 250, 500, 800, 1000 Kbps)
- **Controller core**: SJA1000-compatible (Pelican mode only; Basic CAN mode is NOT supported)
- **TX buffer**: Single transmit buffer (one frame at a time)
- **RX FIFO**: 64-byte receive FIFO buffer
  - Can hold multiple received CAN frames
  - Overflow detection with error signaling
- **Acceptance filter**:
  - Single filter mode: One 32-bit filter (ID + data bytes)
  - Dual filter mode: Two 16-bit filters
  - Each with configurable acceptance code and acceptance mask
- **Error handling**:
  - TX error counter (TEC) and RX error counter (REC)
  - Error-active, error-passive, and bus-off states per CAN specification
  - Automatic error recovery (128 occurrences of 11 consecutive recessive bits)
  - Error capture register for diagnosis
- **Operating modes**:
  - Normal mode (TX and RX on CAN bus)
  - Listen-only mode (RX only, no ACK, no error frames)
  - Self-test mode (TX and RX internally looped, no external bus needed)
  - Reset mode (configuration mode; most registers only writable in reset mode)
- **Interrupts**: TX complete, RX available, error warning, data overrun, bus-off, arbitration lost, bus error, wake-up
- **Clock**: Uses APB_CLK (80 MHz) as base; baud rate configured via bus timing registers
- **Base address**: 0x3FF6B000

## TRM Chapter Reference

- **Chapter 22**: TWAI Controller
  - Section 22.1: Features overview
  - Section 22.2: Operating modes (reset, operating, listen-only, self-test)
  - Section 22.3: Register description (SJA1000-compatible register map)
  - Section 22.4: Acceptance filtering
  - Section 22.5: Bus timing configuration
  - Section 22.6: Error management
  - Section 22.7: Interrupt handling

TRM PDF: https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf

## Register Map Summary

The TWAI registers are based on the SJA1000 Pelican mode register map. Register behavior differs depending on whether the controller is in Reset Mode or Operating Mode.

### Core Registers (base 0x3FF6B000)

| Register              | Offset | Reset Mode                                                           | Operating Mode                                                                           |
| --------------------- | ------ | -------------------------------------------------------------------- | ---------------------------------------------------------------------------------------- |
| MODE_REG              | 0x000  | Mode control (reset, listen-only, self-test, acceptance filter mode) | Same                                                                                     |
| CMD_REG               | 0x004  | N/A                                                                  | Command (TX request, abort TX, release RX buffer, clear overrun, self-reception request) |
| STATUS_REG            | 0x008  | Status                                                               | Status (RX buffer, TX buffer, error, bus-off, etc.)                                      |
| INT_RAW_REG           | 0x00C  | N/A                                                                  | Interrupt status (read-clear in SJA1000; ESP32 has raw + enable)                         |
| INT_ENA_REG           | 0x010  | Interrupt enable                                                     | Interrupt enable                                                                         |
| BUS_TIMING_0_REG      | 0x018  | Bus timing 0 (BRP, SJW)                                              | Read-only                                                                                |
| BUS_TIMING_1_REG      | 0x01C  | Bus timing 1 (TSEG1, TSEG2, SAM)                                     | Read-only                                                                                |
| ARB_LOST_CAP_REG      | 0x02C  | N/A                                                                  | Arbitration lost capture                                                                 |
| ERR_CODE_CAP_REG      | 0x030  | N/A                                                                  | Error code capture                                                                       |
| ERR_WARNING_LIMIT_REG | 0x034  | Error warning limit (default 96)                                     | Read-only                                                                                |
| RX_ERR_CNT_REG        | 0x038  | RX error counter (R/W)                                               | RX error counter (read-only)                                                             |
| TX_ERR_CNT_REG        | 0x03C  | TX error counter (R/W)                                               | TX error counter (read-only)                                                             |

### Acceptance Filter Registers (writable only in Reset Mode)

| Register              | Offset | Description                           |
| --------------------- | ------ | ------------------------------------- |
| ACCEPTANCE_CODE_0_REG | 0x040  | Acceptance code byte 0                |
| ACCEPTANCE_CODE_1_REG | 0x044  | Acceptance code byte 1                |
| ACCEPTANCE_CODE_2_REG | 0x048  | Acceptance code byte 2                |
| ACCEPTANCE_CODE_3_REG | 0x04C  | Acceptance code byte 3                |
| ACCEPTANCE_MASK_0_REG | 0x050  | Acceptance mask byte 0 (1=don't care) |
| ACCEPTANCE_MASK_1_REG | 0x054  | Acceptance mask byte 1                |
| ACCEPTANCE_MASK_2_REG | 0x058  | Acceptance mask byte 2                |
| ACCEPTANCE_MASK_3_REG | 0x05C  | Acceptance mask byte 3                |

### TX Buffer Registers (Operating Mode, after TX request)

| Register          | Offset      | Description                                                              |
| ----------------- | ----------- | ------------------------------------------------------------------------ |
| TX_FRAME_INFO_REG | 0x040       | TX frame info (DLC, RTR, extended frame flag)                            |
| TX_ID / TX_DATA   | 0x044-0x070 | TX identifier and data bytes (layout depends on standard/extended frame) |

Note: TX buffer registers share address space with acceptance filter registers. The interpretation depends on the operating mode.

### RX Buffer Registers (Operating Mode)

| Register          | Offset      | Description                                   |
| ----------------- | ----------- | --------------------------------------------- |
| RX_FRAME_INFO_REG | 0x040       | RX frame info (DLC, RTR, extended frame flag) |
| RX_ID / RX_DATA   | 0x044-0x070 | RX identifier and data bytes                  |

Note: RX data is read from the 64-byte FIFO. The RX buffer register window shows the head of the FIFO. After reading, issue a "release receive buffer" command to advance to the next frame.

### Additional Registers

| Register          | Offset | Description                            |
| ----------------- | ------ | -------------------------------------- |
| RX_MSG_CNT_REG    | 0x074  | Number of messages in RX FIFO          |
| CLOCK_DIVIDER_REG | 0x07C  | Clock divider and extended mode select |

### Bus Timing Configuration

Baud rate is configured via BUS_TIMING_0_REG and BUS_TIMING_1_REG:
- **BRP** (Baud Rate Prescaler): Divides APB clock (80 MHz). Actual prescaler = (BRP + 1) * 2
- **SJW** (Synchronization Jump Width): 1-4 TQ
- **TSEG1** (Time Segment 1): 1-16 TQ
- **TSEG2** (Time Segment 2): 1-8 TQ
- **SAM** (Sampling): Single or triple sampling

Baud rate = APB_CLK / (BRP_prescaler * (1 + TSEG1 + TSEG2))

## Source Code References

### SOC Register Definitions
- **TWAI register structure**: https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/twai_struct.h
  - Complete C struct definition of all TWAI registers
  - Bit field definitions for mode, status, command, timing registers
  - Frame format structures for TX and RX

### API Documentation and Examples
- **ESP-IDF TWAI API**: https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-reference/peripherals/twai.html
  - High-level TWAI driver API
  - Configuration (timing, filter, mode)
  - Transmit and receive operations
  - Alert and recovery mechanisms
- **Examples**: https://github.com/espressif/esp-idf/blob/master/examples/peripherals/twai
  - Network communication example (master/slave/listen)
  - Self-test mode example
  - Alert and recovery example

### QEMU Implementation
- **TWAI model**: https://github.com/espressif/qemu/blob/esp-develop/hw/net/can/esp32_twai.c
  - Complete SJA1000-compatible implementation
  - Register read/write with mode-dependent behavior
  - TX and RX frame processing
  - Acceptance filtering
  - Error counter management
  - Interrupt generation
  - Integrates with QEMU's CAN bus simulation infrastructure

## Renode Implementation Analysis

### Reference Peripherals in Renode
- Renode may have SJA1000 or other CAN controller implementations that can serve as a direct reference
- The SJA1000 register interface is extremely well-documented (NXP/Philips SJA1000 datasheet is publicly available)
- Any SJA1000-compatible model would require minimal changes for ESP32 TWAI

### Implementation Approach

1. **SJA1000 Pelican Mode Core**:
   - Implement the SJA1000 Pelican mode register set
   - **Reset Mode** vs **Operating Mode** state machine:
     - Reset Mode: Configuration registers writable, controller inactive
     - Operating Mode: Controller active, configuration registers read-only, TX/RX active
   - Mode register bit 0 controls the transition

2. **Register Handling**:
   - Many registers have dual behavior depending on mode (same offset, different meaning)
   - TX buffer and acceptance filter share address space (writable only in respective modes)
   - Status register is read-only and reflects internal state
   - Command register is write-only (self-clearing)

3. **TX Path**:
   - Software writes frame info + ID + data to TX buffer registers
   - Software writes TX request to command register
   - Controller "transmits" the frame (in emulation: deliver to virtual CAN bus)
   - TX complete interrupt generated
   - Status register updated (TX buffer available)

4. **RX Path**:
   - Incoming frame arrives from virtual CAN bus
   - Apply acceptance filter (code + mask comparison against frame ID)
   - If accepted, write frame to RX FIFO (64-byte circular buffer)
   - Update RX message count register
   - Generate RX interrupt
   - Software reads frame from RX buffer window, issues "release receive buffer" command

5. **Acceptance Filter**:
   - Single filter mode: 32-bit comparison against frame ID (and optionally first data bytes)
   - Dual filter mode: Two independent 16-bit comparisons
   - Mask bits: 1 = don't care (accept regardless), 0 = must match code bit
   - Filter comparison differs for standard (11-bit) vs extended (29-bit) frames

6. **Error Management (Simplified)**:
   - Track TX and RX error counters
   - Implement error-active / error-passive / bus-off state transitions
   - Error warning interrupt when counter exceeds limit (default 96)
   - Bus-off recovery: automatic after 128 x 11 recessive bits
   - For basic emulation, error counters can remain at 0 (no bus errors in virtual environment)

7. **Self-Test Mode**:
   - Internal loopback: TX frames are received back as RX frames
   - No external bus required
   - Useful for testing without a virtual CAN bus setup
   - Should be straightforward to implement

8. **CAN Bus Integration**:
   - If Renode has a virtual CAN bus infrastructure, connect to it
   - If not, self-test/loopback mode provides basic functionality
   - For multi-node scenarios, a virtual CAN bus hub would be needed

## Complexity Assessment

**Overall Complexity: MEDIUM**

| Aspect                  | Difficulty | Notes                                                   |
| ----------------------- | ---------- | ------------------------------------------------------- |
| SJA1000 register model  | Medium     | Well-documented but mode-dependent behavior is fiddly   |
| Mode state machine      | Low-Medium | Reset vs Operating with clear transition rules          |
| TX frame processing     | Low        | Single buffer, straightforward format                   |
| RX FIFO management      | Medium     | 64-byte circular buffer with message framing            |
| Acceptance filtering    | Medium     | Single/dual mode with mask logic                        |
| Error management        | Low        | Can be simplified for emulation (no real bus errors)    |
| Self-test/loopback mode | Low        | Simple internal routing of TX to RX                     |
| QEMU reference quality  | High       | Complete implementation available for reference         |
| SJA1000 reuse potential | High       | Standard IP; may find existing Renode/open-source model |
| CAN bus infrastructure  | Medium     | Depends on Renode's existing CAN support                |

**Estimated effort**: 2-3 weeks for a complete implementation including self-test and basic CAN bus communication.

**Priority**: LOW-MEDIUM -- TWAI/CAN is important for automotive and industrial applications but not used in typical WiFi/BLE IoT scenarios. It should be prioritized based on target firmware requirements.

**Dependencies**:
- DPORT (for clock gating: DPORT_PERIP_CLK_EN_REG bit 23)
- Interrupt matrix (for TWAI interrupt routing)
- APB clock (for baud rate calculation)
- Virtual CAN bus infrastructure (for multi-node scenarios)

**Risk factors**:
- Mode-dependent register behavior can be error-prone (same address, different meaning)
- RX FIFO overflow handling must be precise
- Acceptance filter bit-level comparison logic needs careful implementation
- Real CAN protocol has complex arbitration and error handling that may surface in testing
- Some firmware may depend on precise timing of TX complete and RX interrupts relative to CAN bit timing
