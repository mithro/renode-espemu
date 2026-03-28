# ESP32 PCNT (Pulse Counter)

## Overview

The PCNT (Pulse Counter) peripheral counts rising and/or falling edges on input signals, with optional direction control via a second input signal. It is used for rotary encoder decoding, frequency measurement, event counting, and quadrature decoding. Each unit has two channels that can independently contribute to a shared 16-bit signed counter, allowing full quadrature decoding from a single unit.

The PCNT is a relatively simple peripheral focused on counting logic with configurable edge/level actions and threshold-based event generation.

## Hardware Specifications

- **Unit count**: 8 independent counter units (PCNT_U0 through PCNT_U7)
- **Channels per unit**: 2 (channel 0 and channel 1)
- **Counter width**: 16-bit signed (-32768 to +32767)
- **Input signals per channel**: 2 (signal edge input + control level input)
  - Total GPIO inputs: up to 32 (8 units x 2 channels x 2 signals)
- **Edge actions** (configurable per edge type per channel):
  - Increment counter
  - Decrement counter
  - Hold (no change)
- **Level actions** (control signal modifies counting behavior):
  - Keep (edge action unchanged)
  - Reverse (edge action inverted)
  - Hold (disable counting)
- **Watchpoints**:
  - High limit (configurable threshold, triggers event when reached)
  - Low limit (configurable threshold, triggers event when reached)
  - 2 configurable threshold values per unit (threshold 0 and threshold 1)
  - Zero crossing detection
- **Filter**: Glitch filter on input signals (10-bit filter value, filters pulses shorter than N APB_CLK cycles)
- **Interrupts**: Per-unit events for high limit, low limit, threshold 0, threshold 1, zero crossing
- **Base address**: `0x3FF57000`
- **Register block size**: `0x1000`

## TRM Chapter Reference

- **Chapter 16: Pulse Count Controller (PCNT)**
- Source: [ESP32 Technical Reference Manual](https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf)
- Key sections:
  - 16.1 Overview
  - 16.2 Functional Description (counter operation, channel configuration, filter, watchpoints)
  - 16.3 Interrupts
  - 16.4 Register Summary and Description

## Register Map Summary

### Per-Unit Configuration Registers (x8 units)

| Register | Offset | Description |
|----------|--------|-------------|
| `PCNT_Ux_CONF0` | 0x0000 + x*0x0C | Unit x config 0: channel 0/1 edge actions, level actions, threshold enable, filter threshold |
| `PCNT_Ux_CONF1` | 0x0004 + x*0x0C | Unit x config 1: high limit value, low limit value |
| `PCNT_Ux_CONF2` | 0x0008 + x*0x0C | Unit x config 2: threshold 0 value, threshold 1 value |

### Per-Unit Status Registers (x8 units)

| Register | Offset | Description |
|----------|--------|-------------|
| `PCNT_Ux_CNT` | 0x0060 + x*4 | Unit x current counter value (16-bit signed, read-only) |
| `PCNT_Ux_STATUS` | 0x0080 + x*4 | Unit x status: zero crossing, threshold 0/1 reached, high/low limit reached, zero latched |

### Control Registers

| Register | Offset | Description |
|----------|--------|-------------|
| `PCNT_CTRL` | 0x00B0 | Global control: per-unit counter pause, per-unit counter reset, per-unit clock enable |

### Interrupt Registers

| Register | Offset | Description |
|----------|--------|-------------|
| `PCNT_INT_RAW` | 0x00A0 | Raw interrupt status (1 bit per unit, bit x = unit x event) |
| `PCNT_INT_ST` | 0x00A4 | Masked interrupt status |
| `PCNT_INT_ENA` | 0x00A8 | Interrupt enable |
| `PCNT_INT_CLR` | 0x00AC | Interrupt clear (write-1-to-clear) |

### Configuration Details for PCNT_Ux_CONF0

The CONF0 register is densely packed with per-channel configuration:

- **Channel 0**: Positive edge action (2 bits), negative edge action (2 bits), high level action (2 bits), low level action (2 bits)
- **Channel 1**: Same as channel 0 but different bit positions
- **Filter threshold**: 10-bit value (number of APB_CLK cycles to filter)
- **Filter enable**: 1 bit
- **Threshold 0/1 event enable**: 1 bit each

**Approximate register count**: ~35 registers

## Source Code References

### SOC Register Definitions
- **Register header**: [components/soc/esp32/register/soc/pcnt_reg.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/pcnt_reg.h)
  - All register addresses, field masks, and bit positions
- **Register struct**: [components/soc/esp32/register/soc/pcnt_struct.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/pcnt_struct.h)
  - C struct overlay for register access

