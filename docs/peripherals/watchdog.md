# ESP32 Watchdog Timers

## Overview

The ESP32 implements three watchdog timer (WDT) instances across two subsystems:

1. **Main Watchdog Timer 0 (MWDT0)** -- located in Timer Group 0
2. **Main Watchdog Timer 1 (MWDT1)** -- located in Timer Group 1
3. **RTC Watchdog Timer (RWDT)** -- located in the RTC Controller

The MWDTs operate from the APB clock (80 MHz) and are the primary watchdog mechanism during normal operation. The RWDT operates from the RTC slow clock (~150 kHz internal RC oscillator or 32.768 kHz external crystal) and remains active during deep sleep, providing a last-resort recovery mechanism.

All three WDTs share a similar multi-stage architecture: each has 4 programmable timeout stages, where each stage can be independently configured to take one of several actions (interrupt, CPU reset, system reset, or disabled). This staged approach allows escalating responses -- for example, Stage 0 generates an interrupt (allowing software recovery), Stage 1 performs a CPU reset, and Stage 2 performs a full system reset.

During the ESP32 boot process, the bootloader enables the RTC WDT as a safety net. If the application fails to initialize within the timeout, the RWDT forces a system reset. Firmware must either feed (reset) the watchdogs or explicitly disable them during boot. This makes correct WDT emulation important for boot success.

## Hardware Specifications

### MWDT (Main Watchdog Timer) -- in Timer Groups

| Feature            | Specification                                                   |
| ------------------ | --------------------------------------------------------------- |
| Instances          | 2 (one per Timer Group: TG0, TG1)                               |
| Clock Source       | APB_CLK (80 MHz typical)                                        |
| Prescaler          | Internal, divides APB clock (configurable)                      |
| Timer Width        | 32-bit countdown                                                |
| Number of Stages   | 4 (Stage 0 through Stage 3)                                     |
| Stage Actions      | Disabled (0), Interrupt (1), CPU Reset (2), System Reset (3)    |
| Write Protection   | Yes, requires writing key value 0x50D83AA1 to WDT_WKEY register |
| Feed Mechanism     | Write to WDT_FEED register (after unlocking write protection)   |
| CPU Reset Duration | Configurable via SYS_RESET_LENGTH and CPU_RESET_LENGTH fields   |

### RWDT (RTC Watchdog Timer) -- in RTC Controller

| Feature             | Specification                                                               |
| ------------------- | --------------------------------------------------------------------------- |
| Instances           | 1                                                                           |
| Clock Source        | RTC_SLOW_CLK (~150 kHz RC or 32.768 kHz XTAL)                               |
| Timer Width         | 32-bit countdown                                                            |
| Number of Stages    | 4 (Stage 0 through Stage 3)                                                 |
| Stage Actions       | Disabled (0), Interrupt (1), CPU Reset (2), System Reset (3), RTC Reset (4) |
| Write Protection    | Yes, requires writing key value 0x50D83AA1 to RTC_CNTL_WDTWPROTECT_REG      |
| Feed Mechanism      | Write to RTC_CNTL_WDTFEED_REG                                               |
| Survives Deep Sleep | Yes                                                                         |
| Additional Action   | RTC Reset (resets RTC domain, unique to RWDT)                               |

### Base Addresses

| WDT   | Located In     | Base Address | WDT Register Offset |
| ----- | -------------- | ------------ | ------------------- |
| MWDT0 | Timer Group 0  | 0x3FF5F000   | 0x0048              |
| MWDT1 | Timer Group 1  | 0x3FF60000   | 0x0048              |
| RWDT  | RTC Controller | 0x3FF48000   | 0x008C              |

### Stage Timeout Architecture

Each WDT has 4 stages that execute sequentially. When the WDT is not fed within the Stage 0 timeout, it progresses to Stage 1, and so on:

```
┌──────────┐    timeout    ┌──────────┐    timeout    ┌──────────┐    timeout    ┌──────────┐
│ Stage 0  │──────────────>│ Stage 1  │──────────────>│ Stage 2  │──────────────>│ Stage 3  │
│ (action) │               │ (action) │               │ (action) │               │ (action) │
└──────────┘               └──────────┘               └──────────┘               └──────────┘
     ^
     │ feed (reset)
```

