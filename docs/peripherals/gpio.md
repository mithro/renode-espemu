# ESP32 GPIO, IO MUX, and GPIO Matrix

## Overview

The ESP32 GPIO subsystem is a three-layer architecture consisting of the GPIO Matrix, the IO MUX, and the GPIO controller itself. Together they provide flexible routing of peripheral signals to physical pins. The GPIO Matrix allows almost any peripheral signal to be mapped to any GPIO pin through a configurable crossbar switch, while the IO MUX provides a direct (lower-latency) connection for certain performance-critical signals. The GPIO controller manages pin state, direction, interrupts, and pull-up/pull-down resistors.

The ESP32 has 34 GPIO pins numbered 0-39, though not all are available on all packages. GPIOs 34-39 are input-only (no output driver, no internal pull-up/pull-down). GPIOs 6-11 are typically connected to the SPI flash and should not be used for other purposes. Each GPIO can be configured as input, output, or open-drain, with optional internal pull-up (45K ohm) or pull-down (45K ohm) resistors. GPIO interrupts are supported on all pins with configurable trigger types (rising edge, falling edge, both edges, low level, high level).

The GPIO Matrix is particularly important for emulation because ESP-IDF uses it extensively to route UART, SPI, I2C, and other peripheral signals to application-selected pins. During boot, the ROM and bootloader configure specific GPIO pins for flash SPI access and UART0 console output. Understanding the three-layer architecture is essential: peripheral signals connect to the GPIO Matrix input/output, the GPIO Matrix connects to the IO MUX, and the IO MUX connects to the physical pad.

## Hardware Specifications

- **Register base addresses:**
  - GPIO: `0x3FF44000` (size: `0x1000`)
  - IO MUX: `0x3FF49000` (size: `0x100`)
  - GPIO Sigma-Delta: `0x3FF44F00` (within GPIO register space)
- **Number of GPIO pins:** 34 (GPIO0-GPIO39; GPIO20, GPIO24, GPIO28-GPIO31 do not exist)
- **Input-only pins:** GPIO34, GPIO35, GPIO36, GPIO37, GPIO38, GPIO39
- **Output-capable pins:** GPIO0-GPIO33 (excluding non-existent pins)
- **GPIO Matrix:** 256 input signals, 256 output signals, fully configurable routing
- **IO MUX functions:** Each pin has up to 5 multiplexed functions (Function 0-4), Function 2 is always GPIO
- **Pull resistors:** Internal pull-up (~45K) and pull-down (~45K) on output-capable pins only
- **Drive strength:** 4 levels (5mA, 10mA, 20mA, 40mA)
- **Interrupt types:** Rising edge, falling edge, both edges, low level, high level (per-pin configurable)
- **Interrupt sources:** Two interrupt sources: GPIO_INTERRUPT_PRO and GPIO_INTERRUPT_APP (one per CPU core)
- **RTC GPIO:** GPIO32-GPIO39 can also be controlled by RTC subsystem (for deep sleep wakeup)

## TRM Chapter Reference

- **ESP32 Technical Reference Manual** Chapter 4: IO_MUX and GPIO Matrix
  - [PDF link](https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf)

## Register Map Summary

### GPIO Controller Registers (base `0x3FF44000`)

| Offset | Register | Purpose |
|--------|----------|---------|
| `0x004` | `GPIO_OUT_REG` | Output level for GPIO0-31 |
| `0x008` | `GPIO_OUT_W1TS_REG` | Write-1-to-set output for GPIO0-31 |
| `0x00C` | `GPIO_OUT_W1TC_REG` | Write-1-to-clear output for GPIO0-31 |
| `0x010` | `GPIO_OUT1_REG` | Output level for GPIO32-39 |
| `0x014` | `GPIO_OUT1_W1TS_REG` | Write-1-to-set output for GPIO32-39 |
| `0x018` | `GPIO_OUT1_W1TC_REG` | Write-1-to-clear output for GPIO32-39 |
| `0x020` | `GPIO_ENABLE_REG` | Output enable for GPIO0-31 |
| `0x024` | `GPIO_ENABLE_W1TS_REG` | Write-1-to-set output enable for GPIO0-31 |
| `0x028` | `GPIO_ENABLE_W1TC_REG` | Write-1-to-clear output enable for GPIO0-31 |
| `0x02C` | `GPIO_ENABLE1_REG` | Output enable for GPIO32-39 |
| `0x038` | `GPIO_STRAP_REG` | Boot strapping pin values (read-only, sampled at reset) |
| `0x03C` | `GPIO_IN_REG` | Input level for GPIO0-31 |
| `0x040` | `GPIO_IN1_REG` | Input level for GPIO32-39 |
| `0x044` | `GPIO_STATUS_REG` | Interrupt status for GPIO0-31 |
| `0x048` | `GPIO_STATUS_W1TS_REG` | Write-1-to-set interrupt status |
| `0x04C` | `GPIO_STATUS_W1TC_REG` | Write-1-to-clear interrupt status |
| `0x050` | `GPIO_STATUS1_REG` | Interrupt status for GPIO32-39 |
| `0x058` | `GPIO_ACPU_INT_REG` | APP CPU interrupt status for GPIO0-31 |
| `0x060` | `GPIO_ACPU_NMI_INT_REG` | APP CPU NMI status for GPIO0-31 |
| `0x068` | `GPIO_PCPU_INT_REG` | PRO CPU interrupt status for GPIO0-31 |
| `0x070` | `GPIO_PCPU_NMI_INT_REG` | PRO CPU NMI status for GPIO0-31 |
| `0x074-0x0F4` | `GPIO_PIN0_REG` - `GPIO_PIN39_REG` | Per-pin config: interrupt type, wakeup enable, pad driver |
| `0x130-0x52C` | `GPIO_FUNC0_IN_SEL_CFG_REG` - `GPIO_FUNC255_IN_SEL_CFG_REG` | GPIO Matrix: input signal routing (signal N -> which GPIO) |
| `0x530-0x5B0` | `GPIO_FUNC0_OUT_SEL_CFG_REG` - `GPIO_FUNC33_OUT_SEL_CFG_REG` | GPIO Matrix: output signal routing (GPIO N -> which signal) |

