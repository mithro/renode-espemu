# ESP32 Interrupt Matrix

## Overview

The ESP32 Interrupt Matrix is a hardware routing fabric that connects peripheral interrupt sources to CPU interrupt inputs. Unlike standard Xtensa interrupt handling where peripherals are hardwired to specific CPU interrupt lines, the ESP32 uses a fully programmable routing matrix that allows any of the 71 peripheral interrupt sources to be mapped to any of the 26 available CPU interrupt inputs, independently for each of the two CPU cores (PRO_CPU and APP_CPU).

This design provides exceptional flexibility but also creates a complex routing layer that must be accurately emulated. The interrupt matrix is CRITICAL for boot and operation -- without it, no peripheral interrupts reach the CPU, which means no FreeRTOS tick, no UART interrupts, no timer alarms, and effectively no functioning system.

The routing is configured through the DPORT (Dual-Port) register space, with separate mapping registers for each CPU core. Each peripheral interrupt source has a dedicated 5-bit register that selects which CPU interrupt line (0-31) it should be routed to. Multiple peripheral sources can be mapped to the same CPU interrupt line (shared/multiplexed interrupts).

## Hardware Specifications

### Interrupt Matrix Architecture

| Feature                      | Specification                                             |
| ---------------------------- | --------------------------------------------------------- |
| Peripheral Interrupt Sources | 71 (numbered 0-70)                                        |
| CPU Interrupt Inputs         | 32 per core (0-31), 26 usable for peripherals             |
| CPU Cores                    | 2 (PRO_CPU = CPU0, APP_CPU = CPU1)                        |
| Routing Granularity          | Per-source, per-core                                      |
| Routing Register Width       | 5 bits per source (selects CPU interrupt 0-31)            |
| Shared Interrupts            | Yes, multiple sources can map to same CPU interrupt       |
| Default Mapping              | All sources mapped to CPU interrupt 0 (disabled) at reset |

### CPU Interrupt Line Allocation

The Xtensa LX6 CPU has 32 interrupt inputs (levels 1-6 + NMI), but not all are available for peripheral routing:

| CPU Interrupt | Level | Type     | Usage                                       |
| ------------- | ----- | -------- | ------------------------------------------- |
| 0             | 1     | Level    | Used as "disabled" target (default mapping) |
| 1             | 1     | Level    | Available for peripherals                   |
| 2             | 1     | Level    | Available for peripherals                   |
| 3             | 1     | Level    | Available for peripherals                   |
| 4             | 1     | Level    | Available for peripherals                   |
| 5             | 1     | Level    | Available for peripherals                   |
| 6             | 1     | Edge     | Timer interrupt (debug)                     |
| 7             | 1     | Edge     | Software / profiling                        |
| 8             | 1     | Level    | Available for peripherals                   |
| 9             | 1     | Level    | Available for peripherals                   |
| 10            | 1     | Edge     | Available for peripherals                   |
| 11            | 3     | Level    | Profiling                                   |
| 12            | 1     | Level    | Available for peripherals                   |
| 13            | 1     | Level    | Available for peripherals                   |
| 14            | 7     | NMI      | NMI (used for panic watchdog)               |
| 15            | 3     | Edge     | Timer (internal)                            |
| 16            | 5     | Edge     | Timer (internal)                            |
| 17            | 1     | Level    | Available for peripherals                   |
| 18            | 1     | Level    | Available for peripherals                   |
| 19            | 2     | Level    | Available for peripherals                   |
| 20            | 2     | Level    | Available for peripherals                   |
| 21            | 2     | Level    | Available for peripherals                   |
| 22            | 3     | Edge     | Available for peripherals                   |
| 23            | 3     | Level    | Available for peripherals                   |
| 24            | 4     | Level    | Available for peripherals                   |
| 25            | 4     | Level    | Available for peripherals                   |
| 26            | 5     | Level    | Available for peripherals                   |
| 27            | 3     | Level    | Available for peripherals                   |
| 28            | 4     | Edge     | Available for peripherals                   |
| 29            | 3     | Software | Software interrupt                          |
| 30            | 4     | Edge     | Reserved                                    |
| 31            | 5     | Edge     | Timer (internal)                            |

