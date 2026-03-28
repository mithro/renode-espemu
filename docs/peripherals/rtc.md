# ESP32 RTC Controller

## Overview

The ESP32 RTC (Real-Time Clock) Controller is a multi-function peripheral that manages several critical system-level functions:

1. **System Clock Configuration**: Controls PLL, XTAL, and internal RC oscillator selection; generates the main CPU clock, APB clock, and RTC slow/fast clocks
2. **Reset Management**: Tracks and reports reset reasons (power-on, watchdog, deep sleep wakeup, software reset, etc.)
3. **Power Management**: Controls power domains for deep sleep, manages voltage regulators, and handles power-down sequences
4. **Brownout Detection**: Monitors supply voltage and triggers interrupts or resets when voltage drops below configurable thresholds
5. **RTC Watchdog Timer (RWDT)**: A watchdog that operates from the RTC slow clock and survives deep sleep (see `docs/peripherals/watchdog.md` for detailed WDT documentation)
6. **ULP Coprocessor Control**: Manages the Ultra-Low-Power coprocessor that can run during deep sleep
7. **RTC GPIO**: Controls GPIO pins that remain active during deep sleep
8. **RTC Memory**: Manages 8 KB of RTC slow memory that retains data during deep sleep

The RTC controller is CRITICAL for boot. The very first operations after reset involve the RTC controller: the ROM bootloader reads the reset reason from RTC registers to determine the boot path (cold boot vs. wake from deep sleep), then configures the system clock (switching from the default internal RC oscillator to the crystal oscillator and PLL). Without correct RTC emulation, the ESP32 cannot complete its boot sequence.

## Hardware Specifications

### Clock System

| Clock Domain | Source Options              | Typical Frequency     | Usage                |
| ------------ | --------------------------- | --------------------- | -------------------- |
| CPU_CLK      | PLL, XTAL, RTC8M            | 80/160/240 MHz (PLL)  | CPU core clock       |
| APB_CLK      | Derived from CPU_CLK        | 80 MHz                | Peripheral bus clock |
| RTC_FAST_CLK | RTC8M, XTAL_DIV             | ~8 MHz                | RTC fast peripherals |
| RTC_SLOW_CLK | RTC150K, XTAL32K, RTC8M_DIV | ~150 kHz / 32.768 kHz | RTC timer, RWDT      |

### PLL Configuration

| PLL Setting | CPU Clock                   | APB Clock |
| ----------- | --------------------------- | --------- |
| PLL_320     | 80 MHz or 160 MHz           | 80 MHz    |
| PLL_480     | 80 MHz, 160 MHz, or 240 MHz | 80 MHz    |

### Reset Reasons

The RTC controller records the reason for the most recent reset in `RTC_CNTL_RESET_STATE_REG`:

| Reset Cause Code | Name             | Description                              |
| ---------------- | ---------------- | ---------------------------------------- |
| 0x01             | POWERON_RESET    | Power-on reset (initial power-up)        |
| 0x03             | SW_RESET         | Software reset via `RTC_CNTL_SW_SYS_RST` |
| 0x04             | OWDT_RESET       | Legacy watchdog reset                    |
| 0x05             | DEEPSLEEP_RESET  | Wake from deep sleep                     |
| 0x06             | SDIO_RESET       | SDIO reset                               |
| 0x07             | TG0WDT_SYS_RESET | Timer Group 0 WDT system reset           |
| 0x08             | TG1WDT_SYS_RESET | Timer Group 1 WDT system reset           |
| 0x09             | RTCWDT_SYS_RESET | RTC WDT system reset                     |
| 0x0A             | INTRUSION_RESET  | Intrusion test reset                     |
| 0x0B             | TGWDT_CPU_RESET  | Timer Group WDT CPU reset                |
| 0x0C             | SW_CPU_RESET     | Software CPU reset                       |
| 0x0D             | RTCWDT_CPU_RESET | RTC WDT CPU reset                        |
| 0x0E             | EXT_CPU_RESET    | External CPU reset                       |
| 0x0F             | RTCWDT_BROWN_OUT | Brownout reset                           |
| 0x10             | RTCWDT_RTC_RESET | RTC reset                                |

