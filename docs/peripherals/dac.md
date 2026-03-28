# ESP32 DAC (Digital-to-Analog Converter)

## Overview

The ESP32 includes two 8-bit DAC (Digital-to-Analog Converter) channels that convert digital values to analog voltage outputs. The DACs can be used for audio output, waveform generation, voltage reference generation, and other analog output applications. Each channel has a built-in cosine waveform (CW) generator that can produce cosine waves of configurable frequency, amplitude, and DC offset without CPU intervention.

The DAC shares the SENS (Sensor) register block with the ADC and Touch sensor peripherals. For higher sample-rate applications, the DAC can be fed via DMA through the I2S peripheral.

## Hardware Specifications

- **Channel count**: 2
  - DAC channel 1: GPIO25
  - DAC channel 2: GPIO26
- **Resolution**: 8-bit (output values 0-255)
- **Output voltage range**: 0 to VDD_A (approximately 0 to 3.3V)
  - Output voltage = VDD_A * DAC_VALUE / 256
- **Operating modes**:
  - **Direct write**: Software writes an 8-bit value to the DAC register
  - **Cosine waveform (CW) generator**: Hardware generates cosine wave
  - **DMA mode**: DAC data fed via I2S DMA for continuous waveform output
- **Cosine waveform generator** (per channel):
  - Frequency: Configurable via frequency step register (f = RTC8M_CLK / 65536 * step)
  - Amplitude scaling: Full, 1/2, 1/4, 1/8
  - Phase: 0 or 180 degrees
  - DC offset: 8-bit configurable
- **Output buffering**: Optional output buffer amplifier (can be enabled/disabled)
- **Power control**: Individual channel power-down
- **Base address**: `0x3FF48800` (SENS register block, shared with ADC and Touch)

## TRM Chapter Reference

- **Chapter 27: On-Chip Sensors and Analog Signal Processing (DAC section)**
- Source: [ESP32 Technical Reference Manual](https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf)
- Key sections:
  - 27.6 Digital-to-Analog Converter (DAC)
  - 27.6.1 Overview
  - 27.6.2 Functional Description
  - 27.6.3 Cosine Waveform Generator
  - 27.6.4 DAC with DMA

## Register Map Summary

DAC registers are within the SENS register block at base `0x3FF48800`, shared with ADC and Touch sensor registers. The DAC has a small register footprint.

### DAC Output Registers

| Register             | Offset          | Description                                               |
| -------------------- | --------------- | --------------------------------------------------------- |
| `SENS_SAR_DAC_CTRL1` | 0x0098 (approx) | DAC control 1: DAC clock control, output enable           |
| `SENS_SAR_DAC_CTRL2` | 0x009C (approx) | DAC control 2: per-channel data, CW enable, power control |

Note: The exact register offsets for DAC-specific registers are within the SENS block. Key fields:

### DAC Control Fields (within SENS registers)

| Field                | Register             | Description                      |
| -------------------- | -------------------- | -------------------------------- |
| `DAC_CW_EN`          | `SENS_SAR_DAC_CTRL1` | Enable cosine waveform generator |
| `DAC_CLK_INV`        | `SENS_SAR_DAC_CTRL1` | Invert DAC clock                 |
| `DAC_CLK_FORCE_LOW`  | `SENS_SAR_DAC_CTRL1` | Force DAC clock low              |
| `DAC_CLK_FORCE_HIGH` | `SENS_SAR_DAC_CTRL1` | Force DAC clock high             |

### Per-Channel Fields (within SENS_SAR_DAC_CTRL2 or related)

| Field                             | Description                                          |
| --------------------------------- | ---------------------------------------------------- |
| `DAC_DC1` / `DAC_DC2`             | 8-bit DC offset for CW generator (channel 1/2)       |
| `DAC_SCALE1` / `DAC_SCALE2`       | 2-bit amplitude scaling: 0=full, 1=1/2, 2=1/4, 3=1/8 |
| `DAC_INV1` / `DAC_INV2`           | 2-bit inversion: 0=none, 2=invert                    |
| `DAC_CW_PHASE1` / `DAC_CW_PHASE2` | Not a separate register; inversion controls phase    |

### RTC_IO Registers (for direct DAC output)

The direct-write DAC output values are controlled through RTC_IO registers:

| Register              | Description                                                       |
| --------------------- | ----------------------------------------------------------------- |
| `RTC_IO_PAD_DAC1_REG` | DAC channel 1: XPD (power), DAC output enable, 8-bit output value |
| `RTC_IO_PAD_DAC2_REG` | DAC channel 2: XPD (power), DAC output enable, 8-bit output value |

Key fields in `RTC_IO_PAD_DACx_REG`:
- `PDAC1_DAC` / `PDAC2_DAC`: 8-bit DAC output value
- `PDAC1_XPD_DAC` / `PDAC2_XPD_DAC`: DAC power enable
- `PDAC1_DAC_XPD_FORCE` / `PDAC2_DAC_XPD_FORCE`: Force DAC power state

### DMA Mode

When using DMA mode, DAC data is routed through the I2S peripheral. The I2S registers control the DMA transfer, and the DAC receives data from the I2S FIFO. This is configured via:
- `SENS_SAR_DAC_CTRL1.DAC_DIG_FORCE`: Enable digital (DMA) mode
- I2S peripheral registers for DMA configuration

