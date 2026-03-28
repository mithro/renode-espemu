# ESP32 Temperature Sensor

## Overview

The ESP32 includes an on-chip temperature sensor that is part of the SAR (Successive Approximation Register) ADC peripheral, specifically within the "sensor" (SENS) register block. The temperature sensor (referred to as TSENS in the registers) measures the die temperature of the chip and is primarily intended for monitoring operating conditions rather than ambient temperature measurement. The sensor output is an 8-bit digital value that maps to a temperature range roughly from -40C to 125C, though accuracy is limited (typically +/- 1-2C after calibration).

On the original ESP32, the temperature sensor is a relatively simple and infrequently used peripheral. Unlike newer ESP32 variants (ESP32-S2, ESP32-S3, ESP32-C3), the original ESP32's temperature sensor has limited ESP-IDF support and is not commonly used in production applications. The sensor is accessed through the SAR ADC's sensor control registers rather than having a dedicated peripheral block. The measurement involves configuring the sensor, starting a conversion, waiting for completion, and reading the result.

The temperature sensor shares the SENS register block with other SAR ADC functions including the touch sensor controller, ADC1/ADC2 control, and ULP coprocessor ADC access. For emulation purposes, the temperature sensor is a small subset of the larger SENS peripheral. Implementing just the temperature sensor registers is straightforward, but awareness of the surrounding SAR ADC registers is important since firmware may access both in the same register space.

## Hardware Specifications

- **Register base address:** `0x3FF48800` (SENS/SAR base, size: `0x400`)
  - Temperature sensor registers are a subset within this block
- **Number of instances:** 1
- **Key capabilities:**
  - 8-bit temperature measurement output
  - Configurable measurement range via DAC offset and clock divider
  - Operating range: approximately -40C to 125C
  - Resolution: approximately 0.4C per LSB (varies with range setting)
  - Measurement time: configurable via clock divider, typically microseconds
- **Interrupt sources:** None dedicated (SAR ADC has interrupts, but TSENS typically uses polling)
- **DMA support:** None for temperature sensor specifically

## TRM Chapter Reference

- **ESP32 Technical Reference Manual** Chapter 29: SAR ADC Controllers (includes temperature sensor description)
  - [PDF link](https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf)

## Register Map Summary

The temperature sensor uses a small number of registers within the SENS (SAR ADC sensor) register block. Key registers (offsets from SENS base `0x3FF48800`):

### Temperature Sensor Specific Registers

| Offset  | Register                   | Key Fields                                                                                                                                                     | Purpose                                                                                |
| ------- | -------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| `0x004` | `SENS_SAR_MEAS_WAIT2_REG`  | `SENS_FORCE_XPD_SAR`                                                                                                                                           | Force SAR power on (needed for TSENS)                                                  |
| `0x04C` | `SENS_SAR_TSENS_CTRL_REG`  | `TSENS_XPD_WAIT`, `TSENS_XPD_FORCE`, `TSENS_CLK_DIV`, `TSENS_POWER_UP`, `TSENS_POWER_UP_FORCE`, `TSENS_DUMP_OUT`, `TSENS_IN_INV`, `TSENS_CLK_INV`, `TSENS_DAC` | Main temperature sensor control: power, clock, DAC offset, data inversion              |
| `0x050` | `SENS_SAR_I2C_CTRL_REG`    | Various                                                                                                                                                        | I2C interface to internal analog blocks (used for TSENS calibration on some revisions) |
| `0x06C` | `SENS_SAR_SLAVE_ADDR3_REG` | `SENS_TSENS_RDY_OUT`, `SENS_TSENS_OUT`                                                                                                                         | Temperature sensor output: 8-bit result and ready flag                                 |

### Context Registers (SAR ADC shared, may affect TSENS operation)