### Base Addresses

| Peripheral                | Base Address | Size    |
| ------------------------- | ------------ | ------- |
| RTC Controller (RTC_CNTL) | 0x3FF48000   | 0x0100+ |
| RTC I/O (RTC_IO)          | 0x3FF48400   | 0x0100+ |
| RTC I2C                   | 0x3FF48800   |         |

### Power Domains

| Domain          | Controls                     | Deep Sleep Behavior                       |
| --------------- | ---------------------------- | ----------------------------------------- |
| RTC Power       | RTC peripherals, RTC memory  | Always on                                 |
| Digital Core    | CPU, digital peripherals     | Powered down in deep sleep                |
| WiFi            | WiFi RF/baseband             | Powered down in deep sleep (configurable) |
| RTC Peripherals | RTC GPIO, touch sensors, ULP | Configurable                              |
| RTC Fast Memory | 8 KB fast RTC memory         | Configurable                              |
| RTC Slow Memory | 8 KB slow RTC memory         | Configurable                              |

### Brownout Detector

| Feature            | Specification                                       |
| ------------------ | --------------------------------------------------- |
| Voltage Thresholds | 7 levels, configurable (approximately 2.1V to 2.7V) |
| Actions            | Interrupt, system reset, or both                    |
| Flash Protection   | Can disable flash when brownout detected            |
| Enable/Disable     | Software configurable                               |

## TRM Chapter Reference

**ESP32 Technical Reference Manual, Chapter 26: RTC Controller (Not Low-Power Management)**

- [ESP32 TRM PDF](https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf)

Note: Despite the "RTC" name, this peripheral is more of a system controller than a traditional real-time clock. The TRM chapter focuses on clock generation, reset control, and power management.

### Key Sections

| Section | Topic                                            |
| ------- | ------------------------------------------------ |
| 26.1    | System clock overview and configuration          |
| 26.2    | Reset sources and reset state                    |
| 26.3    | Power management and deep sleep                  |
| 26.4    | RTC timer (48-bit counter for deep sleep timing) |
| 26.5    | Brownout detector                                |
| 26.6    | Register descriptions                            |

### Critical TRM Details

1. **Boot Clock Sequence**: After power-on reset, the system starts with the internal RC oscillator (~8 MHz for REF_TICK). The ROM bootloader switches to the 40 MHz crystal (XTAL), then the second-stage bootloader configures the PLL for the target CPU frequency (80/160/240 MHz).

2. **Reset Reason Registers**: Two fields in `RTC_CNTL_RESET_STATE_REG` track reset reasons independently for PRO_CPU and APP_CPU. Firmware reads these at the very beginning of boot to determine the boot path.

3. **RTC Timer**: A 48-bit counter running from RTC_SLOW_CLK that provides timing across deep sleep cycles. Used by the system time functions to maintain wall-clock time across sleep/wake.

4. **Store Registers**: `RTC_CNTL_STORE0_REG` through `RTC_CNTL_STORE7_REG` are general-purpose registers that retain their values across deep sleep. ESP-IDF uses specific store registers for well-known purposes:
   - STORE0: Deep sleep wake stub entry point
   - STORE4-STORE7: Used by bootloader and ESP-IDF for boot state tracking

## Register Map Summary

### Clock Configuration Registers

| Register                  | Offset | Description                                               |
| ------------------------- | ------ | --------------------------------------------------------- |
| RTC_CNTL_CLK_CONF_REG     | 0x0070 | Clock configuration (SOC_CLK_SEL, fast/slow clock source) |
| RTC_CNTL_TIMER1_REG       | 0x001C | PLL calibration wait times                                |
| RTC_CNTL_TIMER2_REG       | 0x0020 | ULP wakeup timer                                          |
| RTC_CNTL_OPTIONS0_REG     | 0x0000 | System options (SW reset trigger)                         |
| RTC_CNTL_SW_CPU_STALL_REG | 0x00AC | CPU stall control                                         |