### IO MUX Registers (base `0x3FF49000`)

| Offset | Register | Purpose |
|--------|----------|---------|
| `0x000` | `IO_MUX_PIN_CTRL` | Clock output configuration |
| `0x004`-`0x090` | `IO_MUX_GPIO0_REG` - `IO_MUX_GPIO39_REG` | Per-pin IO MUX config: function select, pull-up/down, drive strength, input enable |

Note: IO MUX register offsets are NOT sequential by GPIO number. The mapping is defined in the TRM and in `io_mux_reg.h`. For example, GPIO0 is at offset 0x44, GPIO1 at 0x88, etc.

### GPIO Sigma-Delta Registers (base `0x3FF44F00`)

| Offset | Register | Purpose |
|--------|----------|---------|
| `0x00-0x1C` | `GPIO_SIGMADELTA0_REG` - `GPIO_SIGMADELTA7_REG` | 8 sigma-delta modulation channels |
| `0x20` | `GPIO_SIGMADELTA_CG_REG` | Sigma-delta clock gate |
| `0x28` | `GPIO_SIGMADELTA_MISC_REG` | Sigma-delta misc config |
| `0xFC` | `GPIO_SIGMADELTA_VERSION_REG` | Version register |

## Source Code References

### ESP-IDF Register Definitions
- [`soc/gpio_reg.h`](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/gpio_reg.h) -- GPIO controller register addresses, bit masks, and field definitions
- [`soc/gpio_struct.h`](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/gpio_struct.h) -- C struct overlay for GPIO registers
- [`soc/io_mux_reg.h`](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/io_mux_reg.h) -- IO MUX register definitions, per-pin offsets, function select macros
- [`soc/gpio_sd_reg.h`](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/gpio_sd_reg.h) -- GPIO sigma-delta modulation registers

