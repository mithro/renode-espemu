# ESP32 ADC (Analog-to-Digital Converter)

## Overview

The ESP32 contains two SAR (Successive Approximation Register) ADCs that convert analog voltages on GPIO pins to 12-bit digital values. ADC1 has 8 channels (GPIO32-39) and ADC2 has 10 channels (various GPIOs). The ADCs support single-shot (oneshot) mode and continuous (DMA) mode, with configurable attenuation for different voltage ranges.

A critical constraint is that ADC2 is shared with the Wi-Fi driver -- when Wi-Fi is active, ADC2 channels cannot be used by application code. This sharing is managed through software arbitration.

The ADC registers are part of the SAR (Sensor) register block (`SENS`), which is shared with the DAC and touch sensor peripherals.

## Hardware Specifications

- **ADC count**: 2 (SAR ADC1, SAR ADC2)
- **Resolution**: 12-bit (configurable: 9, 10, 11, or 12 bits)
- **ADC1 channels**: 8 channels
  - Channel 0: GPIO36 (VP)
  - Channel 1: GPIO37
  - Channel 2: GPIO38
  - Channel 3: GPIO39 (VN)
  - Channel 4: GPIO32
  - Channel 5: GPIO33
  - Channel 6: GPIO34
  - Channel 7: GPIO35
- **ADC2 channels**: 10 channels
  - Channel 0: GPIO4
  - Channel 1: GPIO0
  - Channel 2: GPIO2
  - Channel 3: GPIO15
  - Channel 4: GPIO13
  - Channel 5: GPIO12
  - Channel 6: GPIO14
  - Channel 7: GPIO27
  - Channel 8: GPIO25
  - Channel 9: GPIO26
- **Attenuation settings** (per channel):
  - 0 dB: 100-950 mV range
  - 2.5 dB: 100-1250 mV range
  - 6 dB: 150-1750 mV range
  - 11 dB: 150-2450 mV range
- **Conversion modes**:
  - One-shot (single conversion, software triggered)
  - Continuous (DMA-driven, repeated conversions)
- **Clock**: SAR ADC clock derived from APB_CLK, configurable divider
- **Conversion time**: ~2 microseconds per sample (at 12-bit)
- **DMA support**: Via I2S DMA for continuous mode (ADC data routed through I2S peripheral)
- **Internal voltage reference**: ~1.1V (with per-chip calibration stored in eFuse)
- **Base address**: `0x3FF48800` (SENS register block, shared with DAC and Touch)
- **Register block size**: `0x400`

## TRM Chapter Reference

- **Chapter 27: On-Chip Sensors and Analog Signal Processing (SAR ADC section)**
- Source: [ESP32 Technical Reference Manual](https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf)
- Key sections:
  - 27.3 SAR ADC
  - 27.3.1 Overview
  - 27.3.2 Functional Description (oneshot mode, pattern table, continuous mode, DMA)
  - 27.3.3 ADC configuration registers

## Register Map Summary

ADC registers are within the SENS (Sensor) register block at base `0x3FF48800`. Note that this block is shared with DAC and touch sensor registers.

### ADC1 Registers

| Register | Offset | Description |
|----------|--------|-------------|
| `SENS_SAR_READ_CTRL` | 0x0000 | ADC1 config: SAR clock divider, sample bit width, data inversion |
| `SENS_SAR_READ_STATUS1` | 0x0004 | ADC1 read status (read-only) |
| `SENS_SAR_MEAS_WAIT1` | 0x000C | Measurement wait/timing config (part 1) |
| `SENS_SAR_MEAS_WAIT2` | 0x0010 | Measurement wait/timing config (part 2) |
| `SENS_SAR_MEAS_CTRL` | 0x0014 | Measurement control (XPD SAR, force control) |
| `SENS_SAR_MEAS_START1` | 0x0054 | ADC1 measurement start: channel select (SAR1_EN_PAD), start trigger |
| `SENS_SAR_TOUCH_CTRL1` | 0x0058 | Touch/SAR control (shared register) |
| `SENS_SAR_ATTEN1` | 0x0034 | ADC1 per-channel attenuation (2 bits per channel, 8 channels = 16 bits) |