Feeding the WDT resets it back to Stage 0. Each stage has its own independent timeout value and action.

### Typical Configuration (ESP-IDF defaults)

| WDT                   | Stage 0                     | Stage 1   | Stage 2  | Stage 3  |
| --------------------- | --------------------------- | --------- | -------- | -------- |
| Task WDT (MWDT0)      | Interrupt (print backtrace) | Disabled  | Disabled | Disabled |
| Bootloader WDT (RWDT) | System Reset                | Disabled  | Disabled | Disabled |
| Interrupt WDT (MWDT1) | Interrupt                   | CPU Reset | Disabled | Disabled |

## TRM Chapter Reference

**ESP32 Technical Reference Manual, Chapter 17: Timer Group (TIMG)** -- Section on Watchdog Timers

- [ESP32 TRM PDF](https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf)
- MWDT is documented as part of the Timer Group chapter (Chapter 17)
- RWDT is documented as part of the RTC Controller chapter (Chapter 26)

### Key TRM Details

1. **Write Protection**: All WDT configuration registers are write-protected. Firmware must write the magic value `0x50D83AA1` to the write-key register before any configuration changes, and write any other value to re-lock.

2. **Feed Operation**: Writing to the feed register resets the WDT counter and returns to Stage 0. The feed register is also write-protected.

3. **Flash Boot Protection**: The RWDT is configured by the first-stage bootloader before jumping to the application. If the application hangs during initialization, the RWDT resets the system.

4. **Stage Action Encoding**:
   - 0: Off (stage disabled)
   - 1: Interrupt
   - 2: CPU reset
   - 3: System reset
   - 4: RTC reset (RWDT only)

## Register Map Summary

### MWDT Registers (within Timer Group, offset from group base)

| Register              | Offset | Description                                              |
| --------------------- | ------ | -------------------------------------------------------- |
| TIMGn_WDTCONFIG0_REG  | 0x0048 | WDT configuration: stage actions, prescaler mode, enable |
| TIMGn_WDTCONFIG1_REG  | 0x004C | WDT prescaler value (clock divider)                      |
| TIMGn_WDTCONFIG2_REG  | 0x0050 | Stage 0 timeout value                                    |
| TIMGn_WDTCONFIG3_REG  | 0x0054 | Stage 1 timeout value                                    |
| TIMGn_WDTCONFIG4_REG  | 0x0058 | Stage 2 timeout value                                    |
| TIMGn_WDTCONFIG5_REG  | 0x005C | Stage 3 timeout value                                    |
| TIMGn_WDTFEED_REG     | 0x0060 | Write to feed (reset) watchdog                           |
| TIMGn_WDTWPROTECT_REG | 0x0064 | Write protection key register                            |

### MWDT CONFIG0 Key Fields

| Field                | Bits  | Description                                                   |
| -------------------- | ----- | ------------------------------------------------------------- |
| WDT_EN               | 31    | WDT enable                                                    |
| WDT_STG0             | 30:29 | Stage 0 action (0=off, 1=interrupt, 2=CPU reset, 3=sys reset) |
| WDT_STG1             | 28:27 | Stage 1 action                                                |
| WDT_STG2             | 26:25 | Stage 2 action                                                |
| WDT_STG3             | 24:23 | Stage 3 action                                                |
| WDT_CPU_RESET_LENGTH | 22:20 | CPU reset signal duration                                     |
| WDT_SYS_RESET_LENGTH | 19:17 | System reset signal duration                                  |
| WDT_FLASHBOOT_MOD_EN | 14    | Flash boot mode enable (uses fixed timeout for boot safety)   |

### RWDT Registers (within RTC Controller, offset from RTC base 0x3FF48000)