### ESP-IDF HAL (Low-Level Driver)
- [`hal/gpio_ll.h`](https://github.com/espressif/esp-idf/blob/master/components/esp_hal_gpio/esp32/include/hal/gpio_ll.h) -- Shows exactly how registers are accessed: pin direction, pull-up/down, output level, interrupt configuration, GPIO Matrix routing

### ESP-IDF API Documentation
- [GPIO API Reference](https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-reference/peripherals/gpio.html)

### ESP-IDF Examples
- [`examples/peripherals/gpio`](https://github.com/espressif/esp-idf/blob/master/examples/peripherals/gpio) -- Basic GPIO input/output and interrupt examples

### Espressif QEMU Implementation
- [`hw/gpio/esp32_gpio.c`](https://github.com/espressif/qemu/blob/esp-develop/hw/gpio/esp32_gpio.c) -- QEMU's GPIO and GPIO Matrix model

QEMU's implementation models the GPIO output registers (OUT, OUT_W1TS, OUT_W1TC), input registers, enable registers, interrupt status registers, per-pin configuration registers (GPIO_PINn), and the GPIO Matrix input/output selection registers. It tracks pin state and can route signals between emulated peripherals through the GPIO Matrix. Limitations include: IO MUX is modeled separately and the interaction between IO MUX function selection and GPIO Matrix routing may not be fully accurate. RTC GPIO functionality, sigma-delta modulation, and drive strength are not modeled. The boot strapping register (`GPIO_STRAP_REG`) is modeled to return configurable values.

## Renode Implementation Analysis

### Existing Renode Model

No dedicated ESP32 GPIO model exists in the main Renode repository. The platform definition may use a generic GPIO port or a minimal stub.

### Recommended Renode Reference Peripherals
- [`STM32_GPIOPort.cs`](https://github.com/renode/renode-infrastructure/blob/master/src/Emulator/Peripherals/Peripherals/GPIOPort/STM32_GPIOPort.cs) -- Good reference for a register-mapped GPIO controller with per-pin configuration, alternate function selection, and output state tracking. STM32's MODER/OTYPER/OSPEEDR/PUPDR model is conceptually similar to ESP32's per-pin GPIO_PINn and IO MUX registers.

Additional reference considerations:
- Renode's `BaseGPIOPort` class provides GPIO infrastructure (pin count, state tracking, interrupt connections)
- Any Renode GPIO model with interrupt support per pin would be relevant

### Implementation Approach

The ESP32 GPIO subsystem should be implemented as two or three cooperating peripherals:

**Component 1: GPIO Controller (`ESP32_GPIO`)**
- Handles GPIO_OUT, GPIO_ENABLE, GPIO_IN, GPIO_STATUS registers
- Implements per-pin GPIO_PINn configuration registers (40 registers)
- Manages GPIO interrupt generation based on pin configuration and level/edge detection
- Implements write-1-to-set and write-1-to-clear register variants

**Component 2: IO MUX (`ESP32_IOMUX`)**
- Handles per-pin function selection (40 pins, each with function select field)
- Manages pull-up, pull-down, drive strength settings
- Routes signals between GPIO Matrix function and direct IO MUX functions

**Component 3 (can be deferred): GPIO Matrix**
- 256 input selection registers (`GPIO_FUNCn_IN_SEL_CFG_REG`)
- 34 output selection registers (`GPIO_FUNCn_OUT_SEL_CFG_REG`)
- Can be part of the GPIO controller or separate

**Registers that MUST be implemented for basic functionality:**
- `GPIO_OUT_REG`, `GPIO_OUT_W1TS_REG`, `GPIO_OUT_W1TC_REG` (and _1 variants)
- `GPIO_ENABLE_REG`, `GPIO_ENABLE_W1TS_REG`, `GPIO_ENABLE_W1TC_REG` (and _1 variants)
- `GPIO_IN_REG`, `GPIO_IN1_REG`
- `GPIO_STRAP_REG` -- boot strapping, read by ROM bootloader (must return correct values)
- `GPIO_PINn_REG` (40 registers) -- per-pin interrupt type and config
- `GPIO_STATUS_REG`, `GPIO_STATUS_W1TC_REG` -- interrupt status and clear
- IO MUX per-pin registers (function select, pull-up/down)

**Registers that can be stubbed initially:**
- GPIO Matrix input/output selection registers (can return default values)
- Sigma-delta modulation registers
- NMI interrupt registers
- GPIO_ACPU/PCPU split (can treat as single interrupt source initially)

**Interrupts that need to work:**
- GPIO interrupt to PRO CPU (most firmware uses this)
- Per-pin interrupt type must be respected (level vs edge)
- Interrupt status clear via W1TC must work

**DMA considerations:** GPIO has no DMA. However, the GPIO Matrix signal routing is used by DMA-capable peripherals (SPI, I2S) to select their pins.

**Estimated complexity:** Complex (large register space, three interacting subsystems, per-pin configuration)

### Key Firmware Interactions

**During boot (ROM bootloader):**
1. ROM reads `GPIO_STRAP_REG` to determine boot mode (GPIO0, GPIO2, GPIO5, GPIO12, GPIO15 levels)
2. ROM configures IO MUX for SPI flash pins (GPIO6-11) to SPI function
3. ROM configures IO MUX for UART0 pins (GPIO1=TX, GPIO3=RX) to UART function
4. GPIO12 (MTDI) strapping determines flash voltage (VDD_SDIO) -- critical for flash access

**During ESP-IDF startup:**
1. `gpio_config()` writes IO MUX registers for function select, pull-up/down, input enable
2. `gpio_set_direction()` writes GPIO_ENABLE registers
3. `gpio_set_level()` writes GPIO_OUT_W1TS/W1TC registers
4. `gpio_isr_register()` and `gpio_install_isr_service()` configure per-pin interrupts
5. Peripheral drivers (UART, SPI, I2C) use `gpio_matrix_in()` and `gpio_matrix_out()` to route signals through the GPIO Matrix

**Critical register accesses that MUST succeed:**
- `GPIO_STRAP_REG` must return correct boot mode values (wrong values = wrong boot path)
- IO MUX function select must not block SPI flash pin configuration (boot hangs)
- GPIO_OUT writes should not fault (LED blinking, status indicators are common early firmware operations)

## Complexity Assessment

- **Estimated difficulty:** Complex
- **Estimated register count:** ~350+ registers (40 GPIO_PINn + 256 input sel + 34 output sel + 40 IO MUX + control/status registers)
- **Dependencies:** Interrupt matrix (for routing GPIO interrupts to CPU), RTC subsystem (for RTC GPIO in deep sleep modes, can be deferred)
- **Priority:** Critical for boot -- ROM bootloader reads `GPIO_STRAP_REG` to determine boot mode, configures IO MUX for flash SPI and UART0 pins. GPIO must at minimum return correct strapping values and accept IO MUX configuration writes without error.