### ADC2 Registers

| Register | Offset | Description |
|----------|--------|-------------|
| `SENS_SAR_READ_CTRL2` | 0x0090 | ADC2 config: SAR clock divider, sample bit width, data inversion |
| `SENS_SAR_READ_STATUS2` | 0x0094 | ADC2 read status (read-only) |
| `SENS_SAR_MEAS_START2` | 0x0098 | ADC2 measurement start: channel select (SAR2_EN_PAD), start trigger |
| `SENS_SAR_ATTEN2` | 0x009C | ADC2 per-channel attenuation (2 bits per channel, 10 channels = 20 bits) |

### Shared/Control Registers

| Register | Offset | Description |
|----------|--------|-------------|
| `SENS_SAR_SLAVE_ADDR1-4` | 0x0018-0x0024 | I2C-like slave addresses for internal analog control |
| `SENS_SAR_PATT_TAB1-4` | 0x0028-0x0030 | ADC1 pattern table (DMA mode: channel + attenuation sequence) |
| `SENS_SAR_PATT_TAB5-8` | various | ADC2 pattern table |
| `SENS_SAR_I2C_CTRL` | 0x0038 | Internal I2C bus control (for analog calibration) |
| `SENS_SAR_MEM_WR_CTRL` | 0x003C | Memory write control for DMA |
| `SENS_SAR_NOUSE` | 0x00F8 | Reserved/no use |

### Key Data Flow

1. **One-shot mode (ADC1)**:
   - Write channel to `SAR_MEAS_START1.SAR1_EN_PAD`
   - Set `SAR_MEAS_START1.MEAS1_START_SAR = 1`
   - Poll `SAR_MEAS_START1.MEAS1_DONE_SAR` until 1
   - Read result from `SAR_MEAS_START1.MEAS1_DATA_SAR`

2. **One-shot mode (ADC2)**:
   - Similar flow via `SAR_MEAS_START2` register
   - Must check WiFi arbitration before using

**Approximate ADC-related register count**: ~25-30 registers (within the larger SENS block)

## Source Code References

### SOC Register Definitions
- **Register header**: [components/soc/esp32/register/soc/sens_reg.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/sens_reg.h)
  - Shared register file for SAR ADC, DAC, and Touch sensor
  - Contains all SENS_SAR_* register definitions
- **Register struct**: [components/soc/esp32/register/soc/sens_struct.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/sens_struct.h)
  - C struct overlay for the SENS register block