### Key CLK_CONF_REG Fields

| Field            | Bits  | Description                                         |
| ---------------- | ----- | --------------------------------------------------- |
| SOC_CLK_SEL      | 28:27 | System clock source: 0=XTAL, 1=PLL, 2=RTC8M, 3=APLL |
| FAST_CLK_RTC_SEL | 29    | RTC fast clock: 0=XTAL_DIV, 1=RTC8M                 |
| ANA_CLK_RTC_SEL  | 31:30 | RTC slow clock: 0=RTC150K, 1=XTAL32K, 2=RTC8M_DIV   |
| CK8M_DIV_SEL     | 6:4   | 8MHz RC oscillator divider                          |

### Reset and State Registers

| Register                 | Offset | Description                                             |
| ------------------------ | ------ | ------------------------------------------------------- |
| RTC_CNTL_RESET_STATE_REG | 0x0034 | Reset reason for both CPUs                              |
| RTC_CNTL_STORE0_REG      | 0x004C | General-purpose store register 0 (deep sleep wake stub) |
| RTC_CNTL_STORE1_REG      | 0x0050 | General-purpose store register 1                        |
| RTC_CNTL_STORE2_REG      | 0x0054 | General-purpose store register 2                        |
| RTC_CNTL_STORE3_REG      | 0x0058 | General-purpose store register 3                        |
| RTC_CNTL_STORE4_REG      | 0x005C | General-purpose store register 4 (boot state)           |
| RTC_CNTL_STORE5_REG      | 0x0060 | General-purpose store register 5                        |
| RTC_CNTL_STORE6_REG      | 0x0064 | General-purpose store register 6                        |
| RTC_CNTL_STORE7_REG      | 0x0068 | General-purpose store register 7                        |

### Key RESET_STATE_REG Fields

| Field                  | Bits | Description                                 |
| ---------------------- | ---- | ------------------------------------------- |
| RESET_CAUSE_PROCPU     | 5:0  | PRO_CPU reset reason code                   |
| RESET_CAUSE_APPCPU     | 11:6 | APP_CPU reset reason code                   |
| STAT_VECTOR_SEL_PROCPU | 13   | PRO_CPU reset vector selection (ROM or RTC) |
| STAT_VECTOR_SEL_APPCPU | 14   | APP_CPU reset vector selection              |

### Power Management Registers

| Register                  | Offset | Description                                            |
| ------------------------- | ------ | ------------------------------------------------------ |
| RTC_CNTL_SLP_TIMER0_REG   | 0x0004 | Deep sleep wakeup timer (low 32 bits)                  |
| RTC_CNTL_SLP_TIMER1_REG   | 0x0008 | Deep sleep wakeup timer (high 16 bits)                 |
| RTC_CNTL_STATE0_REG       | 0x0018 | Sleep/wakeup state machine control                     |
| RTC_CNTL_WAKEUP_STATE_REG | 0x003C | Wakeup enable mask (which sources can wake from sleep) |
| RTC_CNTL_DIG_PWC_REG      | 0x0080 | Digital power control                                  |
| RTC_CNTL_DIG_ISO_REG      | 0x0084 | Digital domain isolation                               |
| RTC_CNTL_PWC_REG          | 0x0088 | RTC power control                                      |

### Brownout Detection Registers

| Register               | Offset | Description                     |
| ---------------------- | ------ | ------------------------------- |
| RTC_CNTL_BROWN_OUT_REG | 0x00D4 | Brownout detector configuration |

### Key BROWN_OUT_REG Fields

| Field           | Bits  | Description                          |
| --------------- | ----- | ------------------------------------ |
| CLOSE_FLASH_ENA | 6     | Close flash on brownout              |
| PD_RF_ENA       | 5     | Power down RF on brownout            |
| RST_WAIT        | 14:7  | Reset wait cycles                    |
| RST_ENA         | 15    | Enable reset on brownout             |
| THRES           | 18:16 | Voltage threshold selection (0-7)    |
| ENA             | 19    | Brownout detector enable             |
| DET             | 31    | Brownout detected (read-only status) |

