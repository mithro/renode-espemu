# ESP32 UART

## Overview

The ESP32 integrates three UART controllers (UART0, UART1, UART2) that provide asynchronous serial communication with programmable baud rates, data formats, and flow control. UART0 is the default console output used by the ROM bootloader and ESP-IDF logging framework, making it the single most critical peripheral for early bring-up of any emulation platform. Without a functional UART0, there is no way to observe boot progress or firmware output.

Each UART controller features independent 128-byte transmit and receive FIFOs, hardware flow control (RTS/CTS), software flow control (XON/XOFF), RS-485 support, IrDA support, and auto-baud detection. The controllers support DMA transfers via the UHCI (Universal Host Controller Interface) peripheral for high-throughput scenarios. Baud rates are configurable up to 5 Mbps using a fractional divider driven from the APB clock (80 MHz).

The UART controllers are among the most heavily exercised peripherals in any ESP32 system. The first-stage bootloader, second-stage bootloader, and application firmware all use UART0 for logging. ESP-IDF's `esp_log` system, the `printf` family, and the REPL/console component all route through UART0 by default. UART1 and UART2 are typically used for application-level serial communication (GPS modules, Modbus, sensor buses, etc.).

## Hardware Specifications

- **Register base addresses:**
  - UART0: `0x3FF40000` (size: `0x1000`)
  - UART1: `0x3FF50000` (size: `0x1000`)
  - UART2: `0x3FF6E000` (size: `0x1000`)
- **Number of instances:** 3 (UART0, UART1, UART2)
- **FIFO depth:** 128 bytes TX, 128 bytes RX per controller
- **Baud rate:** Up to 5 Mbps with fractional divider (20-bit integer + 4-bit fractional)
- **Data formats:** 5/6/7/8 data bits, 1/1.5/2 stop bits, even/odd/none parity
- **Flow control:** Hardware RTS/CTS, software XON/XOFF
- **Interrupt sources:** 20 individual interrupt sources per controller (TX FIFO empty, RX FIFO full, parity error, framing error, RX timeout, TX done, break detection, CTS change, etc.)
- **DMA support:** Via UHCI0/UHCI1 controllers for linked-list DMA transfers
- **Special modes:** RS-485 (with collision detection), IrDA (SIR encoder/decoder), auto-baud rate detection, AT command detection, wake-up from light sleep

## TRM Chapter Reference

- **ESP32 Technical Reference Manual** Chapter 13: UART Controllers
  - [PDF link](https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf)

## Register Map Summary

Key registers that an emulator must model (offsets from each UART base address):

| Offset | Register | Purpose |
|--------|----------|---------|
| `0x000` | `UART_FIFO_REG` | Read/write FIFO data (byte at a time) |
| `0x004` | `UART_INT_RAW_REG` | Raw interrupt status (read-only) |
| `0x008` | `UART_INT_ST_REG` | Masked interrupt status (INT_RAW & INT_ENA) |
| `0x00C` | `UART_INT_ENA_REG` | Interrupt enable bits |
| `0x010` | `UART_INT_CLR_REG` | Write-1-to-clear interrupt bits |
| `0x014` | `UART_CLKDIV_REG` | Clock divider (baud rate configuration) |
| `0x018` | `UART_AUTOBAUD_REG` | Auto-baud rate detection control |
| `0x01C` | `UART_STATUS_REG` | TX/RX FIFO counts, RTS/CTS/DTR/DSR line states |
| `0x020` | `UART_CONF0_REG` | Core config: parity, stop bits, data length, TX/RX reset, flow control |
| `0x024` | `UART_CONF1_REG` | FIFO thresholds: RX full, TX empty, RX timeout |
| `0x028` | `UART_LOWPULSE_REG` | Auto-baud: minimum low pulse duration |
| `0x02C` | `UART_HIGHPULSE_REG` | Auto-baud: minimum high pulse duration |
| `0x030` | `UART_RXD_CNT_REG` | Count of RX edge transitions (auto-baud) |
| `0x034` | `UART_FLOW_CONF_REG` | Software flow control config (XON/XOFF) |
| `0x038` | `UART_SLEEP_CONF_REG` | Sleep mode wakeup threshold |
| `0x03C` | `UART_SWFC_CONF_REG` | Software flow control character definitions |
| `0x040` | `UART_IDLE_CONF_REG` | TX/RX idle thresholds |
| `0x044` | `UART_RS485_CONF_REG` | RS-485 mode configuration |
| `0x048` | `UART_AT_CMD_PRECNT_REG` | AT command detection: pre-idle time |
| `0x04C` | `UART_AT_CMD_POSTCNT_REG` | AT command detection: post-idle time |
| `0x050` | `UART_AT_CMD_GAPTOUT_REG` | AT command detection: gap timeout |
| `0x054` | `UART_AT_CMD_CHAR_REG` | AT command detection: character and count |
| `0x058` | `UART_MEM_CONF_REG` | TX/RX memory allocation (in 128-byte units) |
| `0x05C` | `UART_MEM_TX_STATUS_REG` | TX FIFO read/write pointer status |
| `0x060` | `UART_MEM_RX_STATUS_REG` | RX FIFO read/write pointer status |
| `0x064` | `UART_MEM_CNT_STATUS_REG` | TX/RX FIFO byte count |
| `0x068` | `UART_POSPULSE_REG` | Auto-baud: positive pulse minimum duration |
| `0x06C` | `UART_NEGPULSE_REG` | Auto-baud: negative pulse minimum duration |
| `0x078` | `UART_DATE_REG` | Version/date register |
| `0x07C` | `UART_ID_REG` | UART ID register |