### Peripheral Interrupt Source List (Selected Critical Sources)

| Source Number | Peripheral Source   | Description                            |
| ------------- | ------------------- | -------------------------------------- |
| 0             | MAC_INTR            | WiFi MAC interrupt                     |
| 6             | TG0_T0_LEVEL        | Timer Group 0, Timer 0 (FreeRTOS tick) |
| 7             | TG0_T1_LEVEL        | Timer Group 0, Timer 1                 |
| 8             | TG0_WDT_LEVEL       | Timer Group 0, Watchdog                |
| 9             | TG0_LACT_LEVEL      | Timer Group 0, LACT                    |
| 10            | TG1_T0_LEVEL        | Timer Group 1, Timer 0                 |
| 11            | TG1_T1_LEVEL        | Timer Group 1, Timer 1                 |
| 12            | TG1_WDT_LEVEL       | Timer Group 1, Watchdog                |
| 13            | TG1_LACT_LEVEL      | Timer Group 1, LACT                    |
| 14            | GPIO_INTR           | GPIO interrupt                         |
| 17            | UART0_INTR          | UART0 interrupt                        |
| 18            | UART1_INTR          | UART1 interrupt                        |
| 19            | UART2_INTR          | UART2 interrupt                        |
| 24            | I2C_EXT0_INTR       | I2C 0 interrupt                        |
| 25            | I2C_EXT1_INTR       | I2C 1 interrupt                        |
| 28            | SPI1_INTR           | SPI1 interrupt                         |
| 29            | SPI2_INTR           | SPI2 interrupt                         |
| 30            | SPI3_INTR           | SPI3 interrupt                         |
| 35            | TIMER1_INTR         | FRC Timer 1                            |
| 36            | TIMER2_INTR         | FRC Timer 2                            |
| 46            | RTC_CORE_INTR       | RTC interrupt                          |
| 50            | CPU_INTR_FROM_CPU_0 | Cross-core interrupt 0                 |
| 51            | CPU_INTR_FROM_CPU_1 | Cross-core interrupt 1                 |
| 52            | CPU_INTR_FROM_CPU_2 | Cross-core interrupt 2                 |
| 53            | CPU_INTR_FROM_CPU_3 | Cross-core interrupt 3                 |

### Cross-Core Interrupts

The ESP32 dual-core design requires a mechanism for one CPU to interrupt the other. This is handled through 4 cross-core interrupt sources (CPU_INTR_FROM_CPU_0 through CPU_INTR_FROM_CPU_3):

| Register                      | Address    | Description                                   |
| ----------------------------- | ---------- | --------------------------------------------- |
| DPORT_CPU_INTR_FROM_CPU_0_REG | 0x3FF000DC | Write bit 0 to trigger cross-core interrupt 0 |
| DPORT_CPU_INTR_FROM_CPU_1_REG | 0x3FF000E0 | Write bit 0 to trigger cross-core interrupt 1 |
| DPORT_CPU_INTR_FROM_CPU_2_REG | 0x3FF000E4 | Write bit 0 to trigger cross-core interrupt 2 |
| DPORT_CPU_INTR_FROM_CPU_3_REG | 0x3FF000E8 | Write bit 0 to trigger cross-core interrupt 3 |

FreeRTOS uses these for:
- **CPU_INTR_FROM_CPU_0**: Yield notification (PRO_CPU to APP_CPU or vice versa)
- **CPU_INTR_FROM_CPU_1**: Yield notification (other direction)
- **CPU_INTR_FROM_CPU_2**: Used by `esp_ipc` for cross-core function calls
- **CPU_INTR_FROM_CPU_3**: Reserved

