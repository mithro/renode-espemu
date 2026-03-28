# ESP32 MCPWM (Motor Control PWM)

## Overview

The MCPWM (Motor Control Pulse Width Modulation) peripheral is a specialized PWM generator designed for motor control applications, power converter control, and other industrial uses. It provides complementary PWM outputs with dead-time insertion, hardware fault protection (trip zones), synchronization between timers, and input capture for measuring external signals.

The ESP32 has two independent MCPWM units (MCPWM0 and MCPWM1), each containing 3 timer-operator pairs capable of generating 3 complementary PWM signal pairs (6 outputs total per unit, 12 outputs across both units).

## Hardware Specifications

- **Unit count**: 2 independent MCPWM units (MCPWM0, MCPWM1)
- **Per unit**:
  - **Timers**: 3 (timer 0, 1, 2)
  - **Operators**: 3 (operator 0, 1, 2), each bound to a timer
  - **PWM outputs**: 6 (3 pairs of complementary outputs: PWMxA + PWMxB per operator)
  - **Capture channels**: 3 (independent input capture timers)
  - **Fault signals**: 3 (input pins for hardware fault/trip detection)
  - **Sync signals**: 3 (for timer synchronization)
- **Timer width**: 16-bit counter
- **Timer modes**: Up-count, down-count, up-down (center-aligned)
- **Clock source**: APB_CLK (80 MHz) with 8-bit prescaler (operator + timer = 16-bit total prescaling)
- **Dead time generator**: Per operator, configurable rising-edge and falling-edge delay (16-bit counters)
- **Carrier modulation**: Optional high-frequency carrier for transformer-coupled gate drivers
- **Fault handling**: Hardware-triggered PWM output override on fault pin assertion
- **Capture**: 32-bit capture timer with edge-triggered capture on 3 input channels
- **Interrupts**: Rich interrupt set per unit (timer events, compare events, fault events, capture events)
- **Base addresses**:
  - MCPWM0: `0x3FF5E000`
  - MCPWM1: `0x3FF6C000`
- **Register block size**: `0x1000` per unit

## TRM Chapter Reference

- **Chapter 18: Motor Control PWM (MCPWM)**
- Source: [ESP32 Technical Reference Manual](https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf)
- Key sections:
  - 18.1 Overview
  - 18.2 Sub-modules (PWM timer, PWM operator, dead time, capture, fault detection, carrier)
  - 18.3 PWM Timer sub-module
  - 18.4 PWM Operator sub-module
  - 18.5 Fault Detection and Handling
  - 18.6 Capture sub-module
  - 18.7 Interrupts
  - 18.8 Register Summary and Description

## Register Map Summary

The MCPWM has the largest register set of the peripherals in this analysis. The following is organized by sub-module:

### Clock Prescaler

| Register | Offset | Description |
|----------|--------|-------------|
| `MCPWM_CLK_CFG` | 0x0000 | Module clock prescaler (8-bit) |

### Timer Registers (x3 timers)

| Register | Offset (timer 0) | Description |
|----------|-------------------|-------------|
| `MCPWM_TIMERx_CFG0` | 0x0004 + x*8 | Timer x prescaler (8-bit), period (16-bit) |
| `MCPWM_TIMERx_CFG1` | 0x0008 + x*8 | Timer x start/stop, count mode (up/down/up-down) |
| `MCPWM_TIMERx_SYNC` | 0x001C + x*4 | Timer x sync config: input sync enable, sync source, phase |
| `MCPWM_TIMERx_STATUS` | 0x0028 + x*4 | Timer x current counter value + direction (read-only) |

### Operator Registers (x3 operators)

