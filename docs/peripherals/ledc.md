# ESP32 LEDC (LED PWM Controller)

## Overview

The LEDC (LED Control) peripheral is a PWM generator primarily designed for LED brightness control, but usable for any general-purpose PWM application. It provides 16 independent channels organized into two speed groups (high-speed and low-speed), each with 4 shared timers. The peripheral supports hardware-accelerated duty cycle fading, enabling smooth LED dimming transitions without CPU intervention.

The LEDC is one of the simpler timer-based peripherals on the ESP32 and has an existing QEMU implementation, making it a strong candidate for early Renode emulation.

## Hardware Specifications

- **Channel count**: 16 total (8 high-speed + 8 low-speed)
- **Timer count**: 8 total (4 high-speed + 4 low-speed)
- **Duty cycle resolution**: 1 to 20 bits (configurable per timer)
- **Clock sources**:
  - High-speed channels: REF_TICK (1 MHz) or APB_CLK (80 MHz)
  - Low-speed channels: REF_TICK (1 MHz), APB_CLK (80 MHz), or RTC8M_CLK (~8 MHz)
- **Frequency range**: Depends on clock source and duty resolution. With APB_CLK and 1-bit duty: up to 40 MHz. With 20-bit duty: ~76 Hz.
- **Fade support**: Hardware fade function with configurable step size, cycle count per step, and scale
- **Output signals**: `ledc_hs_sig_out0` through `ledc_hs_sig_out7` (high-speed), `ledc_ls_sig_out0` through `ledc_ls_sig_out7` (low-speed) routed via GPIO matrix
- **Interrupts**: Fade-done interrupt per channel (16 total), plus high-speed timer overflow interrupts (4 total)
- **Base address**: `0x3FF59000`
- **Register block size**: `0x1000`

## TRM Chapter Reference

- **Chapter 14: LED PWM Controller (LEDC)**
- Source: [ESP32 Technical Reference Manual](https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf)
- Key sections:
  - 14.1 Overview
  - 14.2 Functional Description (timer configuration, channel configuration, duty cycle change, fade function)
  - 14.3 Register Summary and Description

## Register Map Summary

The LEDC register space is organized into high-speed and low-speed groups with identical layouts:

### Timer Registers (per timer, x4 per speed group)

| Register | Offset (HS) | Description |
|----------|-------------|-------------|
| `LEDC_HSTIMERx_CONF` | 0x0140 + x*8 | Timer x config: clock divider, duty resolution, pause/reset |
| `LEDC_HSTIMERx_VALUE` | 0x0144 + x*8 | Timer x current counter value (read-only) |
| `LEDC_LSTIMERx_CONF` | 0x0160 + x*8 | Low-speed timer x config |
| `LEDC_LSTIMERx_VALUE` | 0x0164 + x*8 | Low-speed timer x current value |

### Channel Registers (per channel, x8 per speed group)

| Register | Offset (HS ch0) | Description |
|----------|-----------------|-------------|
| `LEDC_HSCHx_CONF0` | 0x0000 + x*0x14 | Channel config: timer select, output enable, idle level |
| `LEDC_HSCHx_HPOINT` | 0x0004 + x*0x14 | High point value (PWM rising edge position) |
| `LEDC_HSCHx_DUTY` | 0x0008 + x*0x14 | Duty cycle value (left-shifted by 4 bits for fractional duty) |
| `LEDC_HSCHx_CONF1` | 0x000C + x*0x14 | Duty change config: step num, cycle num, scale, increase/decrease |
| `LEDC_HSCHx_DUTY_R` | 0x0010 + x*0x14 | Current duty value (read-only, reflects running duty) |

Low-speed channel registers follow the same layout starting at offset `0x00A0`.

### Global Registers

| Register | Offset | Description |
|----------|--------|-------------|
| `LEDC_CONF` | 0x0190 | Global config: APB_CLK select for high-speed timers |
| `LEDC_INT_RAW` | 0x0180 | Raw interrupt status |
| `LEDC_INT_ST` | 0x0184 | Masked interrupt status |
| `LEDC_INT_ENA` | 0x0188 | Interrupt enable |
| `LEDC_INT_CLR` | 0x018C | Interrupt clear (write-1-to-clear) |

### Interrupt Bits

- Bits [0:7]: High-speed channel 0-7 fade done (`LEDC_DUTY_CHNG_END_HSCHx_INT`)
- Bits [8:11]: High-speed timer 0-3 overflow (`LEDC_TIMER_OVF_HSx_INT`)
- Bits [12:19]: Low-speed channel 0-7 fade done (`LEDC_DUTY_CHNG_END_LSCHx_INT`)
- Bits [20:23]: Low-speed timer 0-3 overflow (`LEDC_TIMER_OVF_LSx_INT`)

**Approximate register count**: ~100 registers total

## Source Code References

### SOC Register Definitions
- **Register header**: [components/soc/esp32/register/soc/ledc_reg.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/ledc_reg.h)
  - Defines all register addresses, field masks, and bit positions
- **Register struct**: [components/soc/esp32/register/soc/ledc_struct.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/ledc_struct.h)
  - C struct overlay for memory-mapped register access; useful for understanding register layout and field widths

