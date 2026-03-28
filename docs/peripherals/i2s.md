# ESP32 I2S

## Overview

The ESP32 contains two I2S (Inter-IC Sound) controllers: I2S0 and I2S1. While primarily designed for audio streaming, these controllers are highly versatile and are also used for camera input (DVP parallel interface), parallel LCD output, and general-purpose parallel data transfer. The I2S controllers support full-duplex audio with independent TX and RX channels, multiple audio formats (I2S, PCM, PDM), and DMA-based data transfer via linked-list descriptors.

The I2S peripheral is notably used by the popular ESP32-CAM module, which uses I2S0 in camera (LCD_CAM) mode to receive parallel pixel data from an OV2640 or similar camera sensor. This makes I2S emulation relevant beyond pure audio use cases.

Each I2S controller has a built-in DMA engine that uses linked-list descriptors to read from or write to memory, eliminating CPU involvement during streaming. The controllers also include an APLL (Audio PLL) clock source option for precise audio sample rates.

## Hardware Specifications

| Feature                   | I2S0                | I2S1              |
| ------------------------- | ------------------- | ----------------- |
| **Base address**          | 0x3FF4F000          | 0x3FF6D000        |
| **TX supported**          | Yes                 | Yes               |
| **RX supported**          | Yes                 | Yes               |
| **Camera mode**           | Yes                 | No                |
| **LCD mode**              | Yes                 | Yes               |
| **Built-in ADC/DAC mode** | Yes (via I2S0 only) | No                |
| **PDM mode**              | Yes                 | No                |
| **DMA**                   | Yes (linked-list)   | Yes (linked-list) |

### Supported Audio Formats
- **I2S Philips standard**: MSB-first, 1-clock delay after WS transition
- **I2S MSB-justified (left-aligned)**: MSB-first, no delay after WS transition
- **I2S LSB-justified (right-aligned)**: LSB-justified alignment
- **PCM short/long sync**: Pulse-based frame sync
- **PDM (Pulse Density Modulation)**: Single-bit oversampled (I2S0 only)

### Data Widths
- 8, 16, 24, or 32 bits per sample per channel
- Mono or stereo (2-channel)
- Up to 2x 32-bit channels (64 bits per frame)

### Clock System
- **Clock sources**: APB clock (80 MHz), APLL (Audio PLL, configurable)
- **MCLK**: Master clock output (integer multiple of sample rate)
- **BCLK**: Bit clock (= sample_rate x bits_per_sample x num_channels)
- **WS/LRCK**: Word select / left-right clock (= sample rate)

### Camera/LCD Mode
- **Camera input (I2S0 only)**: 8-bit or 16-bit parallel data input, with HSYNC, VSYNC, PCLK signals. Used by ESP32-CAM.
- **LCD output**: 8-bit or 16-bit parallel data output with WR signal. Used for driving parallel LCD displays (8080/6800 interface).

## TRM Chapter Reference

**ESP32 Technical Reference Manual, Chapter 12: I2S**

- https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf

Key sections:
- 12.1: Overview of I2S features and supported modes
- 12.2: I2S clock generation and configuration
- 12.3: I2S audio data formats (Philips, MSB, LSB, PCM)
- 12.4: PDM mode operation (I2S0)
- 12.5: LCD/Camera mode operation
- 12.6: DMA linked-list operation
- 12.7: Built-in ADC/DAC mode (I2S0)
- 12.8: Register descriptions

## Register Map Summary

The I2S register space is approximately 0x100 bytes per controller. Key registers include:

### Configuration
| Register          | Offset | Description                                                                                          |
| ----------------- | ------ | ---------------------------------------------------------------------------------------------------- |
| I2S_CONF_REG      | 0x08   | Main configuration: TX/RX reset, FIFO reset, TX/RX start, mode select (master/slave), channel format |
| I2S_CONF1_REG     | 0x0A0  | TX/RX PCM mode bypass, TX stop enable                                                                |
| I2S_CONF2_REG     | 0x0A8  | Camera/LCD mode enable, LCD TX WR/data mode select                                                   |
| I2S_CONF_CHAN_REG | 0x2C   | TX/RX channel mode (mono/stereo/dual-channel)                                                        |

### Sample Rate / Clock
| Register                 | Offset | Description                                                                                  |
| ------------------------ | ------ | -------------------------------------------------------------------------------------------- |
| I2S_SAMPLE_RATE_CONF_REG | 0x0B0  | TX/RX bit and channel dividers (BCK_DIV_NUM, module clock divider)                           |
| I2S_CLKM_CONF_REG        | 0x0AC  | Clock module configuration: clock divider integer/fractional, clock source select (APB/APLL) |

