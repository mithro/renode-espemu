# ESP32 Timer Groups

## Overview

The ESP32 contains two identical Timer Group peripherals (Timer Group 0 and Timer Group 1), each providing two general-purpose 64-bit hardware timers and one Main Watchdog Timer (MWDT). These timer groups are fundamental to system operation: FreeRTOS uses Timer Group 0, Timer 0 (TG0_T0) as its system tick source, making accurate timer emulation critical for any firmware that relies on RTOS scheduling, delays, or time-based operations.

Each 64-bit timer can count up or down, features a 16-bit prescaler for flexible clock division, supports alarm (compare-match) functionality with auto-reload capability, and generates interrupts on alarm events. The timers operate from the APB clock (typically 80 MHz) after prescaler division.

In addition to the two timer groups, the ESP32 contains a legacy FRC (Free Running Counter) timer used by earlier ESP-IDF versions for high-resolution timing. Modern ESP-IDF versions (v4.x+) use the general-purpose timer API instead.

## Hardware Specifications

### Timer Group Architecture

| Feature | Specification |
|---|---|
| Number of Timer Groups | 2 (TG0, TG1) |
| Timers per Group | 2 (T0, T1) |
| Total General-Purpose Timers | 4 |
| Timer Width | 64 bits |
| Prescaler | 16-bit (divider value 2-65536; value 0 and 1 both treated as 2) |
| Clock Source | APB_CLK (typically 80 MHz) |
| Count Direction | Up or Down (configurable) |
| Alarm Function | Yes, 64-bit compare value |
| Auto-Reload | Yes, configurable reload value on alarm |
| Interrupt Generation | Per-timer alarm interrupt |
| DMA Support | No |

### Timer Group 0 Base Address

| Peripheral | Base Address |
|---|---|
| Timer Group 0 | 0x3FF5F000 |
| Timer Group 1 | 0x3FF60000 |

### Clock Configuration

- **APB Clock**: Default 80 MHz (derived from PLL at 160/240/320 MHz divided appropriately)
- **Prescaler Range**: Effective division by 2 to 65536
- **Minimum Timer Frequency**: APB_CLK / 65536 = ~1.22 kHz (at 80 MHz APB)
- **Maximum Timer Frequency**: APB_CLK / 2 = 40 MHz (at 80 MHz APB)

### Per-Timer Features

- **64-bit counter**: Composed of two 32-bit registers (low and high) that are latched together for atomic reads
- **Counter read mechanism**: Write to a trigger register to latch the current 64-bit value, then read the low and high register pair
- **Alarm**: 64-bit match value; when counter reaches alarm value, generates interrupt and optionally reloads counter
- **Auto-reload**: On alarm match, counter can automatically reload from a configurable 64-bit value
- **Edge count or level**: Alarm interrupt can be edge-triggered

### Watchdog Timer (per group)

Each timer group also contains one Main Watchdog Timer (MWDT). See `docs/peripherals/watchdog.md` for detailed MWDT documentation.

## TRM Chapter Reference

**ESP32 Technical Reference Manual, Chapter 17: Timer Group (TIMG)**

- [ESP32 TRM PDF](https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf)
- Chapter 17 covers both the general-purpose timers and the watchdog timers within each timer group.

### Key Sections

| Section | Topic |
|---|---|
| 17.1 | Introduction and features overview |
| 17.2 | Functional description of 64-bit timers |
| 17.3 | Watchdog timers (MWDT) |
| 17.4 | Register summary and descriptions |

### Critical Details from TRM

1. **Counter Latch Mechanism**: To read the 64-bit counter atomically, firmware writes any value to `TIMGn_TxUPDATE_REG`, which latches the current counter into `TIMGn_TxLO_REG` and `TIMGn_TxHI_REG`. This is essential for correct emulation.
2. **Prescaler Behavior**: A prescaler value of 0 or 1 is treated as 2 (minimum divider is 2).
3. **Alarm Behavior**: When the counter matches the alarm value:
   - If auto-reload is enabled, counter reloads from the reload value
   - An interrupt is generated (if enabled)
   - The alarm enable bit is automatically cleared (one-shot by default; firmware must re-enable)
