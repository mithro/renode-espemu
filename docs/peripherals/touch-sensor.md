# ESP32 Touch Sensor (Capacitive Touch)

## Overview

The ESP32 includes a capacitive touch sensor controller that measures charge/discharge cycles on 10 touch-capable GPIO pads. The controller detects changes in capacitance caused by a human finger approaching or touching the pad, enabling touch buttons, sliders, and proximity detection without external components.

The touch sensor works by counting the charge/discharge cycles of an RC circuit (formed by the touch pad capacitance and internal reference) within a configurable measurement window. When a finger touches the pad, the capacitance increases, causing fewer charge cycles in the measurement period (lower count = touch detected).

The touch sensor registers reside within the shared SENS (Sensor) register block alongside the ADC and DAC peripherals.

## Hardware Specifications

- **Touch pad count**: 10 (T0 through T9)
  - T0: GPIO4
  - T1: GPIO0
  - T2: GPIO2
  - T3: GPIO15 (MTDO)
  - T4: GPIO13 (MTCK)
  - T5: GPIO12 (MTDI)
  - T6: GPIO14 (MTMS)
  - T7: GPIO27
  - T8: GPIO33
  - T9: GPIO32
- **Measurement principle**: Charge/discharge cycle counting
  - Measures number of charge/discharge cycles in a fixed time window
  - Untouched pad: higher count (lower capacitance = faster cycles)
  - Touched pad: lower count (higher capacitance = slower cycles)
- **Measurement output**: 16-bit cycle count per pad
- **Measurement modes**:
  - Software-triggered single measurement
  - Timer-triggered periodic measurement (FSM-driven)
  - Sleep mode measurement (ULP coprocessor or FSM)
- **Threshold**: Configurable 16-bit touch threshold per pad for interrupt generation
- **Interrupt**: Touch detection interrupt when count drops below threshold
- **Wake from sleep**: Touch pads can wake the ESP32 from light/deep sleep
- **Charge/discharge parameters**:
  - Slope (charge speed): Configurable
  - Tie option (charge/discharge reference): Configurable
  - Measurement duration: Configurable via `SENS_TOUCH_MEAS_TIME` (16-bit)
  - Sleep cycle: Configurable via `SENS_TOUCH_SLEEP_CYCLES` (16-bit)
- **FSM (Finite State Machine)**: Hardware FSM that automatically sequences through enabled touch pads
- **Base address**: `0x3FF48800` (SENS register block, shared with ADC and DAC)

## TRM Chapter Reference

- **Chapter 28: Capacitive Touch Sensor**
- Source: [ESP32 Technical Reference Manual](https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf)
- Key sections:
  - 28.1 Overview
  - 28.2 Functional Description (measurement principle, touch FSM, operation modes)
  - 28.3 Touch Sensor Interrupts
  - 28.4 Register Summary and Description

## Register Map Summary

Touch sensor registers are within the SENS register block at base `0x3FF48800`, shared with ADC and DAC.

### Touch Configuration Registers

| Register | Offset | Description |
|----------|--------|-------------|
| `SENS_SAR_TOUCH_CTRL1` | 0x0058 | Touch control 1: measurement time, sleep cycle, clock source |
| `SENS_SAR_TOUCH_CTRL2` | 0x005C | Touch control 2: measurement enable, FSM mode, start trigger |
| `SENS_SAR_TOUCH_ENABLE` | 0x008C | Per-pad enable bitmask (10 bits for T0-T9) |

### Touch Threshold Registers (per pad)

| Register | Offset | Description |
|----------|--------|-------------|
| `SENS_SAR_TOUCH_THRES1` | 0x0060 | Threshold for T0, T1 (16 bits each) |
| `SENS_SAR_TOUCH_THRES2` | 0x0064 | Threshold for T2, T3 |
| `SENS_SAR_TOUCH_THRES3` | 0x0068 | Threshold for T4, T5 |
| `SENS_SAR_TOUCH_THRES4` | 0x006C | Threshold for T6, T7 |
| `SENS_SAR_TOUCH_THRES5` | 0x0070 | Threshold for T8, T9 |

### Touch Measurement Data Registers (read-only)

| Register | Offset | Description |
|----------|--------|-------------|
| `SENS_SAR_TOUCH_OUT1` | 0x0074 | Measurement data for T0, T1 (16 bits each) |
| `SENS_SAR_TOUCH_OUT2` | 0x0078 | Measurement data for T2, T3 |
| `SENS_SAR_TOUCH_OUT3` | 0x007C | Measurement data for T4, T5 |
| `SENS_SAR_TOUCH_OUT4` | 0x0080 | Measurement data for T6, T7 |
| `SENS_SAR_TOUCH_OUT5` | 0x0084 | Measurement data for T8, T9 |

### Touch Status Registers

| Register | Offset | Description |
|----------|--------|-------------|
| `SENS_SAR_TOUCH_STATUS0` | 0x0088 | Touch FSM status: currently scanning pad, measurement done |
| `SENS_SAR_TOUCH_DETECT` | (within status) | Touch detection bitmask: which pads are in touch state |

### Configuration Fields Detail

Key fields in `SENS_SAR_TOUCH_CTRL1`:
- `TOUCH_MEAS_DELAY`: 16-bit measurement time (in RTC_SLOW_CLK cycles)
- `TOUCH_SLEEP_CYCLES`: 16-bit sleep cycle count between measurements
- `TOUCH_XPD_BIAS`: Bias circuit power
- `TOUCH_OUT_SEL`: Output mode (raw count vs. smooth filtered)