### RTC Watchdog Registers

See `docs/peripherals/watchdog.md` for detailed RWDT register documentation.

| Register                 | Offset | Description           |
| ------------------------ | ------ | --------------------- |
| RTC_CNTL_WDTCONFIG0_REG  | 0x008C | RWDT configuration    |
| RTC_CNTL_WDTCONFIG1_REG  | 0x0090 | Stage 0 timeout       |
| RTC_CNTL_WDTCONFIG2_REG  | 0x0094 | Stage 1 timeout       |
| RTC_CNTL_WDTCONFIG3_REG  | 0x0098 | Stage 2 timeout       |
| RTC_CNTL_WDTCONFIG4_REG  | 0x009C | Stage 3 timeout       |
| RTC_CNTL_WDTFEED_REG     | 0x00A0 | RWDT feed register    |
| RTC_CNTL_WDTWPROTECT_REG | 0x00A4 | RWDT write protection |

### Interrupt Registers

| Register             | Offset | Description             |
| -------------------- | ------ | ----------------------- |
| RTC_CNTL_INT_RAW_REG | 0x0040 | Raw interrupt status    |
| RTC_CNTL_INT_ST_REG  | 0x0044 | Masked interrupt status |
| RTC_CNTL_INT_ENA_REG | 0x0048 | Interrupt enable        |
| RTC_CNTL_INT_CLR_REG | 0x004C | Interrupt clear         |

### RTC Interrupt Sources

| Bit | Source     | Description       |
| --- | ---------- | ----------------- |
| 0   | SLP_WAKEUP | Wake from sleep   |
| 1   | SLP_REJECT | Sleep rejected    |
| 2   | SDIO_IDLE  | SDIO idle         |
| 3   | WDT        | RTC watchdog      |
| 4   | TIME_VALID | RTC time valid    |
| 6   | ULP_CP     | ULP coprocessor   |
| 7   | TOUCH      | Touch pad         |
| 8   | BROWN_OUT  | Brownout detected |
| 9   | MAIN_TIMER | RTC main timer    |

## Source Code References

### SOC Register Definitions

- **RTC Controller Registers**: [components/soc/esp32/register/soc/rtc_cntl_reg.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/rtc_cntl_reg.h)
  - Complete register definitions for the RTC controller
  - Includes clock configuration, reset state, power management, brownout, and RWDT registers
  - Defines all bit field masks and shift values

- **RTC Controller Struct**: [components/soc/esp32/register/soc/rtc_cntl_struct.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/rtc_cntl_struct.h)
  - C struct overlay for memory-mapped register access
  - Bit-field definitions for each register

- **RTC I/O Registers**: [components/soc/esp32/register/soc/rtc_io_reg.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/rtc_io_reg.h)
  - RTC GPIO pad configuration registers
  - Controls pin function selection for RTC-domain GPIOs
  - Touch pad, ADC, and DAC configuration that shares RTC I/O space

### HAL (Hardware Abstraction Layer)