4. **Clock Gating**: Each timer can be individually clock-gated for power saving.

## Register Map Summary

### Timer Registers (per timer, offsets relative to group base)

| Register | Offset (T0) | Offset (T1) | Description |
|---|---|---|---|
| TIMGn_TxCONFIG_REG | 0x0000 | 0x0024 | Timer configuration (prescaler, count direction, enable, auto-reload, alarm enable) |
| TIMGn_TxLO_REG | 0x0004 | 0x0028 | Timer counter value (low 32 bits, latched) |
| TIMGn_TxHI_REG | 0x0008 | 0x002C | Timer counter value (high 32 bits, latched) |
| TIMGn_TxUPDATE_REG | 0x000C | 0x0030 | Write to latch counter; read bit 31 for latch status |
| TIMGn_TxALARMLO_REG | 0x0010 | 0x0034 | Alarm compare value (low 32 bits) |
| TIMGn_TxALARMHI_REG | 0x0014 | 0x0038 | Alarm compare value (high 32 bits) |
| TIMGn_TxLOADLO_REG | 0x0018 | 0x003C | Counter reload value (low 32 bits) |
| TIMGn_TxLOADHI_REG | 0x001C | 0x0040 | Counter reload value (high 32 bits) |
| TIMGn_TxLOAD_REG | 0x0020 | 0x0044 | Write to trigger counter reload from load registers |

### Interrupt Registers (per group)

| Register | Offset | Description |
|---|---|---|
| TIMGn_INT_RAW_TIMERS_REG | 0x0098 | Raw interrupt status |
| TIMGn_INT_ST_TIMERS_REG | 0x009C | Masked interrupt status |
| TIMGn_INT_ENA_TIMERS_REG | 0x00A0 | Interrupt enable |
| TIMGn_INT_CLR_TIMERS_REG | 0x00A4 | Interrupt clear (write 1 to clear) |

### Interrupt Bit Assignments

| Bit | Interrupt Source |
|---|---|
| Bit 0 | T0 alarm |
| Bit 1 | T1 alarm |
| Bit 2 | WDT (MWDT) |
| Bit 3 | Lact alarm (legacy, TG0 only) |

### Key CONFIG_REG Fields

| Field | Bits | Description |
|---|---|---|
| EN | 31 | Timer enable (1 = counting) |
| INCREASE | 30 | Count direction (1 = increment, 0 = decrement) |
| AUTORELOAD | 29 | Auto-reload on alarm (1 = enabled) |
| DIVIDER | 28:13 | 16-bit prescaler value |
| EDGE_INT_EN | 12 | Edge interrupt enable |
| LEVEL_INT_EN | 11 | Level interrupt enable |
| ALARM_EN | 10 | Alarm enable (auto-clears on alarm match) |

## Source Code References

### SOC Register Definitions

- **Register Header**: [components/soc/esp32/register/soc/timer_group_reg.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/timer_group_reg.h)
  - Contains all register offset definitions, bit field masks, and shift values
  - Defines `TIMG_T0CONFIG_REG`, `TIMG_T0LO_REG`, `TIMG_T0HI_REG`, etc.
  - Includes WDT registers as part of the timer group

- **Register Struct**: [components/soc/esp32/register/soc/timer_group_struct.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/timer_group_struct.h)
  - C struct overlay for memory-mapped register access
  - Defines `timg_dev_t` with bit-field definitions for each register
  - Useful for understanding exact bit layouts

### HAL (Hardware Abstraction Layer)