### FIFO Configuration
| Register          | Offset | Description                                                        |
| ----------------- | ------ | ------------------------------------------------------------------ |
| I2S_FIFO_CONF_REG | 0x20   | TX/RX FIFO configuration: data length, FIFO thresholds, DMA enable |

### Data Format
| Register                | Offset | Description                                           |
| ----------------------- | ------ | ----------------------------------------------------- |
| I2S_CONF_SIGLE_DATA_REG | 0x28   | Static single data value (used when TX FIFO is empty) |

### DMA Registers
| Register                    | Offset | Description                                             |
| --------------------------- | ------ | ------------------------------------------------------- |
| I2S_OUT_LINK_REG            | 0x30   | TX DMA linked-list descriptor start address and control |
| I2S_IN_LINK_REG             | 0x34   | RX DMA linked-list descriptor start address and control |
| I2S_LC_CONF_REG             | 0x60   | DMA configuration: check owner, loop mode, auto-wrback  |
| I2S_OUTLINK_DSCR_REG        | 0x38   | Current TX DMA descriptor address (read-only)           |
| I2S_INLINK_DSCR_REG         | 0x48   | Current RX DMA descriptor address (read-only)           |
| I2S_OUT_EOF_DES_ADDR_REG    | 0x40   | TX EOF descriptor address                               |
| I2S_IN_SUC_EOF_DES_ADDR_REG | 0x4C   | RX success EOF descriptor address                       |
| I2S_LC_STATE0_REG           | 0x6C   | TX DMA state                                            |
| I2S_LC_STATE1_REG           | 0x70   | RX DMA state                                            |

### Interrupts
| Register        | Offset | Description                        |
| --------------- | ------ | ---------------------------------- |
| I2S_INT_RAW_REG | 0x0C   | Raw interrupt status               |
| I2S_INT_ST_REG  | 0x10   | Masked interrupt status            |
| I2S_INT_ENA_REG | 0x14   | Interrupt enable                   |
| I2S_INT_CLR_REG | 0x18   | Interrupt clear (write-1-to-clear) |

### Key Interrupt Sources
- **TX_REMPTY**: TX DMA has sent all data
- **RX_WFULL**: RX DMA buffer full
- **OUT_EOF**: TX linked-list reached descriptor with EOF flag
- **IN_SUC_EOF**: RX linked-list reached descriptor with EOF flag
- **OUT_DONE**: TX linked-list finished all descriptors
- **IN_DONE**: RX linked-list finished all descriptors
- **OUT_DSCR_ERR**: TX descriptor error (invalid descriptor)
- **IN_DSCR_ERR**: RX descriptor error (invalid descriptor)

### PDM Configuration (I2S0 only)
| Register              | Offset | Description                                        |
| --------------------- | ------ | -------------------------------------------------- |
| I2S_PDM_CONF_REG      | 0x0B4  | PDM enable, decimation/interpolation filter config |
| I2S_PDM_FREQ_CONF_REG | 0x0B8  | PDM frequency configuration                        |

### Timing
| Register       | Offset | Description                       |
| -------------- | ------ | --------------------------------- |
| I2S_TIMING_REG | 0x1C   | Signal timing/delay configuration |

## Source Code References

### SOC Register Definitions
- **Register header**: https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/i2s_reg.h
- **Register struct**: https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/i2s_struct.h

### HAL Layer
- **I2S HAL LL**: https://github.com/espressif/esp-idf/blob/master/components/esp_hal_i2s/esp32/include/hal/i2s_ll.h

### API Documentation
- **I2S API**: https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-reference/peripherals/i2s.html

### Examples
- **I2S examples**: https://github.com/espressif/esp-idf/blob/master/examples/peripherals/i2s

### QEMU Implementation
No QEMU implementation exists for the ESP32 I2S peripheral.

## Renode Implementation Analysis

### Existing Renode Models

Renode does not have a widely-used I2S peripheral model. Audio peripherals are generally less common in emulation scenarios. However, the DMA linked-list engine shares architectural patterns with the SPI DMA implementation on the ESP32, so code can potentially be shared.

### Implementation Approach

I2S emulation strategy depends heavily on the target use case. Two main scenarios exist:

**Scenario A: Audio Streaming (I2S standard mode)**

For audio use cases, the emulation does not need to produce actual audio output. The primary goal is to make the ESP-IDF I2S driver believe transfers are completing successfully:

1. **DMA engine**: Implement the linked-list descriptor parser. This is the same DMA architecture used by SPI and should be factored into a shared DMA component.
   - Parse descriptors from memory (address, length, EOF flag, next pointer)
   - Track current descriptor position for TX and RX paths
   - Handle circular (loop) mode for continuous streaming