### HAL Layer
- **Low-level HAL**: [components/esp_hal_ana_conv/esp32/include/hal/adc_ll.h](https://github.com/espressif/esp-idf/blob/master/components/esp_hal_ana_conv/esp32/include/hal/adc_ll.h)
  - Direct register manipulation for ADC operations
  - Key functions: `adc_ll_set_controller()`, `adc_ll_set_atten()`, `adc_oneshot_ll_start()`, `adc_oneshot_ll_get_raw_result()`, `adc_ll_set_pattern_table()`

### API Documentation
- **API reference**: [ADC Oneshot API Documentation](https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-reference/peripherals/adc_oneshot.html)
  - Recommended oneshot driver API
  - Key functions: `adc_oneshot_new_unit()`, `adc_oneshot_config_channel()`, `adc_oneshot_read()`

### Examples
- **Example code**: [examples/peripherals/adc](https://github.com/espressif/esp-idf/blob/master/examples/peripherals/adc)
  - Oneshot read example
  - Continuous read example
  - ADC DMA example

### Existing Renode Reference
- **STM32 ADC**: [STM32_ADC.cs](https://github.com/renode/renode-infrastructure/blob/master/src/Emulator/Peripherals/Peripherals/Analog/STM32_ADC.cs)
  - Renode's existing STM32 ADC implementation
  - Shows patterns for: register layout, channel selection, conversion triggering, data output, interrupt generation
  - Useful architectural reference even though the register map differs

### QEMU Implementation
- **No QEMU implementation exists** for the ESP32 ADC.

## Renode Implementation Analysis

### Reference Peripherals in Renode

1. **STM32_ADC.cs**: The most relevant reference. Shows how to model ADC conversion in Renode:
   - Channel multiplexing
   - Conversion start trigger and done flag
   - Result register
   - Interrupt on conversion complete
   - External voltage injection via Renode test infrastructure
2. **BasicDoubleWordPeripheral**: Base class for register interface.

### Recommended Approach

1. **SENS register block**: The ADC shares the SENS register block with DAC and Touch sensor. Implementation options:
   - **Option A**: Single large `SENS` peripheral class covering ADC + DAC + Touch
   - **Option B**: Separate peripheral classes that share an address range (more complex but more modular)
   - **Recommended**: Option A for initial implementation, refactor later if needed

2. **ADC1 one-shot mode** (primary target):
   - Implement the channel selection register (`SAR1_EN_PAD`)
   - On start trigger (`MEAS1_START_SAR`), immediately:
     - Set `MEAS1_DONE_SAR` flag
     - Write conversion result to `MEAS1_DATA_SAR`
   - Return a configurable value (default: mid-range, ~2048 for 12-bit)
   - Allow test scripts to set per-channel voltage values that produce corresponding ADC readings

3. **ADC2 one-shot mode**: Same as ADC1 but using `SAR_MEAS_START2` register.

4. **Attenuation modeling**: Store attenuation configuration per channel. When converting voltage to ADC value, apply the attenuation scale factor:
   - 0 dB: V_in / 1.0V * 4095
   - 2.5 dB: V_in / 1.33V * 4095
   - 6 dB: V_in / 2.0V * 4095
   - 11 dB: V_in / 3.55V * 4095 (approximately)

5. **External value injection**: Provide an API for test scripts to set analog input values per channel, similar to how STM32_ADC handles it. This is essential for meaningful ADC emulation.

6. **What to skip initially**:
   - Continuous/DMA mode (requires I2S DMA integration)
   - Pattern table for multi-channel scanning
   - Internal I2C for analog calibration
   - ADC2/WiFi arbitration
   - Detailed timing simulation
   - Internal voltage reference modeling

### Validation Strategy

- Use ESP-IDF ADC oneshot example
- Configure test to inject known voltage values
- Verify: ADC configuration succeeds, oneshot read returns expected values, attenuation affects readings

## Complexity Assessment

| Aspect | Rating | Notes |
|--------|--------|-------|
| **Register count** | Medium | ~25-30 ADC registers within shared SENS block (~100 total SENS registers) |
| **Logic complexity** | Low-Medium | One-shot is simple (trigger -> return value). Continuous/DMA is complex. |
| **Interrupt model** | Low | Minimal interrupt support for one-shot mode |
| **External dependencies** | Medium | Shared SENS register block with DAC and Touch; DMA via I2S for continuous mode |
| **Clock domain complexity** | Low | SAR clock divider is straightforward |
| **DMA** | Medium (if implemented) | Continuous mode requires I2S DMA path; skip for initial implementation |
| **QEMU reference available** | **No** | No QEMU ADC, but Renode has STM32_ADC reference |
| **Overall effort** | **Medium** | Estimated 2-3 days for one-shot mode; +3-4 days for continuous/DMA |
| **Priority** | **Medium-High** | Commonly used peripheral; one-shot mode is high value, continuous mode is lower priority |

The ADC is moderately complex with the main challenge being the shared SENS register block. One-shot mode is straightforward to implement and covers the majority of use cases. Continuous/DMA mode is significantly more complex due to the I2S DMA dependency and can be deferred. The Renode STM32_ADC provides a useful architectural reference for value injection and conversion modeling.