- **Timer LL (Low-Level) HAL**: [components/esp_hal_timg/esp32/include/hal/timer_ll.h](https://github.com/espressif/esp-idf/blob/master/components/esp_hal_timg/esp32/include/hal/timer_ll.h)
  - Inline functions for all timer operations
  - Key functions for emulation reference:
    - `timer_ll_set_clock_prescale()` - prescaler configuration
    - `timer_ll_set_count_direction()` - up/down counting
    - `timer_ll_trigger_soft_reload()` - counter reload sequence
    - `timer_ll_get_counter_value()` - counter latch-and-read sequence
    - `timer_ll_set_alarm_value()` - alarm configuration
    - `timer_ll_enable_alarm()` / `timer_ll_enable_counter()` - enable controls

### API Documentation

- **General Purpose Timer (GPTimer)**: [ESP-IDF GPTimer API](https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-reference/peripherals/gptimer.html)
  - High-level driver API (`gptimer_new_timer`, `gptimer_set_alarm_action`, `gptimer_start`)
  - Documents the user-facing timer functionality
  - Shows typical usage patterns that firmware will exercise

### Examples

- **Timer Group Example**: [examples/peripherals/timer_group](https://github.com/espressif/esp-idf/blob/master/examples/peripherals/timer_group)
  - Demonstrates basic timer group usage with alarm interrupts
  - Good reference for typical firmware interaction sequences
  - Shows the alarm-reload-interrupt cycle that emulation must support

### QEMU Implementation

- **Timer Group Model**: [hw/timer/esp32_timg.c](https://github.com/espressif/qemu/blob/esp-develop/hw/timer/esp32_timg.c)
  - Complete QEMU emulation of both timers and WDT per group
  - Key implementation details:
    - Uses QEMU timer infrastructure for countdown/countup
    - Implements the counter latch mechanism (write-to-update, then read)
    - Handles prescaler reconfiguration
    - Implements alarm match with auto-reload
    - Includes WDT stage progression and system reset
  - This is the most complete reference for behavioral emulation

- **FRC Timer Model**: [hw/timer/esp32_frc_timer.c](https://github.com/espressif/qemu/blob/esp-develop/hw/timer/esp32_frc_timer.c)
  - Legacy Free Running Counter timer emulation
  - Simpler model, useful as secondary reference
  - Lower priority for emulation (modern ESP-IDF does not use FRC)

## Renode Implementation Analysis

### Existing Renode Timer Models (Reference)

- **STM32_Timer**: [Peripherals/Timers/STM32_Timer.cs](https://github.com/renode/renode-infrastructure/blob/master/src/Emulator/Peripherals/Peripherals/Timers/STM32_Timer.cs)
  - Full-featured timer with prescaler, auto-reload, compare channels
  - Good reference for multi-feature timer implementation
  - Shows how to use Renode's `LimitTimer` class with prescaler
  - Demonstrates interrupt flag management patterns

- **LiteX_Timer**: [Peripherals/Timers/LiteX_Timer.cs](https://github.com/renode/renode-infrastructure/blob/master/src/Emulator/Peripherals/Peripherals/Timers/LiteX_Timer.cs)
  - Simpler timer model with reload and interrupt
  - Good starting template for basic timer functionality
  - Shows CSR-style register interface mapping

### Recommended Implementation Approach

#### Class Structure

```
ESP32_TimerGroup : IDoubleWordPeripheral, IKnownSize
├── ESP32_GPTimer (x2) - uses Renode LimitTimer internally
├── ESP32_MWDT (x1) - watchdog, see watchdog.md
└── Register map via DoubleWordRegisterCollection
```

#### Core Components

1. **LimitTimer Usage**: Each 64-bit GP timer should use Renode's `LimitTimer` configured with:
   - Frequency derived from APB clock / prescaler
   - Direction (up/down) matching the INCREASE config bit
   - Limit set to 2^64 (or alarm value when alarm is enabled)
   - Auto-reload behavior triggered on limit reached

2. **Counter Latch Mechanism**: This is ESP32-specific and critical:
   - Maintain internal 64-bit counter state
   - On write to UPDATE register: snapshot current LimitTimer value into latched low/high registers
   - On read of LO/HI registers: return latched values (not live counter)
   - The UPDATE register bit 31 should read as 1 when latch is complete (can be immediate in emulation)

3. **Alarm Implementation**:
   - When alarm is enabled, configure LimitTimer limit to the alarm value
   - On limit reached: generate interrupt, optionally reload, auto-clear alarm enable bit
   - Firmware must re-enable alarm for repeated alarms (one-shot default behavior)

4. **Prescaler**:
   - Map 16-bit prescaler field to LimitTimer frequency: `timer_freq = APB_CLK / prescaler_value`
   - Handle special case: prescaler value 0 or 1 treated as 2
   - Prescaler changes may require recalculating the LimitTimer frequency

#### Key Firmware Interactions to Support

1. **FreeRTOS Tick Timer (TG0_T0)**:
   - This is the most critical interaction
   - FreeRTOS configures TG0_T0 with an alarm at the tick interval (typically 1ms or 10ms)
   - On alarm: interrupt fires, ISR acknowledges interrupt, re-enables alarm
   - Without this working, FreeRTOS scheduling is completely broken

2. **Boot Sequence**:
   - Early boot code may read timer values for entropy or timing
   - Bootloader configures WDT (see watchdog.md)
   - Timer group clock gating is configured during peripheral initialization

3. **Typical Timer Usage Sequence**:
   ```
   1. Configure prescaler and direction in CONFIG_REG
   2. Set reload value in LOADLO/LOADHI, trigger LOAD
   3. Set alarm value in ALARMLO/ALARMHI
   4. Enable alarm in CONFIG_REG
   5. Enable interrupt in INT_ENA_TIMERS_REG
   6. Enable counter in CONFIG_REG
   7. On alarm interrupt:
      a. Read and clear interrupt status
      b. Re-enable alarm if periodic
      c. Optionally read counter value (latch + read)
   ```

4. **Counter Read Sequence**:
   ```
   1. Write any value to TIMGn_TxUPDATE_REG
   2. (Optional) Poll bit 31 of UPDATE_REG until set
   3. Read TIMGn_TxLO_REG
   4. Read TIMGn_TxHI_REG
   5. Combine into 64-bit value
   ```

### Implementation Priority

| Component | Priority | Reason |
|---|---|---|
| TG0_T0 basic counting | CRITICAL | FreeRTOS tick source |
| Alarm + interrupt | CRITICAL | FreeRTOS tick mechanism |
| Counter latch read | HIGH | Counter read correctness |
| Prescaler | HIGH | Correct timing intervals |
| Auto-reload | HIGH | Periodic timer operation |
| Count direction | MEDIUM | Most firmware uses count-up |
| TG0_T1, TG1_T0, TG1_T1 | MEDIUM | Same implementation, different instances |
| Edge vs level interrupt | LOW | Most firmware uses level |
| Clock gating | LOW | Can stub as always-on |

## Complexity Assessment

### Overall Complexity: MEDIUM-HIGH

#### Justification

| Factor | Rating | Notes |
|---|---|---|
| Register complexity | Medium | ~10 registers per timer, clear bit fields |
| Behavioral complexity | Medium-High | 64-bit counters, latch mechanism, alarm auto-clear, prescaler edge cases |
| Timing sensitivity | High | FreeRTOS tick depends on accurate alarm timing |
| Boot criticality | HIGH | FreeRTOS won't schedule without TG0_T0 |
| Interrupt integration | Medium | Standard interrupt generation, routes through interrupt matrix |
| QEMU reference quality | High | Complete implementation available |
| Renode reference quality | High | STM32_Timer provides good patterns |

#### Estimated Effort

- **Minimal viable (TG0_T0 with alarm + interrupt)**: 2-3 days
- **Full implementation (all 4 timers + prescaler + all features)**: 4-5 days
- **Including WDT (see watchdog.md)**: Add 2-3 days

#### Key Risks

1. **64-bit Counter Accuracy**: Renode's LimitTimer may need careful configuration to handle full 64-bit range without overflow issues
2. **Latch Mechanism**: Non-standard read mechanism must be correctly implemented or counter reads will return stale/incorrect values
3. **Alarm Auto-Clear**: The one-shot alarm behavior (auto-clear on match) is a subtle behavior that firmware depends on
4. **Prescaler Edge Cases**: Values 0 and 1 mapping to divider 2 must be handled correctly
5. **FreeRTOS Tick Sensitivity**: If alarm timing is even slightly off, FreeRTOS tick rate will drift, causing timeout and delay issues throughout the system