| Register | Offset (op 0) | Description |
|----------|----------------|-------------|
| `MCPWM_GENx_STMP_CFG` | 0x0034 + x*0x28 | Generator x timestamp config: A and B load mode |
| `MCPWM_GENx_TSTMP_A` | 0x0038 + x*0x28 | Generator x compare value A (16-bit) |
| `MCPWM_GENx_TSTMP_B` | 0x003C + x*0x28 | Generator x compare value B (16-bit) |
| `MCPWM_GENx_CFG0` | 0x0040 + x*0x28 | Generator x config: timer select, update mode |
| `MCPWM_GENx_FORCE` | 0x0044 + x*0x28 | Generator x forced output level |
| `MCPWM_GENx_A` | 0x0048 + x*0x28 | Generator x output A actions (on timer events, compare events) |
| `MCPWM_GENx_B` | 0x004C + x*0x28 | Generator x output B actions |
| `MCPWM_DTx_CFG` | 0x0050 + x*0x28 | Dead time x config: mode, rising/falling delay values |
| `MCPWM_CARRIERx_CFG` | 0x0054 + x*0x28 | Carrier x config: enable, prescaler, duty, inversion |
| `MCPWM_FHx_CFG0` | 0x0058 + x*0x28 | Fault handler x config 0: trip zone actions per output |
| `MCPWM_FHx_CFG1` | 0x005C + x*0x28 | Fault handler x config 1: cycle-by-cycle vs one-shot, clear |

### Fault Detection Registers

| Register | Offset | Description |
|----------|--------|-------------|
| `MCPWM_FAULT_DETECT` | 0x00E4 | Fault input enable, polarity, and filter config |

### Capture Registers

| Register | Offset | Description |
|----------|--------|-------------|
| `MCPWM_CAP_TIMER_CFG` | 0x00E8 | Capture timer config: enable, sync, phase |
| `MCPWM_CAP_TIMER_PHASE` | 0x00EC | Capture timer phase preset value |
| `MCPWM_CAP_CHx_CFG` | 0x00F0 + x*4 | Capture channel x config: enable, edge select, prescaler |
| `MCPWM_CAP_CHx` | 0x00FC + x*4 | Capture channel x value (32-bit timestamp, read-only) |
| `MCPWM_CAP_STATUS` | 0x0108 | Capture status: edge detected per channel |

### Interrupt Registers

| Register | Offset | Description |
|----------|--------|-------------|
| `MCPWM_INT_RAW` | 0x0110 | Raw interrupt status |
| `MCPWM_INT_ST` | 0x0114 | Masked interrupt status |
| `MCPWM_INT_ENA` | 0x0118 | Interrupt enable |
| `MCPWM_INT_CLR` | 0x011C | Interrupt clear |

### Interrupt Sources (30 bits)

- Timer 0/1/2 stop, TEZ (timer equals zero), TEP (timer equals period)
- Operator 0/1/2 TEA (timer equals A), TEB (timer equals B)
- Fault 0/1/2 enter, exit
- Capture 0/1/2 event

**Approximate register count**: ~80-90 registers per MCPWM unit

## Source Code References

### SOC Register Definitions
- **Register header**: [components/soc/esp32/register/soc/mcpwm_reg.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/mcpwm_reg.h)
  - Complete register addresses, field masks, and bit positions
- **Register struct**: [components/soc/esp32/register/soc/mcpwm_struct.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/mcpwm_struct.h)
  - C struct overlay showing register layout and field widths

