# ESP32 RMT (Remote Control Transceiver)

## Overview

The RMT (Remote Control Transceiver) peripheral is a pulse encoder/decoder designed for infrared remote control signals but widely used for driving addressable LEDs (WS2812/NeoPixel), encoding/decoding arbitrary pulse sequences, and generating precise timing waveforms. It converts between memory-stored pulse duration data and real-time signal edges on GPIO pins.

Each RMT channel has its own dedicated RAM block for storing pulse items, where each item encodes a high/low duration pair. The TX channels read items from RAM and produce the corresponding waveform; RX channels sample the input signal and write captured pulse items to RAM.

## Hardware Specifications

- **Channel count**: 8 total (channels 0-7, each independently TX or RX on ESP32)
  - Typical usage: channels 0-3 as TX, channels 4-7 as RX
  - Any channel can be configured for either direction
- **RAM**: 512 x 32-bit words total, shared across channels
  - Default: 64 words (512 bytes) per channel (8 blocks x 64 words)
  - Channels can borrow RAM blocks from higher-numbered channels for longer sequences
  - Each 32-bit word holds 2 pulse items (16 bits each: 15-bit duration + 1-bit level)
- **Clock sources**: APB_CLK (80 MHz) or REF_TICK (1 MHz)
- **Clock divider**: 8-bit (1-255), applied per channel
- **Resolution**: With APB_CLK and div=1: 12.5 ns per tick; with div=255: ~3.19 us per tick
- **Maximum pulse duration**: 15-bit = 32767 ticks per half-item
- **Carrier modulation**: Built-in carrier generator for each TX channel (configurable frequency and duty cycle)
- **Carrier demodulation**: Built-in for RX channels
- **Continuous/loop mode**: TX channels can loop (ESP32 has limited loop support)
- **Interrupts**: TX done, RX done, TX threshold, RX threshold, error per channel
- **Base address**: `0x3FF56000`
- **Register block size**: `0x1000`

## TRM Chapter Reference

- **Chapter 15: Remote Control Peripheral (RMT)**
- Source: [ESP32 Technical Reference Manual](https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf)
- Key sections:
  - 15.1 Overview
  - 15.2 Functional Description (TX/RX operation, RAM access, carrier modulation)
  - 15.3 Interrupts
  - 15.4 Register Summary and Description

## Register Map Summary

### Per-Channel Configuration Registers (x8 channels)

| Register       | Offset       | Description                                                                                    |
| -------------- | ------------ | ---------------------------------------------------------------------------------------------- |
| `RMT_CHxCONF0` | 0x0020 + x*8 | Channel x config 0: clock divider, mem block count, carrier enable/polarity                    |
| `RMT_CHxCONF1` | 0x0024 + x*8 | Channel x config 1: TX/RX enable, mem owner (TX/RX), idle output enable/level, ref_tick select |

### TX Control Registers

| Register        | Offset       | Description                                                                       |
| --------------- | ------------ | --------------------------------------------------------------------------------- |
| `RMT_CHxSTATUS` | 0x0060 + x*4 | Channel x status (read-only): state machine state, memory position, counter value |
| `RMT_CHxADDR`   | 0x0080 + x*4 | Channel x current RAM read/write address (read-only)                              |

### Interrupt Registers

| Register      | Offset | Description                        |
| ------------- | ------ | ---------------------------------- |
| `RMT_INT_RAW` | 0x00A0 | Raw interrupt status               |
| `RMT_INT_ST`  | 0x00A4 | Masked interrupt status            |
| `RMT_INT_ENA` | 0x00A8 | Interrupt enable                   |
| `RMT_INT_CLR` | 0x00AC | Interrupt clear (write-1-to-clear) |

### Carrier Registers (per channel)

| Register                | Offset       | Description                                          |
| ----------------------- | ------------ | ---------------------------------------------------- |
| `RMT_CHx_RX_CARRIER_RM` | 0x00B0 + x*4 | RX carrier removal thresholds (low/high cycle count) |
| `RMT_CH0_TX_LIM`        | 0x00D0 + x*4 | TX threshold value for wrap-around interrupt         |

### Miscellaneous

| Register       | Offset | Description                                                       |
| -------------- | ------ | ----------------------------------------------------------------- |
| `RMT_APB_CONF` | 0x00F0 | APB clock gate, memory access (FIFO mode vs direct), clock enable |

### RAM Region

| Address Range             | Description                                                  |
| ------------------------- | ------------------------------------------------------------ |
| `0x3FF56400 - 0x3FF567FF` | RMT channel RAM (512 x 32-bit words, 2 pulse items per word) |

Each pulse item is 16 bits: `{level[15], duration[14:0]}`. A zero-duration item marks end-of-sequence.

### Interrupt Bits (in `RMT_INT_*` registers)

- Bits [0:7]: `CH0_TX_END` through `CH7_TX_END` (TX complete)
- Bits [8:15]: `CH0_RX_END` through `CH7_RX_END` (RX complete)
- Bits [16:23]: `CH0_ERR` through `CH7_ERR` (error: RAM access conflict)
- Bits [24:31]: `CH0_TX_THR_EVENT` through `CH7_TX_THR_EVENT` (TX threshold reached)

**Approximate register count**: ~60 configuration registers + 512 words of RAM

## Source Code References

### SOC Register Definitions
- **Register header**: [components/soc/esp32/register/soc/rmt_reg.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/rmt_reg.h)
  - All register addresses, field masks, and bit positions