Key fields in `SENS_SAR_TOUCH_CTRL2`:
- `TOUCH_MEAS_EN`: Per-pad measurement enable (10 bits)
- `TOUCH_START_FSM_EN`: Enable FSM-driven measurement
- `TOUCH_START`: Manual start trigger (software mode)
- `TOUCH_MEAS_DONE`: Measurement complete flag
- `TOUCH_START_EN`: Start enable
- `TOUCH_START_FORCE`: Force start mode (software vs FSM)

### Interrupt Registers

Touch interrupts are routed through the RTC interrupt system:
- `RTC_CNTL_INT_RAW`: Contains `TOUCH_INT_RAW` bit
- `RTC_CNTL_INT_ST`: Masked interrupt status
- `RTC_CNTL_INT_ENA`: Interrupt enable
- `RTC_CNTL_INT_CLR`: Interrupt clear

The touch interrupt fires when any enabled pad's measurement count drops below its configured threshold.

**Approximate touch-specific register count**: ~15-18 registers/fields (within the larger SENS block)

## Source Code References

### SOC Register Definitions
- **Sensor register header**: [components/soc/esp32/register/soc/sens_reg.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/sens_reg.h)
  - Shared register file containing all `SENS_SAR_TOUCH_*` register definitions
- **Sensor register struct**: [components/soc/esp32/register/soc/sens_struct.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/sens_struct.h)
  - C struct overlay including touch sensor fields

### API Documentation
- **API reference**: [Touch Sensor API Documentation](https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-reference/peripherals/cap_touch_sens.html)
  - Touch sensor driver API
  - Key concepts: touch pad configuration, threshold setting, callback registration
  - Note: The ESP32 uses the legacy touch sensor API; newer chips use the cap_touch_sens API

### Examples
- **Example code**: [examples/peripherals/touch_sensor](https://github.com/espressif/esp-idf/blob/master/examples/peripherals/touch_sensor)
  - Basic touch pad read example
  - Touch pad interrupt example
  - Touch pad wake-up from deep sleep example

### QEMU Implementation
- **No QEMU implementation exists** for the ESP32 touch sensor.

## Renode Implementation Analysis

### Reference Peripherals in Renode

There is no capacitive touch sensor peripheral in Renode's existing library. The touch sensor is somewhat unique in the peripheral ecosystem. Relevant references:

1. **ADC modeling**: The touch sensor is conceptually similar to an ADC -- it converts a physical quantity (capacitance) to a digital value (cycle count). The value injection approach from ADC emulation applies directly.
2. **GPIO-based sensors**: Various Renode sensor models that accept input values from test scripts provide patterns for external value injection.
3. **SENS register block**: The touch sensor shares the register block with ADC and DAC, so it will be part of the same peripheral implementation.

### Recommended Approach

1. **Shared SENS block**: Touch sensor registers must be implemented within the SENS register block alongside ADC and DAC. See the ADC document for shared block discussion.

2. **Measurement value storage**:
   - Maintain a 16-bit measurement value per touch pad (10 pads)
   - Default "untouched" value: a high cycle count (e.g., 1000-2000, representing no capacitance change)
   - Provide test infrastructure to set per-pad values to simulate touch/no-touch states

3. **Software-triggered measurement**:
   - When `TOUCH_START` is written (software mode):
     - Set `TOUCH_MEAS_DONE` flag after brief delay (or immediately)
     - Copy current pad values to the output registers (`SAR_TOUCH_OUTx`)
   - This covers the basic polling use case

4. **FSM-driven measurement**:
   - When `TOUCH_START_FSM_EN` is set:
     - Periodically cycle through enabled pads
     - Update output registers with current values
     - Check each pad against its threshold
     - If any pad's count < threshold, set touch detection bit and fire interrupt
   - Model with a Renode periodic timer at the configured measurement rate

5. **Touch state injection API**:
   - `SetTouchPadValue(int pad, ushort value)` -- set the raw cycle count for a pad
   - `SimulateTouch(int pad)` -- set the pad value below its threshold (simulate finger)
   - `SimulateRelease(int pad)` -- restore the pad value above its threshold
   - This is the key interface for test scripts

6. **Interrupt generation**: Touch interrupt goes through the RTC interrupt controller, which adds a dependency. For initial implementation, can wire directly to the CPU interrupt line.

7. **What to skip initially**:
   - Wake from deep sleep (requires RTC/sleep state modeling)
   - Denoise/filter algorithms
   - Slope/charge current calibration
   - Waterproof features (ESP32-S2+, not original ESP32)
   - Actual capacitance simulation

### Validation Strategy

- Use ESP-IDF touch pad read example
- Inject "touch" and "release" states via Renode test scripts
- Verify: touch pad initialization succeeds, measurement reads return injected values, threshold crossing generates interrupt

## Complexity Assessment

| Aspect | Rating | Notes |
|--------|--------|-------|
| **Register count** | Low | ~15-18 touch-specific registers/fields within SENS block |
| **Logic complexity** | Low-Medium | Measurement FSM adds moderate complexity; basic read is simple |
| **Interrupt model** | Low-Medium | Single interrupt through RTC controller; threshold comparison per pad |
| **External dependencies** | Medium | Shared SENS register block; RTC interrupt controller dependency |
| **Clock domain complexity** | Low-Medium | Uses RTC_SLOW_CLK for measurement timing |
| **DMA** | None | Touch sensor does not use DMA |
| **QEMU reference available** | **No** | No QEMU touch sensor implementation |
| **Overall effort** | **Low-Medium** | Estimated 1-2 days (assuming SENS block exists) |
| **Priority** | **Low** | Specialized input method; lower priority than ADC/DAC for general emulation |

The touch sensor has moderate complexity primarily due to the FSM-driven measurement mode and RTC interrupt integration. The basic polling mode is simple to implement. The main value of touch sensor emulation is enabling test scripts to simulate user touch input for UI-oriented applications. Since it shares the SENS register block with ADC and DAC, it makes sense to implement all three together.