### HAL Layer
- **Low-level HAL**: [components/esp_hal_mcpwm/esp32/include/hal/mcpwm_ll.h](https://github.com/espressif/esp-idf/blob/master/components/esp_hal_mcpwm/esp32/include/hal/mcpwm_ll.h)
  - Direct register manipulation functions
  - Key functions: `mcpwm_ll_timer_set_count_mode()`, `mcpwm_ll_operator_set_compare_value()`, `mcpwm_ll_generator_set_action_on_timer_event()`, `mcpwm_ll_deadtime_set_rising_delay()`, `mcpwm_ll_fault_enable_detection()`

### API Documentation
- **API reference**: [MCPWM API Documentation](https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-reference/peripherals/mcpwm.html)
  - High-level driver with object-oriented API
  - Key concepts: timers, operators, comparators, generators, dead-time, fault handlers, capture channels
  - Functions: `mcpwm_new_timer()`, `mcpwm_new_operator()`, `mcpwm_new_comparator()`, `mcpwm_new_generator()`

### Examples
- **Example code**: [examples/peripherals/mcpwm](https://github.com/espressif/esp-idf/blob/master/examples/peripherals/mcpwm)
  - Brushed DC motor control
  - Servo motor control
  - BLDC motor control with Hall sensors
  - Capture-based HC-SR04 ultrasonic sensor example

### QEMU Implementation
- **No QEMU implementation exists** for the MCPWM peripheral.

## Renode Implementation Analysis

### Reference Peripherals in Renode

No direct MCPWM equivalent exists in Renode. Potentially relevant:

1. **STM32 Advanced Timer (TIM1/TIM8)**: The STM32 advanced timers share many concepts with MCPWM: complementary outputs, dead-time insertion, break (fault) inputs, and center-aligned mode. However, the ESP32 MCPWM architecture is significantly different in its organization (separate timer/operator/generator sub-modules).
2. **PWM peripherals in various SoCs**: NRF52 PWM, LPC PWM, etc. provide simpler PWM models.
3. **BasicDoubleWordPeripheral**: Standard base for register interface.

### Recommended Approach

1. **Register scaffold**: Implement all ~80-90 registers per unit. Due to the complexity, consider implementing one MCPWM unit first and then instantiating a second copy.

2. **Timer sub-module** (3 per unit):
   - 16-bit counter with configurable period
   - Count mode: up, down, up-down
   - Prescaler chain: module prescaler (8-bit) x timer prescaler (8-bit)
   - Timer events: TEZ (equals zero), TEP (equals period)
   - Start/stop control
   - For emulation: use Renode virtual timers. Calculate timer period from clock and prescaler, fire TEZ/TEP events at appropriate intervals.

3. **Operator sub-module** (3 per unit):
   - Binds to one of the 3 timers
   - Two 16-bit compare values (A and B)
   - Compare events: TEA (equals A), TEB (equals B)
   - Generator outputs: configurable actions on timer events and compare events (set high, set low, toggle, no change)
   - This is the core PWM generation logic

4. **Dead time generator**: Per operator, adds configurable delays between complementary outputs. For emulation, this primarily affects the output state tracking and can be simplified to a configuration store with basic output logic.

5. **Fault handler**: Hardware override of PWM outputs on fault pin assertion. Important for safety-critical firmware that tests fault behavior. Implement as GPIO input that forces outputs to configured safe state.

6. **Capture sub-module**: Independent 32-bit free-running timer with 3 capture channels. On configured edge, latches timer value. Useful and relatively simple to implement. Requires GPIO input infrastructure.

7. **Interrupt generation**: Wire up all ~30 interrupt sources to the interrupt controller. The interrupt pattern is standard (raw/status/enable/clear).

8. **What to skip initially**:
   - Carrier modulation (specialized for transformer-coupled drivers)
   - Timer synchronization (complex inter-timer coordination)
   - Cycle-accurate PWM waveform output
   - Output pin-level tracking (focus on register state and interrupts)

### Validation Strategy

- Use ESP-IDF MCPWM servo motor example (simpler than BLDC)
- Verify: timer configuration, operator/comparator setup, generator action configuration, basic PWM operation completes without error
- Capture example is a good second test case

## Complexity Assessment

| Aspect | Rating | Notes |
|--------|--------|-------|
| **Register count** | High | ~80-90 registers per unit, 2 units = ~180 total |
| **Logic complexity** | High | Multiple interacting sub-modules: timers, operators, generators, dead-time, fault, capture |
| **Interrupt model** | Medium-High | ~30 interrupt sources per unit, standard pattern but many sources |
| **External dependencies** | Medium | GPIO for fault inputs and capture inputs |
| **Clock domain complexity** | Medium | Two-stage prescaler, timer modes (up/down/up-down) |
| **DMA** | None | MCPWM does not use DMA |
| **QEMU reference available** | **No** | Must implement from TRM and HAL code analysis |
| **Overall effort** | **High** | Estimated 5-7 days |
| **Priority** | **Low-Medium** | Specialized for motor control; fewer general-purpose use cases |

The MCPWM is the most complex peripheral in this analysis. Its multi-layered architecture (timer -> operator -> generator -> dead-time -> output) creates significant implementation effort. For many emulation scenarios, motor control is not the primary focus, making this a lower priority. If needed, a phased approach (timer + basic PWM first, then dead-time, fault, capture) is recommended.