## TRM Chapter Reference

**ESP32 Technical Reference Manual, Chapter 7: Interrupt Matrix**

- [ESP32 TRM PDF](https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf)

### Key Sections

| Section | Topic                                                  |
| ------- | ------------------------------------------------------ |
| 7.1     | Introduction to interrupt matrix                       |
| 7.2     | Peripheral interrupt sources (full list of 71 sources) |
| 7.3     | CPU interrupt types and levels                         |
| 7.4     | Interrupt configuration registers                      |
| 7.5     | NMI interrupt mask                                     |

### Critical TRM Details

1. **Default State**: After reset, all peripheral sources are mapped to CPU interrupt 0 on both cores. CPU interrupt 0 is level 1 and is typically not enabled, so all peripheral interrupts are effectively disabled.

2. **No Priority Within a CPU Interrupt**: When multiple peripheral sources share a CPU interrupt line, the interrupt handler must check all mapped sources' status registers to determine which fired. There is no hardware prioritization among shared sources.

3. **CPU Interrupt Level**: The CPU interrupt level determines preemption priority. Higher-level interrupts can preempt lower-level interrupt handlers. NMI (level 7, interrupt 14) is the highest.

4. **Edge vs Level**: Edge-triggered CPU interrupts need different handling than level-triggered. Level interrupts must be cleared at the source; edge interrupts are auto-cleared by the CPU on acknowledgment.

## Register Map Summary

### Interrupt Routing Registers (DPORT space)

The routing registers are in the DPORT address space (base 0x3FF00000). Each peripheral source has two 5-bit registers, one per CPU core.

#### PRO_CPU Mapping Registers (selected)

| Register                              | Offset | Source                                   |
| ------------------------------------- | ------ | ---------------------------------------- |
| DPORT_PRO_MAC_INTR_MAP_REG            | 0x0104 | WiFi MAC -> PRO_CPU interrupt number     |
| DPORT_PRO_TG_T0_LEVEL_INT_MAP_REG     | 0x011C | TG0_T0 -> PRO_CPU interrupt number       |
| DPORT_PRO_TG_T1_LEVEL_INT_MAP_REG     | 0x0120 | TG0_T1 -> PRO_CPU interrupt number       |
| DPORT_PRO_TG_WDT_LEVEL_INT_MAP_REG    | 0x0124 | TG0_WDT -> PRO_CPU interrupt number      |
| DPORT_PRO_TG1_T0_LEVEL_INT_MAP_REG    | 0x012C | TG1_T0 -> PRO_CPU interrupt number       |
| DPORT_PRO_TG1_T1_LEVEL_INT_MAP_REG    | 0x0130 | TG1_T1 -> PRO_CPU interrupt number       |
| DPORT_PRO_TG1_WDT_LEVEL_INT_MAP_REG   | 0x0134 | TG1_WDT -> PRO_CPU interrupt number      |
| DPORT_PRO_GPIO_INTERRUPT_MAP_REG      | 0x0138 | GPIO -> PRO_CPU interrupt number         |
| DPORT_PRO_UART_INTR_MAP_REG           | 0x0148 | UART0 -> PRO_CPU interrupt number        |
| DPORT_PRO_UART1_INTR_MAP_REG          | 0x014C | UART1 -> PRO_CPU interrupt number        |
| DPORT_PRO_CPU_INTR_FROM_CPU_0_MAP_REG | 0x01C8 | Cross-core 0 -> PRO_CPU interrupt number |
| DPORT_PRO_CPU_INTR_FROM_CPU_1_MAP_REG | 0x01CC | Cross-core 1 -> PRO_CPU interrupt number |
| DPORT_PRO_CPU_INTR_FROM_CPU_2_MAP_REG | 0x01D0 | Cross-core 2 -> PRO_CPU interrupt number |
| DPORT_PRO_CPU_INTR_FROM_CPU_3_MAP_REG | 0x01D4 | Cross-core 3 -> PRO_CPU interrupt number |