2. **TX path**: When DMA is started for TX, consume data from DMA buffers (reading from memory via descriptors) and discard it (or optionally forward to a host audio sink). Generate OUT_EOF and OUT_DONE interrupts at appropriate points.

3. **RX path**: When DMA is started for RX, fill DMA buffers with silence (zeros) or optionally from a host audio source. Generate IN_SUC_EOF and IN_DONE interrupts.

4. **Timing**: Audio streaming is inherently time-dependent. The emulation must generate DMA completion interrupts at a rate that approximates the configured sample rate, or the application may time out or buffer incorrectly. This can be achieved with Renode's timer infrastructure.

5. **Configuration registers**: Store all configuration values (sample rate, bit width, channel count, format) so the driver can read them back, but the actual data path does not need to honor format differences -- it just moves bytes.

**Scenario B: Camera Input (I2S0 LCD_CAM mode)**

For ESP32-CAM and similar camera applications:

1. **Camera input emulation**: The I2S0 controller in camera mode receives parallel pixel data clocked by an external PCLK, framed by HSYNC/VSYNC. In emulation, this would be replaced by injecting pre-captured frame data into the RX DMA path.

2. **Frame injection**: Provide a mechanism to load image data (e.g., from a file or test pattern) into the RX DMA buffers when camera capture is triggered. The data format must match what the camera sensor would produce (e.g., YUV422, RGB565).

3. **VSYNC/HSYNC simulation**: Generate appropriate interrupts to signal frame boundaries.

4. **Camera sensor (I2C)**: The camera sensor itself (e.g., OV2640) is configured via I2C. A separate I2C slave device model is needed for the sensor's register interface.

**Minimum Viable Implementation:**

For basic ESP-IDF compatibility, the minimum implementation needs:
1. Configuration registers (store and return values)
2. DMA linked-list descriptor engine (shared with SPI DMA)
3. TX path: consume DMA buffers and generate completion interrupts
4. RX path: provide data to DMA buffers and generate completion interrupts
5. FIFO reset and start/stop control

**Key simplifications:**
- Audio format details (I2S vs PCM vs PDM) do not affect the data path in emulation
- Clock configuration (APLL, BCLK dividers) can be stored but not functionally modeled
- Signal timing registers have no effect
- PDM decimation/interpolation filters are not needed

## Complexity Assessment

| Component                   | Complexity  | Priority | Rationale                                                                                                                     |
| --------------------------- | ----------- | -------- | ----------------------------------------------------------------------------------------------------------------------------- |
| **DMA linked-list engine**  | MEDIUM-HIGH | HIGH     | Core data transport. Shared architecture with SPI DMA. Descriptor parsing, chaining, EOF handling, circular mode.             |
| **TX DMA path**             | MEDIUM      | MEDIUM   | Consume data from descriptors, generate interrupts. Timing approximation needed for streaming.                                |
| **RX DMA path**             | MEDIUM      | MEDIUM   | Fill descriptors with data, generate interrupts. Data source depends on use case.                                             |
| **Configuration registers** | LOW         | HIGH     | Store all config values for driver read-back. Large register set but mostly passive storage.                                  |
| **Audio mode (I2S/PCM)**    | LOW-MEDIUM  | LOW      | Format details irrelevant in emulation. Just move bytes.                                                                      |
| **PDM mode**                | MEDIUM      | LOW      | I2S0 only. Rare in emulation scenarios. Filter math not needed.                                                               |
| **Camera mode (LCD_CAM)**   | HIGH        | MEDIUM   | Requires frame data injection, sync signal emulation, and coordination with camera sensor I2C model. Important for ESP32-CAM. |
| **LCD output mode**         | MEDIUM      | LOW      | Parallel display output. Useful for display emulation but lower priority.                                                     |
| **Built-in ADC/DAC mode**   | MEDIUM      | LOW      | I2S0 only. Requires integration with ADC/DAC peripheral models.                                                               |
| **Interrupt generation**    | MEDIUM      | HIGH     | DMA EOF, done, and error interrupts are critical for driver operation.                                                        |

**Overall I2S complexity: MEDIUM-HIGH**

The I2S peripheral has a large register set and many modes (audio, camera, LCD, PDM, ADC/DAC), but most of the complexity lies in mode-specific details that can be deferred. The core DMA engine is the critical component and is shared with SPI. The lack of a QEMU reference implementation means more effort is needed to determine the minimal register set for driver compatibility, but the ESP-IDF HAL LL source provides a clear map of which registers the driver actually touches.

**Estimated register count**: ~50 registers per instance, ~100 total across both instances.