| Offset  | Register                  | Purpose                                        |
| ------- | ------------------------- | ---------------------------------------------- |
| `0x000` | `SENS_SAR_READ_CTRL_REG`  | SAR1 read control (clock div, sample cycles)   |
| `0x004` | `SENS_SAR_MEAS_WAIT2_REG` | SAR power control (affects TSENS power domain) |
| `0x048` | `SENS_SAR_MEAS_CTRL_REG`  | Overall measurement control                    |

### Key Bit Fields in SENS_SAR_TSENS_CTRL_REG (offset 0x04C)

| Bits    | Field                  | Purpose                                           |
| ------- | ---------------------- | ------------------------------------------------- |
| `31:24` | `TSENS_XPD_WAIT`       | Wait cycles after power-up before measurement     |
| `23`    | `TSENS_XPD_FORCE`      | Force temperature sensor power domain control     |
| `22`    | `TSENS_CLK_INV`        | Invert clock                                      |
| `21:14` | `TSENS_CLK_DIV`        | Clock divider for measurement timing              |
| `13`    | `TSENS_POWER_UP`       | Power up the temperature sensor (when FORCE=1)    |
| `12`    | `TSENS_POWER_UP_FORCE` | Use software control for power (vs hardware auto) |
| `11`    | `TSENS_DUMP_OUT`       | Start/trigger measurement                         |
| `10`    | `TSENS_IN_INV`         | Invert input                                      |
| `9:6`   | `TSENS_DAC`            | DAC offset value (affects measurement range)      |
| `5:0`   | Reserved               | --                                                |

### Key Bit Fields in SENS_SAR_SLAVE_ADDR3_REG (offset 0x06C)

| Bits    | Field                | Purpose                                              |
| ------- | -------------------- | ---------------------------------------------------- |
| `31`    | `SENS_TSENS_RDY_OUT` | Temperature measurement ready (read-only)            |
| `30:23` | `SENS_TSENS_OUT`     | 8-bit temperature value (read-only)                  |
| `22:0`  | Other fields         | I2C slave address configuration (unrelated to TSENS) |

## Source Code References

### ESP-IDF Register Definitions
- [`soc/sens_reg.h`](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/sens_reg.h) -- Complete SAR ADC / sensor register definitions including all TSENS fields

### ESP-IDF HAL (Low-Level Driver)

For the original ESP32, the temperature sensor does not have a dedicated HAL LL file in the same way as newer chips. Temperature sensor access is typically done through direct register manipulation or through the legacy API. On newer ESP32 variants, `temperature_sensor_ll.h` exists, but for the original ESP32, the access pattern is:
1. Power up the sensor: set `TSENS_POWER_UP_FORCE` and `TSENS_POWER_UP` in `SENS_SAR_TSENS_CTRL_REG`
2. Configure DAC offset and clock divider
3. Trigger measurement: set `TSENS_DUMP_OUT`
4. Wait for `SENS_TSENS_RDY_OUT` to be set
5. Read `SENS_TSENS_OUT` for the 8-bit result
6. Convert raw value to temperature using calibration formula

### ESP-IDF API Documentation

The temperature sensor API for the original ESP32 is limited. The unified `temperature_sensor` driver in newer ESP-IDF versions has partial ESP32 support. The legacy approach uses direct register access.