#### APP_CPU Mapping Registers

The APP_CPU mapping registers follow the same layout, starting at a different offset range. Each PRO_CPU register has an APP_CPU counterpart:

| Register Pattern    | Description                                    |
| ------------------- | ---------------------------------------------- |
| DPORT_APP_*_MAP_REG | Same sources, mapped independently for APP_CPU |

The APP_CPU mapping register block starts at offset 0x0204 and mirrors the PRO_CPU layout.

### Interrupt Status Registers

| Register                    | Offset | Description                                |
| --------------------------- | ------ | ------------------------------------------ |
| DPORT_PRO_INTR_STATUS_0_REG | 0x003C | PRO_CPU interrupt source status bits 0-31  |
| DPORT_PRO_INTR_STATUS_1_REG | 0x0040 | PRO_CPU interrupt source status bits 32-63 |
| DPORT_PRO_INTR_STATUS_2_REG | 0x0044 | PRO_CPU interrupt source status bits 64-70 |
| DPORT_APP_INTR_STATUS_0_REG | 0x0048 | APP_CPU interrupt source status bits 0-31  |
| DPORT_APP_INTR_STATUS_1_REG | 0x004C | APP_CPU interrupt source status bits 32-63 |
| DPORT_APP_INTR_STATUS_2_REG | 0x0050 | APP_CPU interrupt source status bits 64-70 |

These read-only registers provide the raw status of each peripheral interrupt source, useful for determining which source triggered a shared CPU interrupt.

## Source Code References

### SOC Definitions

- **Interrupt Source Numbers**: [components/soc/esp32/include/soc/interrupts.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/include/soc/interrupts.h)
  - Defines the enumeration of all 71 peripheral interrupt sources
  - Maps source names to numbers (e.g., `ETS_TG0_T0_LEVEL_INTR_SOURCE = 6`)
  - Critical reference for mapping between peripheral names and source numbers

- **DPORT Registers (Interrupt Routing)**: [components/soc/esp32/register/soc/dport_reg.h](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/dport_reg.h)
  - Defines all DPORT register addresses including interrupt mapping registers
  - Contains `DPORT_PRO_*_MAP_REG` and `DPORT_APP_*_MAP_REG` definitions
  - Also defines cross-core interrupt trigger registers
  - Includes interrupt status registers for both cores

### QEMU Implementation