## Source Code References

### ESP-IDF Register Definitions
- [`soc/uart_reg.h`](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/uart_reg.h) -- Register addresses, bit field masks, and shift values for all UART registers
- [`soc/uart_struct.h`](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/uart_struct.h) -- C struct overlay for memory-mapped register access

### ESP-IDF HAL (Low-Level Driver)
- [`hal/uart_ll.h`](https://github.com/espressif/esp-idf/blob/master/components/esp_hal_uart/esp32/include/hal/uart_ll.h) -- Shows exactly how registers are accessed: baud rate calculation, FIFO reads/writes, interrupt management, status checks

### ESP-IDF API Documentation
- [UART API Reference](https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-reference/peripherals/uart.html)

### ESP-IDF Examples
- [`examples/peripherals/uart`](https://github.com/espressif/esp-idf/blob/master/examples/peripherals/uart) -- Echo, selection, events, and RS-485 examples

### Espressif QEMU Implementation
- [`hw/char/esp32_uart.c`](https://github.com/espressif/qemu/blob/esp-develop/hw/char/esp32_uart.c) -- QEMU's register-level UART model

QEMU's implementation covers the core UART functionality: FIFO read/write, baud rate register storage, basic interrupt generation (TX done, RX FIFO threshold), and STATUS register reporting. It models the FIFO as a circular buffer and connects to QEMU's CharBackend for host I/O. Limitations include: no RS-485 emulation, no IrDA, no auto-baud detection, no UHCI/DMA integration, and simplified flow control. The QEMU model is sufficient for console I/O and basic serial communication but does not model timing or FIFO threshold interrupts with full accuracy.

## Renode Implementation Analysis

### Existing Renode Model

Renode already has an ESP32 UART model:
- [`ESP32_UART.cs`](https://github.com/renode/renode-infrastructure/blob/master/src/Emulator/Peripherals/Peripherals/UART/ESP32_UART.cs)

This model implements the `IUART` interface and handles basic TX/RX FIFO operations, baud rate configuration, and interrupt generation. It is functional enough for console output during boot. Areas for potential improvement include:
- Full interrupt source coverage (all 20 sources)
- Accurate FIFO threshold behavior
- Hardware flow control state tracking
- RS-485 mode support
- AT command detection
- Auto-baud detection registers

### Recommended Renode Reference Peripherals

The existing ESP32_UART.cs is itself the primary reference. For understanding Renode UART patterns more broadly:

- The Renode `UARTBase` class (which ESP32_UART inherits from) provides the standard FIFO and character I/O infrastructure
- Other SoC-specific UART models in Renode (STM32, nRF52, etc.) demonstrate how to handle SoC-specific features like DMA triggers and multiple interrupt sources

### Implementation Approach

Since ESP32_UART.cs already exists, the approach is enhancement rather than creation:

**Registers that MUST work for basic functionality (already implemented):**
- `UART_FIFO_REG` -- byte read/write for TX and RX
- `UART_CONF0_REG` -- data format configuration, FIFO reset bits
- `UART_CONF1_REG` -- FIFO thresholds (RX full, TX empty)
- `UART_CLKDIV_REG` -- baud rate (firmware reads this back to verify)
- `UART_STATUS_REG` -- TX/RX FIFO counts, must reflect actual FIFO state
- `UART_INT_RAW_REG`, `UART_INT_ST_REG`, `UART_INT_ENA_REG`, `UART_INT_CLR_REG` -- full interrupt lifecycle

**Registers that can be stubbed initially:**
- `UART_AUTOBAUD_REG`, `UART_LOWPULSE_REG`, `UART_HIGHPULSE_REG` -- auto-baud detection
- `UART_RS485_CONF_REG` -- RS-485 mode
- `UART_AT_CMD_*` registers -- AT command detection
- `UART_SWFC_CONF_REG`, `UART_FLOW_CONF_REG` -- software flow control
- `UART_SLEEP_CONF_REG` -- sleep wakeup

**Interrupts that need to work:**
- `UART_TXFIFO_EMPTY_INT` -- triggered when TX FIFO count drops below threshold
- `UART_RXFIFO_FULL_INT` -- triggered when RX FIFO count exceeds threshold
- `UART_RXFIFO_TOUT_INT` -- RX timeout (data in FIFO but below threshold for a while)
- `UART_TX_DONE_INT` -- all data transmitted
- `UART_BRK_DET_INT`, `UART_FRM_ERR_INT`, `UART_PARITY_ERR_INT` -- error conditions

**DMA considerations:**
- UHCI DMA is a separate peripheral; UART DMA support can be deferred
- Most firmware uses FIFO mode (interrupt-driven) for UART, not DMA

**Estimated complexity:** Medium (model exists, enhancements are incremental)

### Key Firmware Interactions

**During boot (ROM bootloader):**
1. ROM code configures UART0 baud rate to 115200 (writes `UART_CLKDIV_REG`)
2. ROM code resets FIFOs (sets then clears `TXFIFO_RST` and `RXFIFO_RST` in `CONF0`)
3. ROM outputs boot messages by writing bytes to `UART_FIFO_REG`
4. ROM checks `UART_STATUS_REG` for TX FIFO count before writing (busy-wait)

**During second-stage bootloader:**
1. Bootloader re-configures baud rate (may change to different speed)
2. Outputs extensive boot log via `UART_FIFO_REG`
3. May enable interrupts for download mode (UART boot)

**During application startup (ESP-IDF):**
1. `uart_driver_install()` configures interrupts: enables `RXFIFO_FULL`, `RXFIFO_TOUT`, `TXFIFO_EMPTY`
2. Sets FIFO thresholds in `UART_CONF1_REG` (typically RX full = 120, TX empty = 10)
3. Clears all pending interrupts via `UART_INT_CLR_REG`
4. All `ESP_LOG*` output goes through UART0 TX FIFO writes
5. Console/REPL input reads from UART0 RX FIFO via interrupts

**Critical register accesses that MUST succeed:**
- Writing to `UART_FIFO_REG` must produce output (console visibility)
- Reading `UART_STATUS_REG` must report correct FIFO counts (firmware busy-waits on this)
- Interrupt clear/enable/status cycle must work correctly (firmware hangs if interrupts are stuck)

## Complexity Assessment

- **Estimated difficulty:** Medium (existing model needs enhancement)
- **Estimated register count:** ~30 registers to model
- **Dependencies:** Interrupt matrix (for routing UART interrupts to CPU), UHCI (for DMA, can be deferred)
- **Priority:** Critical for boot -- UART0 is the primary output for ROM bootloader, second-stage bootloader, and all ESP-IDF logging. Without functional UART, emulation is blind.