### ESP-IDF Examples
- [`examples/peripherals/temperature_sensor`](https://github.com/espressif/esp-idf/blob/master/examples/peripherals/temperature_sensor) -- Temperature sensor example (note: may target newer ESP32 variants primarily)

### Espressif QEMU Implementation

There is no dedicated temperature sensor model in Espressif's QEMU for the ESP32. The SAR ADC register block may be partially stubbed, but temperature sensor functionality is not specifically implemented. This is consistent with the peripheral's low priority -- it is not used during boot and is rarely used in typical applications on the original ESP32.

## Renode Implementation Analysis

### Existing Renode Model

No ESP32 temperature sensor or SAR ADC model exists in the main Renode repository.

### Recommended Renode Reference Peripherals

There is no closely matching temperature sensor peripheral in Renode's existing library. Relevant references:

- For a simple sensor with a single readable value, any Renode ADC model provides a pattern
- The implementation is simple enough that a reference peripheral is not strictly necessary
- `BasicDoubleWordPeripheral` with a few registers would suffice

### Implementation Approach

The temperature sensor can be implemented as part of a broader SENS/SAR ADC peripheral or as a standalone stub that only covers the TSENS-related registers.

**Standalone approach (recommended for initial implementation):**

1. Create a peripheral mapped at the SENS base address (`0x3FF48800`)
2. Implement the full register space as a `DoubleWordRegisterCollection` with most registers as simple storage
3. Add specific behavior for TSENS registers:
   - `SENS_SAR_TSENS_CTRL_REG`: Track power state and measurement trigger
   - `SENS_SAR_SLAVE_ADDR3_REG`: When TSENS is powered and triggered, set `TSENS_RDY_OUT=1` and provide a fixed or configurable `TSENS_OUT` value

**Registers that MUST be implemented for basic functionality:**
- `SENS_SAR_TSENS_CTRL_REG` -- Accept power-up and trigger writes
- `SENS_SAR_SLAVE_ADDR3_REG` -- Return ready flag and temperature value on read
- `SENS_SAR_MEAS_WAIT2_REG` -- Accept SAR power configuration writes

**Registers that can be stubbed (accept writes, return stored value):**
- All other SENS registers (SAR ADC, touch sensor, ULP-related)
- These must not fault on access since firmware may configure the entire SENS block

**Interrupts that need to work:**
- None -- temperature sensor is polled, not interrupt-driven

**DMA considerations:**
- None

**Temperature value behavior:**
- Return a fixed reasonable value (e.g., 128, corresponding to approximately 25C)
- Optionally allow configuration from Renode Python script or monitor command
- Always report `TSENS_RDY_OUT = 1` when sensor is powered and measurement is triggered (instant measurement)

**Estimated complexity:** Simple (3-4 meaningful registers, no complex state machine, no interrupts)

### Key Firmware Interactions

**During boot:**
The temperature sensor is NOT accessed during boot. It is not part of the critical boot path for the original ESP32.

**During application runtime (if used):**
1. Firmware powers up the sensor by writing `SENS_SAR_TSENS_CTRL_REG`
2. Firmware configures DAC offset for desired range
3. Firmware triggers measurement by setting `TSENS_DUMP_OUT`
4. Firmware polls `SENS_TSENS_RDY_OUT` in `SENS_SAR_SLAVE_ADDR3_REG`
5. Firmware reads `SENS_TSENS_OUT` for the 8-bit raw value
6. Firmware converts raw value to degrees Celsius using a calibration formula

**Critical register accesses:**
- Writes to the SENS register space must not fault (other SAR ADC functions share this space)
- If firmware reads the temperature sensor, it will poll `TSENS_RDY_OUT` -- this must eventually become 1 or firmware will hang in a polling loop
- The 8-bit temperature value does not need to be physically accurate; any reasonable value works

**Interaction with other SENS functions:**
The SAR ADC register space is also used by:
- ADC1 and ADC2 (analog-to-digital conversion)
- Touch sensor controller (capacitive touch pad scanning)
- ULP coprocessor ADC access
If any of these are implemented, they will share the same register base address and care must be taken to avoid conflicts.

## Complexity Assessment

- **Estimated difficulty:** Simple
- **Estimated register count:** 3-4 TSENS-specific registers (within a larger ~50 register SENS block that should accept writes)
- **Dependencies:** None for TSENS itself. The broader SENS block is related to SAR ADC and touch sensor but TSENS operates independently.
- **Priority:** Nice to have -- the temperature sensor is not used during boot on the original ESP32 and is rarely used in production applications. However, the SENS register block must accept accesses without faulting since firmware may configure SAR ADC for other purposes (ADC readings, touch sensor). A minimal stub of the entire SENS register space is more important than accurate temperature sensor modeling.