- **Interrupt Controller**: [hw/xtensa/esp32_intc.c](https://github.com/espressif/qemu/blob/esp-develop/hw/xtensa/esp32_intc.c)
  - Core interrupt matrix emulation
  - Key implementation details:
    - Maintains a 71-entry routing table per CPU core
    - Each entry is a 5-bit value selecting the target CPU interrupt
    - When a peripheral asserts its interrupt source, looks up the routing table to determine which CPU interrupt to assert
    - Handles level and edge interrupt types differently
    - Supports dynamic re-routing (register writes update routing immediately)
    - Manages interrupt status register reads

- **Cross-Core Interrupts**: [hw/misc/esp32_crosscore_int.c](https://github.com/espressif/qemu/blob/esp-develop/hw/misc/esp32_crosscore_int.c)
  - Dedicated model for the 4 cross-core interrupt sources
  - Writing bit 0 to the trigger register asserts the corresponding interrupt source in the interrupt matrix
  - Reading the register returns the current state
  - Writing 0 clears the interrupt
  - Used by FreeRTOS for inter-core communication

### HAL / Driver Layer

ESP-IDF's interrupt allocation is managed by the `esp_intr_alloc` system, which abstracts the interrupt matrix. Key files:

- `components/esp_hw_support/intr_alloc.c` -- interrupt allocator
- `components/esp_hw_support/include/esp_intr_alloc.h` -- public API

These are driver-level and don't directly inform hardware emulation, but understanding the allocation strategy helps predict which CPU interrupt lines firmware will use.

## Renode Implementation Analysis

### Existing Renode Models (Reference)

Renode does not have a direct ESP32 interrupt matrix model. The Xtensa CPU model in Renode handles standard Xtensa interrupt inputs, but the ESP32's custom routing matrix is a peripheral that must be explicitly implemented.

General interrupt controller references in Renode:
- Most Renode platforms use direct GPIO connections from peripherals to CPU interrupt inputs
- The ESP32 interrupt matrix adds a programmable routing layer between these

### Recommended Implementation Approach

#### Architecture

```
ESP32_InterruptMatrix : IDoubleWordPeripheral, IKnownSize, IIRQController
├── RoutingTable[2][71]  -- per-core, per-source -> CPU interrupt mapping
├── SourceState[71]      -- current state of each peripheral interrupt source
├── CPU IRQ connections   -- GPIO outputs to each CPU's interrupt inputs
└── CrossCoreInterrupt[4] -- cross-core interrupt trigger state
```

#### Core Implementation

1. **Routing Table**:
   - 2 arrays of 71 entries (one per core), each entry is a 5-bit CPU interrupt number
   - Initialize all entries to 0 (all sources routed to CPU interrupt 0 = disabled)
   - On write to mapping register: update the corresponding entry and recalculate CPU interrupt state

2. **Interrupt Routing Logic**:
   ```
   For each CPU core:
     For each CPU interrupt line (0-31):
       cpu_int_state[line] = OR of all source_state[s]
                             where routing_table[core][s] == line
   ```
   - When any source state changes or any routing register changes, recalculate the affected CPU interrupt lines
   - Assert/deassert CPU interrupt GPIO connections accordingly

3. **Source State Management**:
   - Each peripheral connects to the interrupt matrix as an IRQ source
   - When a peripheral asserts/deasserts its interrupt, the matrix updates `source_state[source_num]`
   - Then recalculates which CPU interrupts should be asserted

4. **Status Registers**:
   - `INTR_STATUS_0/1/2` registers return the raw `source_state` bits packed into 32-bit words
   - Read-only, reflect real-time peripheral interrupt state

5. **Cross-Core Interrupts**:
   - 4 registers that, when written with bit 0 = 1, assert the corresponding interrupt source
   - These sources (50-53) route through the same matrix as other peripherals
   - Writing 0 to the register clears the interrupt source

#### Integration with DPORT

The interrupt matrix registers reside in the DPORT register space. The DPORT peripheral is a large multi-function block. Implementation options:

- **Option A**: Implement interrupt matrix as part of a larger DPORT peripheral model
- **Option B**: Implement interrupt matrix as a standalone peripheral mapped to the DPORT address range, with the DPORT model forwarding relevant register accesses

Option B is recommended for modularity, with the DPORT peripheral dispatching reads/writes in the interrupt mapping range to the interrupt matrix model.

#### Key Firmware Interactions

1. **Interrupt Allocation** (during peripheral driver init):
   ```
   1. esp_intr_alloc() finds a free CPU interrupt line at the requested level
   2. Writes the selected CPU interrupt number to the source's mapping register
   3. Enables the CPU interrupt in the Xtensa INTENABLE special register
   4. Enables the interrupt at the peripheral level
   ```

2. **FreeRTOS Tick Setup**:
   ```
   1. Configures TG0_T0 alarm (see timer-group.md)
   2. Allocates interrupt: maps TG0_T0_LEVEL (source 6) to a CPU interrupt
   3. On timer alarm: source 6 asserts -> matrix routes to CPU interrupt -> ISR fires
   4. ISR clears timer interrupt, re-enables alarm
   ```

3. **Cross-Core Yield** (FreeRTOS SMP):
   ```
   1. CPU0 needs CPU1 to yield
   2. CPU0 writes 1 to DPORT_CPU_INTR_FROM_CPU_0_REG
   3. Source 50 asserts in matrix
   4. Matrix routes source 50 to CPU1's mapped interrupt line
   5. CPU1 handles interrupt, performs context switch
   6. ISR writes 0 to clear the cross-core interrupt
   ```

4. **Interrupt Handler Dispatch** (shared interrupts):
   ```
   1. CPU interrupt fires
   2. Handler reads INTR_STATUS registers to determine which sources are active
   3. Calls registered handlers for active sources
   4. Sources are cleared at the peripheral level (not in the matrix)
   ```

### Implementation Priority

| Component                                 | Priority | Reason                                      |
| ----------------------------------------- | -------- | ------------------------------------------- |
| Routing table (per-core, per-source)      | CRITICAL | All interrupts depend on routing            |
| Mapping register writes                   | CRITICAL | Firmware configures routing at boot         |
| Source state tracking + CPU IRQ assertion | CRITICAL | Interrupts must reach CPUs                  |
| Status registers (read-only)              | HIGH     | Shared interrupt dispatch reads these       |
| Cross-core interrupt triggers             | HIGH     | FreeRTOS SMP requires this                  |
| Dynamic re-routing                        | MEDIUM   | Uncommon after boot, but firmware can do it |
| NMI mask                                  | LOW      | Rarely used                                 |

## Complexity Assessment

### Overall Complexity: HIGH

#### Justification

| Factor                 | Rating      | Notes                                                                 |
| ---------------------- | ----------- | --------------------------------------------------------------------- |
| Register complexity    | High        | 71 sources x 2 cores = 142 mapping registers, plus status registers   |
| Behavioral complexity  | Medium-High | Routing logic is conceptually simple but must handle all edge cases   |
| Timing sensitivity     | Medium      | Interrupt latency matters but routing is combinational (near-instant) |
| Boot criticality       | CRITICAL    | Without interrupt routing, no peripheral interrupts work at all       |
| Integration complexity | High        | Must interface with all peripheral models and both CPU cores          |
| QEMU reference quality | High        | Complete implementation available                                     |
| Dual-core complexity   | High        | Independent routing per core, cross-core interrupts add complexity    |

#### Estimated Effort

- **Single-core minimal (PRO_CPU only, critical sources)**: 3-4 days
  - Implement routing table for PRO_CPU
  - Support timer, UART, GPIO interrupt sources
  - No cross-core interrupts

- **Full dual-core implementation**: 5-7 days
  - Both cores with independent routing
  - All 71 interrupt sources
  - Cross-core interrupt support
  - Status registers
  - Integration with DPORT peripheral model

#### Key Risks

1. **Performance**: Recalculating CPU interrupt state on every source change or routing change could be expensive with 71 sources. Optimization may be needed (e.g., maintain reverse mapping from CPU interrupt to sources).

2. **Xtensa CPU Model Integration**: Renode's Xtensa CPU model must support asserting/deasserting specific interrupt lines. Verify this capability exists before implementation.

3. **Level vs Edge Semantics**: Level interrupts require the source to be cleared before the CPU interrupt deasserts. Edge interrupts auto-clear. Mixing these up causes interrupt storms or missed interrupts.

4. **DPORT Access Complexity**: The ESP32 DPORT has known hardware errata requiring special read sequences on multi-core systems. The emulation may need to account for firmware using workaround read patterns (e.g., `DPORT_READ_PERI_REG` macro in ESP-IDF).

5. **Interrupt Source Numbering**: Careful mapping between source numbers in `interrupts.h` and the register offset in DPORT is essential. Off-by-one errors will route interrupts to the wrong CPU line.

6. **Shared Interrupt Handling**: When multiple sources share a CPU interrupt, the matrix must correctly OR all source states. If any active source is not properly tracked, the shared interrupt may incorrectly deassert while other sources are still active.