- **RTC Controller LL**: [components/esp_hal_pmu/esp32/include/hal/rtc_cntl_ll.h](https://github.com/espressif/esp-idf/blob/master/components/esp_hal_pmu/esp32/include/hal/rtc_cntl_ll.h)
  - Low-level inline functions for RTC controller operations
  - Key functions:
    - `rtc_cntl_ll_set_wakeup_timer()` - deep sleep timer configuration
    - `rtc_cntl_ll_get_reset_reason()` - reads reset reason for each CPU
    - `rtc_cntl_ll_set_cpu_retention()` - CPU state retention control
    - `rtc_cntl_ll_sleep_enable()` - initiates sleep sequence

- **Brownout LL**: [components/esp_hal_pmu/esp32/include/hal/brownout_ll.h](https://github.com/espressif/esp-idf/blob/master/components/esp_hal_pmu/esp32/include/hal/brownout_ll.h)
  - Low-level brownout detector control
  - Key functions:
    - `brownout_ll_set_threshold()` - voltage threshold configuration
    - `brownout_ll_enable()` / `brownout_ll_disable()` - enable control
    - `brownout_ll_reset_enable()` - enable reset on brownout
    - `brownout_ll_detect()` - read brownout detection status

- **Clock Tree LL**: [components/esp_hal_clock/esp32/include/hal/clk_tree_ll.h](https://github.com/espressif/esp-idf/blob/master/components/esp_hal_clock/esp32/include/hal/clk_tree_ll.h)
  - System clock configuration functions
  - Key functions:
    - `clk_ll_cpu_set_src()` - select CPU clock source (XTAL, PLL, RTC8M)
    - `clk_ll_cpu_set_freq_mhz_from_pll()` - set CPU frequency from PLL
    - `clk_ll_bbpll_set_config()` - PLL configuration (320MHz or 480MHz)
    - `clk_ll_xtal_load_freq_mhz()` / `clk_ll_xtal_store_freq_mhz()` - crystal frequency stored in RTC STORE registers
    - `clk_ll_rtc_slow_set_src()` - RTC slow clock source selection
    - `clk_ll_rtc_fast_set_src()` - RTC fast clock source selection
  - This is the most important reference for clock configuration emulation

### QEMU Implementation

- **RTC Controller Model**: [hw/misc/esp32_rtc_cntl.c](https://github.com/espressif/qemu/blob/esp-develop/hw/misc/esp32_rtc_cntl.c)
  - Complete RTC controller emulation
  - Key implementation details:
    - **Reset Reason**: Initializes with POWERON_RESET, updates on different reset types
    - **Clock Configuration**: Tracks SOC_CLK_SEL and reports back configured values; actual clock frequency changes are coordinated with the CPU model
    - **Store Registers**: Simple read/write storage that persists across soft resets
    - **RWDT**: Integrated watchdog timer (see watchdog.md)
    - **Brownout**: Typically stubbed (never triggers in emulation since supply voltage is not simulated)
    - **Deep Sleep**: Intercepted and either emulated or handled as a pause-and-resume
    - **Power Domain Control**: Register writes accepted, power state tracked, but all domains effectively always-on in emulation

## Renode Implementation Analysis

### Existing Renode Models (Reference)

- **STM32F4_RTC**: [Peripherals/Timers/STM32F4_RTC.cs](https://github.com/renode/renode-infrastructure/blob/master/src/Emulator/Peripherals/Peripherals/Timers/STM32F4_RTC.cs)
  - RTC peripheral with calendar functions, alarm, and wakeup timer
  - Shows patterns for:
    - Write protection (similar key-unlock mechanism)
    - Register initialization with reset values
    - Multiple sub-functions in one peripheral model
  - Less applicable to clock/power management aspects

### Recommended Implementation Approach

#### Architecture

The RTC controller is large and multi-functional. A phased approach is recommended:

```
ESP32_RTCController : IDoubleWordPeripheral, IKnownSize
├── Clock Configuration
│   ├── SOC_CLK_SEL tracking
│   ├── PLL state (on/off, frequency)
│   ├── XTAL state
│   └── RTC slow/fast clock selection
├── Reset Management
│   ├── Reset reason registers (per CPU)
│   ├── Software reset trigger
│   └── Reset vector selection
├── Store Registers
│   └── STORE0-STORE7 (simple read/write)
├── Power Management (stub)
│   ├── Power domain state tracking
│   └── Deep sleep state machine
├── Brownout Detector (stub)
│   └── Configuration registers (never triggers)
├── RWDT (see watchdog.md)
│   └── ESP32_RWDT instance
├── RTC Timer
│   └── 48-bit counter for timekeeping
└── Interrupt Controller
    └── RTC interrupt sources and masking
```

#### Phase 1: Minimal Boot Support (CRITICAL)

These components must work for the ESP32 to boot:

1. **Reset Reason Registers**:
   - Initialize `RESET_CAUSE_PROCPU` = `0x01` (POWERON_RESET) on cold start
   - Initialize `RESET_CAUSE_APPCPU` = `0x01` (POWERON_RESET) on cold start
   - Support reading these fields (firmware reads very early in boot)
   - The reset vector selection bits (`STAT_VECTOR_SEL`) must default correctly

2. **Clock Configuration (SOC_CLK_SEL)**:
   - Track the `SOC_CLK_SEL` field in `CLK_CONF_REG`
   - Initial value: 0 (XTAL) -- after ROM bootloader, this switches to 1 (PLL)
   - Firmware reads this to determine current clock source
   - In emulation, actual CPU speed doesn't change, but the register must reflect firmware writes

3. **Store Registers (STORE0-STORE7)**:
   - Simple 32-bit read/write registers
   - STORE4 is read by bootloader for boot state
   - STORE register containing XTAL frequency must return valid value (firmware uses `clk_ll_xtal_load_freq_mhz()`)
   - Zero-initialize on power-on reset

4. **OPTIONS0_REG (Software Reset)**:
   - Writing `RTC_CNTL_SW_SYS_RST` bit triggers system reset
   - Must connect to Renode's machine reset mechanism

#### Phase 2: Extended Functionality

5. **Power Domain Registers**:
   - Accept writes to `DIG_PWC_REG`, `DIG_ISO_REG`, `PWC_REG`
   - Track state for readback
   - In emulation, all domains remain powered (no actual power gating)

6. **Brownout Detector**:
   - Accept configuration writes to `BROWN_OUT_REG`
   - Never assert brownout detection (DET bit always 0)
   - Never trigger brownout reset or interrupt

7. **RTC Timer (48-bit counter)**:
   - Implement as a free-running counter
   - Used for system timekeeping across sleep cycles
   - Lower priority if deep sleep is not emulated

8. **CPU Stall Control**:
   - `SW_CPU_STALL_REG` allows one CPU to stall the other
   - Used during flash operations and some critical sections
   - Important for dual-core emulation

#### Phase 3: Deep Sleep Support

9. **Deep Sleep State Machine**:
   - Full sleep entry/exit sequence
   - Wakeup source configuration
   - Timer-based wakeup
   - Power domain sequencing

10. **ULP Coprocessor Control**:
    - ULP program start/stop
    - Shared memory access
    - ULP wakeup triggers

#### Key Firmware Interactions

1. **Very Early Boot (ROM Bootloader)**:
   ```
   1. Read RESET_CAUSE_PROCPU from RTC_CNTL_RESET_STATE_REG
   2. If DEEPSLEEP_RESET: check STORE0 for wake stub address
   3. If POWERON_RESET: normal boot path
   4. Configure initial clock: switch from RC to XTAL
   5. Store XTAL frequency in STORE register
   ```

2. **Clock Configuration (Second-Stage Bootloader)**:
   ```
   1. Read current SOC_CLK_SEL
   2. Configure PLL (bbpll_set_config):
      - Select PLL frequency (320 or 480 MHz based on target CPU freq)
      - Wait for PLL lock (calibration delay)
   3. Switch SOC_CLK_SEL to PLL
   4. Configure CPU frequency divider
   5. APB clock derived from CPU clock (always 80 MHz)
   ```

3. **Reset Reason Check (Application)**:
   ```
   1. esp_reset_reason() reads RTC_CNTL_RESET_STATE_REG
   2. Maps hardware reset code to esp_reset_reason_t enum
   3. Application uses this for recovery logic, logging, etc.
   ```

4. **Software Reset**:
   ```
   1. Firmware writes SW_SYS_RST bit in RTC_CNTL_OPTIONS0_REG
   2. System performs full reset
   3. On next boot, RESET_CAUSE reads as SW_RESET (0x03)
   ```

5. **Brownout Configuration (Application Init)**:
   ```
   1. Reads brownout configuration from efuse/menuconfig defaults
   2. Configures threshold in BROWN_OUT_REG
   3. Enables brownout detector
   4. Registers brownout ISR if configured
   ```

### Implementation Priority

| Component                     | Priority | Reason                             |
| ----------------------------- | -------- | ---------------------------------- |
| Reset reason registers        | CRITICAL | Read at very start of boot         |
| Store registers (STORE0-7)    | CRITICAL | Bootloader state and XTAL freq     |
| CLK_CONF_REG (SOC_CLK_SEL)    | CRITICAL | Clock source tracking              |
| OPTIONS0_REG (SW reset)       | HIGH     | Software reset support             |
| RWDT registers                | HIGH     | See watchdog.md, needed for boot   |
| RTC interrupt registers       | HIGH     | RTC interrupt sources              |
| Power domain registers (stub) | MEDIUM   | Accept writes, all domains stay on |
| Brownout registers (stub)     | MEDIUM   | Accept config, never trigger       |
| CPU stall control             | MEDIUM   | Dual-core operation                |
| RTC timer (48-bit)            | MEDIUM   | System timekeeping                 |
| Deep sleep state machine      | LOW      | Not needed for basic operation     |
| ULP control                   | LOW      | Specialized use cases only         |

## Complexity Assessment

### Overall Complexity: HIGH

#### Justification

| Factor                   | Rating      | Notes                                                                               |
| ------------------------ | ----------- | ----------------------------------------------------------------------------------- |
| Register complexity      | High        | Large register space (~100+ registers) with many sub-functions                      |
| Behavioral complexity    | Medium-High | Multiple independent sub-systems (clock, reset, power, brownout, WDT)               |
| Timing sensitivity       | Medium      | Clock switching must be consistent, but emulation can abstract timing               |
| Boot criticality         | CRITICAL    | Reset reason and clock config are the very first things firmware accesses           |
| Stubbing viability       | High        | Many subsystems can be effectively stubbed (brownout, power management, deep sleep) |
| QEMU reference quality   | High        | Complete implementation available                                                   |
| Renode reference quality | Medium      | STM32F4_RTC covers some patterns but ESP32 RTC is much more complex                 |

#### Estimated Effort

- **Phase 1 Minimal boot (reset reason + clock + store regs)**: 2-3 days
  - Reset reason registers with correct power-on values
  - Clock configuration register tracking
  - Store registers as simple read/write
  - Software reset trigger
  - RWDT stub (see watchdog.md)

- **Phase 2 Extended (power domains + brownout stubs)**: 2-3 days additional
  - Power domain register stubs
  - Brownout detector stub
  - CPU stall control
  - RTC timer

- **Phase 3 Deep sleep**: 3-5 days additional
  - Full sleep entry/exit state machine
  - Wakeup source configuration
  - Power domain sequencing
  - ULP coprocessor control

- **Total for complete implementation**: 7-11 days

#### Key Risks

1. **Register Space Size**: The RTC controller has a very large register space. Missing or incorrectly defaulted registers can cause boot failures that are difficult to diagnose.

2. **Clock Configuration Dependencies**: Other peripherals (timers, UARTs, SPI) derive their clocks from the system clock tree. The RTC controller's clock configuration must be consistent with how other peripheral models calculate their operating frequencies.

3. **Reset Reason Accuracy**: If the reset reason doesn't match what firmware expects, boot path selection may go wrong (e.g., trying to execute a deep sleep wake stub when there is none).

4. **Store Register Initialization**: Some store registers have expected values after ROM bootloader execution. If the emulation starts after ROM boot (e.g., loading an application binary directly), these registers must be pre-initialized with appropriate values.

5. **Crystal Frequency Storage**: ESP-IDF stores the detected crystal frequency in a store register. If this reads as 0 or an invalid value, clock configuration calculations will be wrong, leading to incorrect peripheral timing.

6. **DPORT Integration**: Some RTC-related configuration is accessed through DPORT registers, requiring coordination between the RTC controller model and the DPORT peripheral model.