### HAL Layer
- **Low-level HAL**: [components/esp_hal_ledc/esp32/include/hal/ledc_ll.h](https://github.com/espressif/esp-idf/blob/master/components/esp_hal_ledc/esp32/include/hal/ledc_ll.h)
  - Inline functions that directly manipulate registers
  - Shows exact register manipulation sequences for each operation
  - Key functions: `ledc_ll_set_duty_resolution()`, `ledc_ll_set_clock_divider()`, `ledc_ll_set_duty_int_part()`, `ledc_ll_set_fade_param()`, `ledc_ll_set_sig_out_en()`

### API Documentation
- **API reference**: [LEDC API Documentation](https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-reference/peripherals/ledc.html)
  - High-level driver API showing typical usage patterns
  - Key functions: `ledc_timer_config()`, `ledc_channel_config()`, `ledc_set_duty()`, `ledc_update_duty()`, `ledc_set_fade_with_time()`

### Examples
- **Example code**: [examples/peripherals/ledc](https://github.com/espressif/esp-idf/blob/master/examples/peripherals/ledc)
  - Basic LED fade example demonstrating timer setup, channel config, and fade operations
  - Good test case for emulation validation

### QEMU Implementation
- **QEMU source**: [hw/misc/esp32_ledc.c](https://github.com/espressif/qemu/blob/esp-develop/hw/misc/esp32_ledc.c)
  - Espressif's QEMU implementation of the LEDC for ESP32
  - Provides a reference for register handling, timer behavior modeling, and interrupt generation
  - Key observations from QEMU approach:
    - Models timer counting and duty cycle comparison
    - Implements fade state machine
    - Generates fade-done interrupts
    - Does not produce actual PWM waveform output (returns duty state on read)

## Renode Implementation Analysis

### Reference Peripherals in Renode

There is no existing LEDC peripheral in Renode. Relevant reference implementations:

1. **STM32 Timer/PWM peripherals**: STM32 timers with PWM mode share conceptual similarity (timer-based, duty cycle compare, multiple channels). Files like `STM32_Timer.cs` in renode-infrastructure show how to model timer counting and compare events.
2. **BasicDoubleWordPeripheral base class**: The standard Renode base for 32-bit register peripherals, providing register definition DSL with read/write callbacks.

### Recommended Approach

1. **Register scaffold**: Implement all ~100 registers using Renode's register definition framework. Start with read/write storage for all registers.

2. **Timer modeling**: Each of the 8 timers needs:
   - Clock source selection (APB_CLK, REF_TICK, RTC8M_CLK)
   - Divisor configuration (18-bit integer + 10-bit fractional)
   - Duty resolution (defines timer overflow period)
   - Pause/reset control
   - The timers do not need cycle-accurate counting; tracking the configured frequency and period is sufficient for most firmware.

3. **Channel modeling**: Each of the 16 channels needs:
   - Timer binding (which of 4 timers in its speed group)
   - Duty cycle value (with hpoint for phase offset)
   - Output enable and idle level
   - For emulation, the key behavior is: when duty is set and `ledc_update_duty()` writes the update bit, the new duty takes effect.

4. **Fade engine**: The hardware fade is the most complex part:
   - Step-based duty change: `duty += scale` every `cycle_num` timer overflows, repeated `num` times
   - When fade completes, fire `DUTY_CHNG_END` interrupt
   - This can be modeled with a virtual timer that calculates fade completion time and fires the interrupt after the appropriate delay.

5. **Interrupt generation**: Wire up the 24 interrupt sources (16 fade-done + 4 HS timer overflow + 4 LS timer overflow) to the interrupt controller.

6. **What to skip initially**:
   - Actual PWM waveform generation (no physical output in emulation)
   - Fractional duty (the 4 LSBs of duty used for sigma-delta dithering)
   - RTC8M_CLK source accuracy modeling
   - Cycle-accurate timer counter values (return reasonable approximations)

### Validation Strategy

- Use the ESP-IDF LEDC fade example as the primary test
- Verify: timer configuration succeeds, channel binds to timer, duty set/update works, fade completes with interrupt
- Compare register read-back values against QEMU behavior

## Complexity Assessment

| Aspect | Rating | Notes |
|--------|--------|-------|
| **Register count** | Low-Medium | ~100 registers, but highly repetitive (8 copies of same channel layout) |
| **Logic complexity** | Medium | Timer counting is simple; fade engine adds moderate complexity |
| **Interrupt model** | Low | Standard enable/status/clear pattern, 24 sources |
| **External dependencies** | Low | Only needs GPIO matrix for output routing (optional for basic emulation) |
| **Clock domain complexity** | Low-Medium | Multiple clock sources but straightforward mux |
| **DMA** | None | LEDC does not use DMA |
| **QEMU reference available** | Yes | Significantly reduces implementation risk |
| **Overall effort** | **Medium** | Estimated 2-3 days |
| **Priority** | **High** | Common peripheral, QEMU reference exists, good early win |

The LEDC is a strong candidate for early implementation due to its moderate complexity, existing QEMU reference, and wide usage in ESP-IDF applications. The fade engine is the primary complexity driver; a simplified model that correctly fires the completion interrupt will satisfy most firmware.