| Register                 | Offset | Description                                                      |
| ------------------------ | ------ | ---------------------------------------------------------------- |
| RTC_CNTL_WDTCONFIG0_REG  | 0x008C | WDT config: enable, stage actions, pause in sleep, reset lengths |
| RTC_CNTL_WDTCONFIG1_REG  | 0x0090 | Stage 0 timeout value                                            |
| RTC_CNTL_WDTCONFIG2_REG  | 0x0094 | Stage 1 timeout value                                            |
| RTC_CNTL_WDTCONFIG3_REG  | 0x0098 | Stage 2 timeout value                                            |
| RTC_CNTL_WDTCONFIG4_REG  | 0x009C | Stage 3 timeout value                                            |
| RTC_CNTL_WDTFEED_REG     | 0x00A0 | Write to feed (reset) RTC watchdog                               |
| RTC_CNTL_WDTWPROTECT_REG | 0x00A4 | Write protection key register                                    |

### RWDT CONFIG0 Key Fields

| Field                | Bits  | Description                                       |
| -------------------- | ----- | ------------------------------------------------- |
| WDT_EN               | 31    | RTC WDT enable                                    |
| WDT_STG0             | 30:28 | Stage 0 action (3-bit: adds RTC reset option = 4) |
| WDT_STG1             | 27:25 | Stage 1 action                                    |
| WDT_STG2             | 24:22 | Stage 2 action                                    |
| WDT_STG3             | 21:19 | Stage 3 action                                    |
| WDT_CPU_RESET_LENGTH | 18:16 | CPU reset signal duration                         |
| WDT_SYS_RESET_LENGTH | 15:13 | System reset signal duration                      |
| WDT_PAUSE_IN_SLP     | 12    | Pause WDT during sleep                            |
| WDT_FLASHBOOT_MOD_EN | 11    | Flash boot protection mode                        |

## Source Code References

### SOC Register Definitions

- **Timer Group WDT Registers**: [components/soc/esp32/register/soc/timer_group_reg.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/timer_group_reg.h)
  - Contains MWDT register offset definitions within the timer group
  - Defines `TIMG_WDTCONFIG0_REG` through `TIMG_WDTWPROTECT_REG`
  - Write protection key value: `TIMG_WDT_WKEY_VALUE = 0x50D83AA1`

- **Timer Group Struct**: [components/soc/esp32/register/soc/timer_group_struct.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/timer_group_struct.h)
  - Struct definitions for MWDT registers with bit-field breakdowns

- **RTC Controller WDT Registers**: [components/soc/esp32/register/soc/rtc_cntl_reg.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/rtc_cntl_reg.h)
  - Contains RWDT register definitions within the RTC controller
  - Defines `RTC_CNTL_WDTCONFIG0_REG` through `RTC_CNTL_WDTWPROTECT_REG`

### HAL (Hardware Abstraction Layer)