### HAL Layer
- **Low-level HAL**: [components/esp_hal_pcnt/esp32/include/hal/pcnt_ll.h](https://github.com/espressif/esp-idf/blob/master/components/esp_hal_pcnt/esp32/include/hal/pcnt_ll.h)
  - Direct register manipulation functions
  - Key functions: `pcnt_ll_set_edge_action()`, `pcnt_ll_set_level_action()`, `pcnt_ll_get_count()`, `pcnt_ll_clear_count()`, `pcnt_ll_set_glitch_filter_thres()`, `pcnt_ll_set_watch_point_value()`

### API Documentation
- **API reference**: [PCNT API Documentation](https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-reference/peripherals/pcnt.html)
  - High-level driver API
  - Key concepts: `pcnt_new_unit()`, `pcnt_new_channel()`, `pcnt_unit_add_watch_point()`, `pcnt_unit_start()`

### Examples
- **Example code**: [examples/peripherals/pcnt](https://github.com/espressif/esp-idf/blob/master/examples/peripherals/pcnt)
  - Rotary encoder example
  - Basic pulse counting example

### QEMU Implementation
- **No QEMU implementation exists** for the PCNT peripheral.

## Renode Implementation Analysis

### Reference Peripherals in Renode

There is no direct pulse counter peripheral in Renode's existing library. Potentially relevant:

1. **GPIO-connected peripherals**: PCNT is fundamentally GPIO-driven. Renode's GPIO modeling infrastructure (IGPIOReceiver) can be used to receive signal edges.
2. **Timer peripherals with capture**: Some Renode timer implementations support input capture, which shares the concept of reacting to external signal edges.
3. **BasicDoubleWordPeripheral**: Standard base for the register interface.

### Recommended Approach

1. **Register scaffold**: Implement all ~35 registers. This is a small register set and straightforward to model.

2. **Counter core**: Each of the 8 units needs:
   - A 16-bit signed counter (internally track as `short` / `Int16`)
   - Pause and reset control via the `PCNT_CTRL` register
   - Counter value readable via `PCNT_Ux_CNT`

3. **Channel input modeling**: Each channel has two inputs (signal + control):
   - Implement as `IGPIOReceiver` inputs (32 total)
   - On a signal edge (rising/falling), look up the configured action and the current control level
   - Apply the action: increment, decrement, or hold
   - This is the core logic and is straightforward lookup-table behavior

4. **Watchpoint/event detection**: After each counter update:
   - Check if counter reached high limit or low limit (auto-clear counter on limit if configured)
   - Check if counter crossed threshold 0 or threshold 1 values
   - Check for zero crossing
   - Set corresponding status bits and fire interrupt if enabled

5. **Glitch filter**: Can be approximated or skipped for initial implementation. In emulation, input signals are clean digital edges, so glitch filtering is rarely meaningful. Store the filter configuration for read-back but do not apply actual timing-based filtering.

6. **Input injection for testing**:
   - Expose GPIO inputs so test scripts can toggle them to simulate encoder signals
   - Renode's GPIO infrastructure allows programmatic edge injection from Python scripts or machine files

7. **What to skip initially**:
   - Glitch filter timing (store config, do not apply)
   - Cycle-accurate counter timing (count synchronously on edge)

### Validation Strategy

- Use ESP-IDF PCNT rotary encoder example
- Inject simulated quadrature signals via Renode GPIO scripting
- Verify: counter increments/decrements correctly, watchpoint events fire, interrupts generated

## Complexity Assessment

| Aspect | Rating | Notes |
|--------|--------|-------|
| **Register count** | Low | ~35 registers, highly regular |
| **Logic complexity** | Low-Medium | Simple counting logic with configurable actions; watchpoint detection |
| **Interrupt model** | Low | 8 interrupt sources (1 per unit), standard pattern |
| **External dependencies** | Medium | Requires GPIO input infrastructure for meaningful operation |
| **Clock domain complexity** | None | Counter is event-driven, not clock-driven |
| **DMA** | None | PCNT does not use DMA |
| **QEMU reference available** | **No** | Must implement from TRM and HAL code analysis |
| **Overall effort** | **Low-Medium** | Estimated 1-2 days |
| **Priority** | **Medium** | Used for rotary encoders and event counting; simpler than LEDC/RMT |

The PCNT is one of the simpler peripherals to emulate. The core logic is a configurable counter with edge/level action tables, which is straightforward to implement. The main consideration is properly integrating with Renode's GPIO infrastructure to receive input signals. This peripheral would be a good early implementation target due to its simplicity.