**Approximate DAC-specific register count**: ~5-8 registers/fields (within the larger SENS and RTC_IO blocks)

## Source Code References

### SOC Register Definitions
- **Sensor register header**: [components/soc/esp32/register/soc/sens_reg.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/sens_reg.h)
  - Shared register file containing DAC control registers (`SENS_SAR_DAC_CTRL1`, `SENS_SAR_DAC_CTRL2`)
- **Sensor register struct**: [components/soc/esp32/register/soc/sens_struct.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/sens_struct.h)
  - C struct overlay including DAC fields

### HAL Layer
- **Low-level HAL**: [components/esp_hal_ana_conv/esp32/include/hal/dac_ll.h](https://github.com/espressif/esp-idf/blob/master/components/esp_hal_ana_conv/esp32/include/hal/dac_ll.h)
  - Direct register manipulation for DAC operations
  - Key functions: `dac_ll_update_output_value()`, `dac_ll_power_on()`, `dac_ll_cw_set_freq()`, `dac_ll_cw_set_scale()`, `dac_ll_cw_set_dc_offset()`
  - Shows that direct DAC output goes through `RTCIO.pad_dac[channel]`
  - CW generator configured via `SENS.sar_dac_ctrl1` and `SENS.sar_dac_ctrl2`

### API Documentation
- **API reference**: [DAC API Documentation](https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-reference/peripherals/dac.html)
  - High-level driver API
  - Key functions: `dac_oneshot_new_channel()`, `dac_oneshot_output_voltage()`, `dac_cosine_new_channel()`, `dac_continuous_new_channels()`

### Examples
- **Example code**: [examples/peripherals/dac](https://github.com/espressif/esp-idf/blob/master/examples/peripherals/dac)
  - Oneshot voltage output example
  - Cosine waveform generator example
  - Continuous (DMA) output example

### QEMU Implementation
- **No QEMU implementation exists** for the ESP32 DAC.

## Renode Implementation Analysis

### Reference Peripherals in Renode

1. **No direct DAC peripheral exists** in Renode's standard library for any platform that closely matches this architecture.
2. **STM32_DAC**: If one exists, it would show patterns for voltage output modeling, but the ESP32 DAC is simpler (just 8-bit value -> voltage).
3. **The SENS register block** implementation (shared with ADC and Touch) is the main architectural consideration.

### Recommended Approach

1. **Shared SENS block**: The DAC must be implemented as part of the SENS register block alongside ADC and Touch. See the ADC document for discussion of the shared register block approach.

2. **Direct write mode** (primary target):
   - Handle writes to `RTC_IO_PAD_DAC1_REG` and `RTC_IO_PAD_DAC2_REG`
   - Extract the 8-bit DAC value and power enable fields
   - Store the current output value per channel
   - Optionally: provide an API for test scripts to read the current DAC output voltage
   - Voltage calculation: `V_out = 3.3V * DAC_VALUE / 256`

3. **CW generator mode**:
   - Track CW enable, frequency step, amplitude scale, DC offset, and inversion per channel
   - For emulation, the CW generator does not need to produce a real-time waveform
   - Store the configuration for read-back
   - If a test needs to verify CW output, provide a query API that returns the configured waveform parameters

4. **Output value tracking**: Provide a mechanism for test infrastructure to observe the DAC output:
   - Log DAC value changes
   - Expose current output voltage via a Renode monitor command or Python API
   - This replaces the physical voltage measurement that would verify real hardware

5. **What to skip initially**:
   - DMA/continuous mode (requires I2S DMA integration)
   - Actual analog waveform generation
   - Output buffer amplifier modeling
   - CW generator real-time waveform production
   - RTC clock domain accuracy

### Validation Strategy

- Use ESP-IDF DAC oneshot example
- Verify: DAC channel powers on, value writes succeed, read-back is correct
- Use Renode test infrastructure to verify output value matches expected voltage

## Complexity Assessment

| Aspect                       | Rating                  | Notes                                                                            |
| ---------------------------- | ----------------------- | -------------------------------------------------------------------------------- |
| **Register count**           | Very Low                | ~5-8 DAC-specific registers/fields within SENS and RTC_IO blocks                 |
| **Logic complexity**         | Very Low                | Direct write is trivial (write value -> output). CW is config-only in emulation. |
| **Interrupt model**          | None                    | DAC has no interrupts in direct/CW mode (DMA mode uses I2S interrupts)           |
| **External dependencies**    | Medium                  | Shared SENS register block; DMA mode requires I2S integration                    |
| **Clock domain complexity**  | Low                     | CW generator uses RTC8M_CLK but only needs config storage                        |
| **DMA**                      | Medium (if implemented) | Continuous mode via I2S DMA; skip for initial implementation                     |
| **QEMU reference available** | **No**                  | No QEMU DAC implementation                                                       |
| **Overall effort**           | **Low**                 | Estimated 0.5-1 day for direct write mode (assuming SENS block exists)           |
| **Priority**                 | **Low**                 | Less commonly used than ADC; simple enough to add when SENS block is built       |

The DAC is the simplest peripheral in this analysis. Direct write mode is trivially simple -- write an 8-bit value to a register. The main implementation effort is shared with the ADC and Touch sensor via the SENS register block. Once the SENS block infrastructure exists, adding DAC support is minimal work. DMA mode adds significant complexity due to I2S dependency but can be deferred.