- **Timer LL HAL** (includes MWDT): [components/esp_hal_timg/esp32/include/hal/timer_ll.h](https://github.com/espressif/esp-idf/blob/master/components/esp_hal_timg/esp32/include/hal/timer_ll.h)
  - Inline functions for MWDT operations
  - Key functions:
    - `mwdt_ll_enable()` / `mwdt_ll_disable()` - WDT enable control
    - `mwdt_ll_set_prescaler()` - clock prescaler
    - `mwdt_ll_set_timeout_behavior()` - per-stage action configuration
    - `mwdt_ll_set_timeout()` - per-stage timeout value
    - `mwdt_ll_feed()` - feed (reset) the watchdog
    - `mwdt_ll_write_protect_enable()` / `_disable()` - write protection

### API Documentation

- **Watchdog Timers**: [ESP-IDF WDT API](https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-reference/system/wdts.html)
  - Documents the three types of WDT usage in ESP-IDF:
    - **Interrupt Watchdog Timer (IWDT)**: Uses MWDT1 (TG1), monitors that ISRs are not blocked too long
    - **Task Watchdog Timer (TWDT)**: Uses MWDT0 (TG0), monitors that tasks are running (user-configurable)
    - **RTC Watchdog**: Used by bootloader for boot protection, typically disabled by application
  - Configuration via `menuconfig` (Kconfig options)
  - Panic handler behavior on WDT timeout

### QEMU Implementation

- **Timer Group (includes MWDT)**: [hw/timer/esp32_timg.c](https://github.com/espressif/qemu/blob/esp-develop/hw/timer/esp32_timg.c)
  - MWDT emulation is integrated into the timer group model
  - Key implementation details:
    - Write protection check before all config writes
    - Stage progression with configurable timeouts
    - Feed operation resets to Stage 0
    - System reset action triggers QEMU system reset
    - Flash boot mode with fixed timeout
    - Interrupt generation for Stage action = 1

- **RTC Controller (includes RWDT)**: [hw/misc/esp32_rtc_cntl.c](https://github.com/espressif/qemu/blob/esp-develop/hw/misc/esp32_rtc_cntl.c)
  - RWDT emulation within the RTC controller
  - Similar stage-based architecture to MWDT

## Renode Implementation Analysis

### Existing Renode Models (Reference)

Renode does not have a direct ESP32 watchdog model, but provides general patterns:

- **STM32_Timer**: [Peripherals/Timers/STM32_Timer.cs](https://github.com/renode/renode-infrastructure/blob/master/src/Emulator/Peripherals/Peripherals/Timers/STM32_Timer.cs)
  - While primarily a general-purpose timer, demonstrates timeout-based interrupt/reset patterns
  - Shows how to integrate timer peripherals with system reset

- **LiteX_Timer**: [Peripherals/Timers/LiteX_Timer.cs](https://github.com/renode/renode-infrastructure/blob/master/src/Emulator/Peripherals/Peripherals/Timers/LiteX_Timer.cs)
  - Simple countdown timer pattern applicable to WDT stage timeouts

### Recommended Implementation Approach

#### Architecture

The MWDT should be implemented as part of the Timer Group peripheral (see `timer-group.md`). The RWDT should be part of the RTC Controller peripheral (see `rtc.md`). Both share the same core WDT logic:

```
ESP32_WatchdogCore (shared logic)
├── 4x Stage (timeout value + action)
├── Write protection state machine
├── Feed mechanism
├── Prescaler
└── Stage progression timer

ESP32_TimerGroup
├── GPTimer x2
└── ESP32_MWDT (uses WatchdogCore)

ESP32_RTCController
├── ... (other RTC features)
└── ESP32_RWDT (uses WatchdogCore)
```

#### Core WDT Logic

1. **Write Protection State Machine**:
   - Maintain a `write_protected` flag (default: true)
   - On write to WPROTECT register:
     - If value == `0x50D83AA1`: set `write_protected = false`
     - Else: set `write_protected = true`
   - All config register writes check `write_protected` and silently discard if protected
   - This is critical -- firmware always unlocks before configuring, and re-locks after

2. **Stage Progression**:
   - Use a Renode `LimitTimer` configured to count down from the current stage's timeout
   - On timer expiry: execute current stage's action, advance to next stage, reload timer with next timeout
   - On feed: reset to Stage 0, reload timer with Stage 0 timeout
   - Stage actions:
     - **Off (0)**: Skip stage, immediately proceed to next
     - **Interrupt (1)**: Assert WDT interrupt line
     - **CPU Reset (2)**: Trigger CPU reset via machine reset mechanism
     - **System Reset (3)**: Trigger full system reset
     - **RTC Reset (4, RWDT only)**: Reset RTC domain

3. **Feed Operation**:
   - On write to FEED register (while unlocked): reset stage to 0, restart countdown
   - Simple but must be implemented correctly -- if feed is broken, WDT will constantly reset the system

4. **Flash Boot Mode**:
   - When `FLASHBOOT_MOD_EN` is set, the WDT uses a fixed timeout that is not configurable
   - Bootloader disables this mode once it takes over WDT configuration
   - Must be supported to prevent unexpected resets during emulated boot

#### Key Firmware Interactions

1. **Boot Sequence**:
   ```
   ROM bootloader:
     - RWDT enabled with flash boot mode
     - MWDT0 enabled with flash boot mode

   Second-stage bootloader:
     - Feeds RWDT
     - Configures RWDT with longer timeout
     - Disables flash boot mode

   Application startup:
     - Disables RWDT (or reconfigures)
     - Configures TWDT on MWDT0 (if enabled in menuconfig)
     - Configures IWDT on MWDT1 (if enabled in menuconfig)
   ```

2. **Task Watchdog Feed Cycle** (MWDT0):
   ```
   1. Write 0x50D83AA1 to WDTWPROTECT_REG (unlock)
   2. Write to WDTFEED_REG (feed)
   3. Write 0x00000000 to WDTWPROTECT_REG (re-lock)
   ```

3. **WDT Disable Sequence** (common during debugging or app init):
   ```
   1. Write 0x50D83AA1 to WDTWPROTECT_REG (unlock)
   2. Clear WDT_EN bit in WDTCONFIG0_REG
   3. Write 0x00000000 to WDTWPROTECT_REG (re-lock)
   ```

4. **Panic Handler**: On WDT timeout interrupt, ESP-IDF's panic handler prints CPU state and backtrace, then may trigger a reset.

### Implementation Strategy for Emulation

For initial boot support, the simplest viable approach is:

1. **Stub Mode**: Accept all WDT register writes, track enable state, but never actually trigger timeouts. This prevents WDT resets during emulation where timing may be imprecise. This is sufficient for boot and basic operation.

2. **Active Mode** (later): Implement actual countdown with stage progression. Required for testing WDT-dependent firmware logic, but not needed for basic boot or application execution.

### Implementation Priority

| Component                      | Priority | Reason                                       |
| ------------------------------ | -------- | -------------------------------------------- |
| Write protection state machine | CRITICAL | All WDT operations require unlock/lock       |
| MWDT enable/disable            | CRITICAL | Firmware disables WDT during boot            |
| RWDT enable/disable            | CRITICAL | Bootloader configures RWDT                   |
| Feed register (accept writes)  | CRITICAL | Prevents WDT resets                          |
| Flash boot mode disable        | HIGH     | Bootloader clears this flag                  |
| Stage configuration registers  | MEDIUM   | Stores config, may not need active countdown |
| Active stage countdown         | LOW      | Only needed for WDT-testing scenarios        |
| System reset action            | LOW      | Only needed with active countdown            |
| Interrupt action               | LOW      | Only needed with active countdown            |

## Complexity Assessment

### Overall Complexity: MEDIUM

#### Justification

| Factor                 | Rating     | Notes                                                                    |
| ---------------------- | ---------- | ------------------------------------------------------------------------ |
| Register complexity    | Medium     | Write-protected registers add state management                           |
| Behavioral complexity  | Medium     | Multi-stage progression with different actions                           |
| Timing sensitivity     | Low-Medium | WDT timeouts are coarse; exact timing less critical than GP timers       |
| Boot criticality       | HIGH       | Must handle WDT disable during boot or system resets constantly          |
| Stubbing viability     | High       | Stub implementation (accept writes, never fire) works for most use cases |
| QEMU reference quality | High       | Complete implementation available                                        |

#### Estimated Effort

- **Stub implementation (critical for boot)**: 1 day
  - Write protection state machine
  - All registers writable and readable
  - Enable/disable tracked but no actual countdown
  - Feed accepted but no-op

- **Full active implementation**: 2-3 days additional
  - Stage progression with countdown timers
  - All 4 stage actions (interrupt, CPU reset, system reset, RTC reset)
  - Flash boot mode with fixed timeout
  - Correct prescaler-based timing

#### Key Risks

1. **Write Protection Bypass**: If write protection is not implemented correctly, firmware WDT configuration will silently fail, leading to unexpected resets or inability to disable WDT
2. **Boot Timing**: If RWDT fires before bootloader has a chance to reconfigure it, the system will reset loop. The stub approach avoids this entirely.
3. **Flash Boot Mode**: The fixed-timeout boot protection mode must be clearable, or the system will reset during boot
4. **Interaction with Reset Controller**: WDT system reset must properly interact with the RTC controller's reset reason tracking (firmware reads reset reason on boot)