- **Register struct**: [components/soc/esp32/register/soc/rmt_struct.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/rmt_struct.h)
  - C struct overlay; particularly useful for understanding the RAM item format

### HAL Layer
- **Low-level HAL**: [components/esp_hal_rmt/esp32/include/hal/rmt_ll.h](https://github.com/espressif/esp-idf/blob/master/components/esp_hal_rmt/esp32/include/hal/rmt_ll.h)
  - Direct register manipulation functions
  - Key functions: `rmt_ll_tx_start()`, `rmt_ll_tx_set_mem_blocks()`, `rmt_ll_rx_enable()`, `rmt_ll_set_counter_clock_div()`, `rmt_ll_write_memory()`

### API Documentation
- **API reference**: [RMT API Documentation](https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-reference/peripherals/rmt.html)
  - High-level driver API for TX/RX channels
  - Encoder interface for converting data to pulse sequences
  - Key concepts: `rmt_new_tx_channel()`, `rmt_new_rx_channel()`, `rmt_transmit()`, `rmt_receive()`

### Examples
- **Example code**: [examples/peripherals/rmt](https://github.com/espressif/esp-idf/blob/master/examples/peripherals/rmt)
  - IR NEC encode/decode example
  - LED strip (WS2812) driver example
  - Musical buzzer example
  - These are excellent test cases for emulation validation

### QEMU Implementation
- **No QEMU implementation exists** for the RMT peripheral.

## Renode Implementation Analysis

### Reference Peripherals in Renode

There is no existing RMT-like peripheral in Renode. Potentially relevant references:

1. **Timer peripherals**: RMT channels internally use a clock divider and counter, similar to timer peripherals. However, the pulse-sequence-from-RAM behavior is unique.
2. **SPI/I2S with DMA**: The pattern of "read data from memory buffer and clock it out" is conceptually similar to SPI TX with DMA, though the encoding is different.
3. **BasicDoubleWordPeripheral**: Standard base class for the register interface.

### Recommended Approach

1. **Register scaffold**: Implement all ~60 configuration registers. The register space is moderately sized and relatively straightforward.

2. **RAM modeling**: The 512-word (2 KB) RAM region is critical:
   - Needs to be readable and writable by both CPU and the RMT engine
   - Must support the `mem_owner` bit which controls CPU vs peripheral access
   - Can be implemented as a simple byte array with appropriate address mapping
   - FIFO mode vs direct access mode (controlled by `APB_CONF.MEM_ACCESS_EN`)

3. **TX channel modeling**:
   - When TX is started (`tx_start` bit written), read pulse items from RAM sequentially
   - Each item specifies a level (0/1) and duration in clock ticks
   - A zero-duration item signals end of transmission
   - On completion, fire `CHx_TX_END` interrupt
   - For emulation, the actual waveform output is not needed; the key is to consume the RAM items and fire the completion interrupt after the appropriate calculated delay
   - TX threshold interrupt fires when the read pointer crosses the configured threshold (used by driver for wrap-around/continuous mode)

4. **RX channel modeling**:
   - RX is harder to emulate meaningfully since it requires external signal input
   - Minimal approach: allow software to write pulse items directly into RX RAM and trigger `CHx_RX_END` interrupt to simulate received data
   - A test harness API could inject pulse sequences for testing IR/protocol decoding firmware

5. **Carrier modulation**: Can be skipped for initial implementation. The carrier is used for IR protocols and does not affect the fundamental data flow.

6. **Clock divider**: Model the per-channel clock divider to correctly calculate timing. This affects when TX_END fires relative to when TX starts.

7. **What to skip initially**:
   - Actual waveform generation (no physical output)
   - Carrier modulation/demodulation
   - Loop/continuous mode (complex, rarely used on ESP32 original)
   - Cycle-accurate timing of individual pulse edges
   - Error detection (RAM access conflicts)

### Validation Strategy

- Use ESP-IDF RMT TX example (IR NEC or LED strip)
- Verify: channel configuration succeeds, RAM is loaded with pulse data, TX starts, TX_END interrupt fires
- For WS2812 use case: verify the driver completes the TX transaction without hanging

## Complexity Assessment

| Aspect                       | Rating          | Notes                                                            |
| ---------------------------- | --------------- | ---------------------------------------------------------------- |
| **Register count**           | Medium          | ~60 registers plus 2 KB RAM region                               |
| **Logic complexity**         | Medium-High     | TX state machine, RAM pointer management, threshold events       |
| **Interrupt model**          | Medium          | 32 interrupt sources (4 per channel x 8 channels)                |
| **External dependencies**    | Low             | GPIO matrix for signal routing (optional for emulation)          |
| **Clock domain complexity**  | Low             | Simple 8-bit clock divider per channel                           |
| **DMA**                      | None            | RMT uses its own dedicated RAM, not system DMA                   |
| **QEMU reference available** | **No**          | Must implement from TRM and HAL code analysis                    |
| **Overall effort**           | **Medium-High** | Estimated 3-4 days                                               |
| **Priority**                 | **High**        | Very popular peripheral (WS2812 LEDs), commonly used in examples |

The RMT is a high-priority peripheral due to its popularity for addressable LED control (WS2812/NeoPixel). The lack of a QEMU reference increases implementation effort. The TX path is more important than RX for most emulation scenarios. The dedicated RAM region with its access control adds some implementation complexity but is well-documented.
