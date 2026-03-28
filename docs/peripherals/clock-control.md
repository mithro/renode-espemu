# ESP32 Clock Control

## Overview

The ESP32 clock control subsystem manages all clock generation and distribution across the SoC. It starts from crystal oscillator and internal RC oscillator sources, generates high-frequency clocks through PLLs, and distributes derived clocks to the CPU cores, APB bus, and individual peripherals. Clock configuration is one of the earliest operations performed during boot and directly affects system performance, peripheral timing, and power consumption.

For emulation purposes, clock control is important because firmware reads back clock configuration registers to determine bus frequencies, calculate baud rates, and configure peripheral timing. If the clock registers do not report expected values, drivers will miscalculate timing parameters and peripherals will malfunction.

## Hardware Specifications

### Clock Sources
- **XTAL (External Crystal)**: 40 MHz (most common), 26 MHz, or other frequencies; configured by board design
- **Internal 8 MHz RC oscillator (RC_FAST / CK8M)**: Low-power clock source, ~8.5 MHz typical
- **Internal 150 kHz RC oscillator (RC_SLOW / CK_RTC)**: Ultra-low-power RTC clock, ~150 kHz typical
- **External 32.768 kHz crystal (XTAL32K)**: Optional precision RTC clock source

### PLLs
- **BBPLL (Baseband PLL)**: Main PLL, generates 320 MHz or 480 MHz from XTAL
  - 320 MHz mode: CPU can run at 80 MHz or 160 MHz
  - 480 MHz mode: CPU can run at 80 MHz, 160 MHz, or 240 MHz
- **APLL (Audio PLL)**: Configurable PLL for I2S and other audio peripherals
  - Output range: ~16 MHz to ~128 MHz
  - Provides fractional frequency synthesis for precise audio sample rates

### Clock Tree
```
XTAL (40 MHz) ──> BBPLL ──> 320/480 MHz
                    |
                    +──> CPU_CLK: 80/160/240 MHz (divider from PLL)
                    |
                    +──> APB_CLK: 80 MHz (fixed when PLL active)
                    |
                    +──> REF_TICK: 1 MHz (derived from APB_CLK)

XTAL (40 MHz) ──> Direct peripheral use (some peripherals can use XTAL directly)

RC_FAST (8 MHz) ──> CK8M_D256 (31.25 kHz) ──> RTC options
                 ──> Digital peripherals when PLL is off

RC_SLOW (150 kHz) ──> RTC_CLK (default RTC source)
XTAL32K (32.768 kHz) ──> RTC_CLK (optional, more precise)
```

### CPU Clock Frequencies
| PLL Setting | CPU Divider | CPU Frequency | APB Frequency |
| ----------- | ----------- | ------------- | ------------- |
| PLL 320 MHz | /4          | 80 MHz        | 80 MHz        |
| PLL 320 MHz | /2          | 160 MHz       | 80 MHz        |
| PLL 480 MHz | /2          | 240 MHz       | 80 MHz        |
| XTAL direct | /1          | 40 MHz        | 40 MHz        |
| RC_FAST     | /1          | ~8 MHz        | ~8 MHz        |

### Peripheral Clocks
- Most peripherals use APB_CLK (80 MHz) as their base clock
- Some peripherals (UART, LEDC, etc.) can select REF_TICK (1 MHz) for baud-rate independent operation
- I2S can use APLL for precise audio clocking
- RTC peripherals use the RTC slow clock

## TRM Chapter Reference

- **Chapter 3**: Reset and Clock
  - Section 3.2: Clock system overview
  - Section 3.3: CPU clock configuration
  - Section 3.4: Peripheral clock configuration
  - Section 3.5: PLL configuration
  - Section 3.6: RTC clock sources

TRM PDF: https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf

## Register Map Summary

### RTC_CNTL Registers (base 0x3FF48000) - PLL and Clock Source

| Register                   | Offset | Description                                                  |
| -------------------------- | ------ | ------------------------------------------------------------ |
| RTC_CNTL_CLK_CONF_REG      | 0x070  | Main clock configuration (CPU clock source select, dividers) |
| RTC_CNTL_OPTIONS0_REG      | 0x000  | Power/reset options (includes PLL force enable)              |
| RTC_CNTL_BIAS_CONF_REG     | 0x018  | Bias configuration for oscillators                           |
| RTC_CNTL_TIMER1_REG        | 0x01C  | PLL and CK8M startup wait times                              |
| RTC_CNTL_TIMER2_REG        | 0x020  | Additional timing configuration                              |
| RTC_CNTL_SLOW_CLK_CONF_REG | 0x074  | RTC slow clock source selection and calibration              |
| RTC_CNTL_STORE4_REG        | 0x0B0  | Boot-persistent storage (used to store XTAL freq)            |
| RTC_CNTL_STORE5_REG        | 0x0B4  | Boot-persistent storage (used for clock cal data)            |
| RTC_CNTL_DIAG0_REG         | 0x0C0  | Diagnostic (includes CK8M ready status)                      |

### APB Control Registers (base 0x3FF66000)

| Register                    | Offset | Description                                |
| --------------------------- | ------ | ------------------------------------------ |
| APB_CTRL_SYSCLK_CONF_REG    | 0x000  | System clock pre-divider and source select |
| APB_CTRL_XTAL_TICK_CONF_REG | 0x004  | XTAL tick divider for REF_TICK generation  |
| APB_CTRL_PLL_TICK_CONF_REG  | 0x008  | PLL tick divider for REF_TICK generation   |
| APB_CTRL_CK8M_TICK_CONF_REG | 0x00C  | CK8M tick divider                          |
| APB_CTRL_DATE_REG           | 0x07C  | Version register                           |

### DPORT Clock Gate Registers (base 0x3FF00000)

| Register                   | Offset | Description                                         |
| -------------------------- | ------ | --------------------------------------------------- |
| DPORT_PERIP_CLK_EN_REG     | 0x0C0  | Peripheral clock gate enable (bit per peripheral)   |
| DPORT_PERIP_RST_EN_REG     | 0x0C4  | Peripheral reset control (bit per peripheral)       |
| DPORT_CPU_PER_CONF_REG     | 0x03C  | CPU period configuration (CPU clock divider select) |
| DPORT_BT_LPCK_DIV_INT_REG  | 0x0D0  | Bluetooth low-power clock divider (integer part)    |
| DPORT_BT_LPCK_DIV_FRAC_REG | 0x0D4  | Bluetooth low-power clock divider (fractional part) |

## Source Code References

### HAL Layer
- **Clock tree low-level operations**: https://github.com/espressif/esp-idf/blob/master/components/esp_hal_clock/esp32/include/hal/clk_tree_ll.h
  - `clk_ll_bbpll_set_freq_mhz()` - Configure BBPLL output frequency
  - `clk_ll_bbpll_get_freq_mhz()` - Read back BBPLL frequency
  - `clk_ll_cpu_set_freq_mhz_from_pll()` - Set CPU frequency from PLL
  - `clk_ll_cpu_get_freq_mhz_from_pll()` - Read CPU frequency
  - `clk_ll_cpu_set_src()` - Select CPU clock source (XTAL/PLL/CK8M)
  - `clk_ll_cpu_get_src()` - Read CPU clock source
  - `clk_ll_apb_get_freq_hz()` - Get APB bus frequency

### SOC Register Definitions
- **RTC control registers**: https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/rtc_cntl_reg.h
- **APB control registers**: https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/apb_ctrl_reg.h

## Renode Implementation Analysis

### Reference Peripherals in Renode
- Various STM32 RCC (Reset and Clock Control) models in Renode that manage PLL configuration and clock distribution
- These demonstrate the pattern of maintaining clock frequency state and reporting it via registers

### Implementation Approach

1. **Clock State Model**:
   - Maintain internal state for all clock sources: XTAL frequency, PLL output, CPU clock, APB clock
   - Implement a `RecalculateClocks()` method that recomputes all derived frequencies when any source register changes
   - Expose clock frequency queries to other peripheral models

2. **RTC_CNTL Clock Registers**:
   - Implement `RTC_CNTL_CLK_CONF_REG` with CPU clock source select and divider fields
   - Track PLL enable/frequency state
   - Report correct values when firmware reads back the configuration
   - `RTC_CNTL_STORE4_REG` / `STORE5_REG` should be simple read-write storage (firmware uses them to cache XTAL frequency)

3. **APB Control Registers**:
   - Implement `APB_CTRL_SYSCLK_CONF_REG` with pre-divider and source select
   - REF_TICK divider registers should compute 1 MHz from APB clock

4. **DPORT Clock Gating**:
   - `DPORT_PERIP_CLK_EN_REG` and `DPORT_PERIP_RST_EN_REG` control per-peripheral clocking
   - In emulation, clock gating can be simplified: track the enable bits, and optionally use them to prevent peripheral access when clock is gated
   - For initial implementation, allowing all peripheral access regardless of clock gate state is acceptable

5. **Boot Sequence Expectations**:
   - ROM bootloader reads XTAL frequency and stores it in RTC_CNTL_STORE4_REG
   - Second-stage bootloader configures BBPLL and sets CPU frequency
   - Firmware (esp-idf `esp_clk_init()`) configures final clock settings
   - Registers must return sane default values at reset (XTAL as clock source, PLL off)

6. **Peripheral Clock Queries**:
   - Other peripheral models (UART, SPI, I2C, etc.) need to know the APB clock frequency to calculate baud rates and timing
   - Implement a shared clock state object or interface that peripherals can query

## Complexity Assessment

**Overall Complexity: MEDIUM**

| Aspect                      | Difficulty | Notes                                            |
| --------------------------- | ---------- | ------------------------------------------------ |
| PLL configuration registers | Medium     | Mostly state tracking and readback               |
| Clock derivation logic      | Medium     | Straightforward divider chains                   |
| APB/CPU clock computation   | Low        | Small number of well-defined frequency combos    |
| DPORT clock gating          | Low        | Bit field tracking; gating enforcement optional  |
| Boot compatibility          | Medium     | Must report correct defaults for boot to proceed |
| Inter-peripheral clock API  | Medium     | Other models need to query clock frequencies     |
| RTC clock sources           | Low        | Mostly relevant for deep sleep (lower priority)  |
| Audio PLL (APLL)            | Low-Medium | Only needed for I2S audio applications           |

**Estimated effort**: 1-2 weeks for a functional implementation supporting boot and basic peripheral clocking.

**Priority**: HIGH -- clock registers are read very early in boot. If the CPU clock source register does not report a valid state, the bootloader and esp-idf startup code can hang or misconfigure peripherals. APB clock frequency is needed by every peripheral driver for timing calculations.

**Dependencies**:
- DPORT register model (clock gating registers live in DPORT)
- RTC_CNTL register block

**Risk factors**:
- Firmware may detect unexpected clock states and enter error paths
- Baud rate calculations depend on accurate APB clock reporting
- Some firmware uses calibration routines that measure actual clock frequency against a reference; these may need special handling or stubbing
