# ESP32 I2C

## Overview

The ESP32 contains two I2C controllers: I2C0 and I2C1. Both controllers can operate in either master or slave mode and support the standard I2C protocol with 7-bit addressing. The ESP32 I2C implementation uses a unique command-based architecture where transfers are programmed as a sequence of hardware commands (START, WRITE, READ, STOP, END) loaded into a command register queue. The hardware then executes these commands sequentially, generating the appropriate I2C bus signals.

This command-based approach differs significantly from simpler I2C controllers (which typically just have address/data registers and start/stop bits) and is an important consideration for emulation -- the command queue execution model must be faithfully reproduced for the ESP-IDF I2C driver to function correctly.

## Hardware Specifications

| Feature | Value |
|---|---|
| **Number of instances** | 2 (I2C0, I2C1) |
| **Modes** | Master and Slave |
| **Speed modes** | Standard (100 kHz), Fast (400 kHz) |
| **Address width** | 7-bit |
| **Base address (I2C0)** | 0x3FF53000 |
| **Base address (I2C1)** | 0x3FF67000 |
| **TX FIFO depth** | 32 bytes |
| **RX FIFO depth** | 32 bytes |
| **Command queue depth** | 16 commands (COMD0-COMD15) |
| **SCL filter** | Configurable digital filter (0-7 APB clock cycles) |
| **SDA filter** | Configurable digital filter (0-7 APB clock cycles) |
| **Interrupts** | 13 interrupt sources per controller |
| **Clock source** | APB clock (80 MHz) |

### Command Types
The I2C controller executes transfers via a hardware command queue. Each command register encodes:
- **Opcode** (3 bits): RSTART (0), WRITE (1), READ (2), STOP (3), END (4)
- **Byte count** (8 bits): Number of bytes for WRITE/READ commands
- **ACK check enable** (1 bit): Whether to verify ACK on WRITE
- **ACK expected value** (1 bit): Expected ACK/NACK value
- **ACK value** (1 bit): ACK value to send on READ

## TRM Chapter Reference

**ESP32 Technical Reference Manual, Chapter 11: I2C Controller**

- https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf

Key sections:
- 11.1: Overview of I2C controller features
- 11.2: I2C architecture and command-based operation model
- 11.3: Master mode operation and command sequences
- 11.4: Slave mode operation
- 11.5: Clock stretching and SCL/SDA timing configuration
- 11.6: Register descriptions

## Register Map Summary

The I2C register space is approximately 0x100 bytes per controller. Key registers include:

### Configuration
| Register | Offset | Description |
|---|---|---|
| I2C_SCL_LOW_PERIOD_REG | 0x00 | SCL low period (clock divider low half) |
| I2C_CTR_REG | 0x04 | Control register: master/slave mode, SCL force output, SDA force output, sample/setup time config, clock enable |
| I2C_SR_REG | 0x08 | Status register: bus state, master state machine, TX/RX FIFO count |
| I2C_TO_REG | 0x0C | Timeout register: SCL timeout threshold |
| I2C_SLAVE_ADDR_REG | 0x10 | Slave address (7-bit) and 10-bit addressing mode select |
| I2C_SCL_HIGH_PERIOD_REG | 0x38 | SCL high period (clock divider high half) |
| I2C_SDA_HOLD_REG | 0x30 | SDA hold time after SCL negative edge |
| I2C_SDA_SAMPLE_REG | 0x34 | SDA sample time after SCL positive edge |

### FIFO
| Register | Offset | Description |
|---|---|---|
| I2C_FIFO_CONF_REG | 0x18 | FIFO configuration: TX/RX FIFO thresholds, non-FIFO access mode |
| I2C_DATA_REG | 0x1C | FIFO read/write data port (byte access) |

### Command Queue
| Register | Offset | Description |
|---|---|---|
| I2C_COMD0_REG - I2C_COMD15_REG | 0x58 - 0x94 | 16 command registers, each encoding opcode, byte count, ACK settings. A DONE bit is set by hardware when each command completes. |

### Interrupts
| Register | Offset | Description |
|---|---|---|
| I2C_INT_RAW_REG | 0x20 | Raw interrupt status |
| I2C_INT_CLR_REG | 0x24 | Interrupt clear (write-1-to-clear) |
| I2C_INT_ENA_REG | 0x28 | Interrupt enable mask |
| I2C_INT_STATUS_REG | 0x2C | Masked interrupt status |

### Key Interrupt Sources
- **TRANS_COMPLETE**: All commands in queue executed successfully
- **END_DETECT**: END command executed (allows refilling command queue)
- **ACK_ERR**: Unexpected NACK received during WRITE
- **TIME_OUT**: SCL held low longer than timeout threshold
- **RXFIFO_FULL**: RX FIFO count exceeds threshold
- **TXFIFO_EMPTY**: TX FIFO count below threshold
- **ARBITRATION_LOST**: Bus arbitration lost (multi-master)
- **SLAVE_TRAN_COMP**: Slave transfer complete
- **TXFIFO_OVF**: TX FIFO overflow
- **RXFIFO_OVF**: RX FIFO overflow

### SCL Stretch / Filter
| Register | Offset | Description |
|---|---|---|
| I2C_SCL_FILTER_CFG_REG | 0x3C | SCL digital filter configuration |
| I2C_SDA_FILTER_CFG_REG | 0x40 | SDA digital filter configuration |

## Source Code References

### SOC Register Definitions
- **Register header**: https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/i2c_reg.h
- **Register struct**: https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/i2c_struct.h

### HAL Layer
- **I2C HAL LL**: https://github.com/espressif/esp-idf/blob/master/components/esp_hal_i2c/esp32/include/hal/i2c_ll.h

### API Documentation
- **I2C API**: https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-reference/peripherals/i2c.html

### Examples
- **I2C examples**: https://github.com/espressif/esp-idf/blob/master/examples/peripherals/i2c

### QEMU Implementation
- **ESP32 I2C in QEMU**: https://github.com/espressif/qemu/blob/esp-develop/hw/i2c/esp32_i2c.c

## Renode Implementation Analysis

### Existing Renode Models
- **EFR32 I2C reference**: https://github.com/renode/renode-infrastructure/blob/master/src/Emulator/Peripherals/Peripherals/I2C/EFR32_I2CController.cs
- **Cadence I2C reference**: https://github.com/renode/renode-infrastructure/blob/master/src/Emulator/Peripherals/Peripherals/I2C/Cadence_I2C.cs

Both reference implementations demonstrate the Renode `II2CPeripheral` interface and the pattern for connecting I2C slave devices to a master controller. The EFR32 model also uses a state-machine approach which maps well to the ESP32's command-based architecture.

### Implementation Approach

**Core Architecture: Command Queue Execution Engine**

The central challenge in emulating the ESP32 I2C controller is faithfully implementing the command queue execution model:

1. **Command queue parsing**: When a transfer is triggered (by writing to the TRANS_START bit in I2C_CTR_REG), the controller must iterate through COMD0-COMD15 registers, executing each command in sequence.

2. **Command execution logic**:
   - **RSTART**: Generate I2C START/repeated-START condition. No bus action needed in emulation beyond state tracking.
   - **WRITE**: Pop N bytes from the TX FIFO and send them to the addressed I2C slave device via Renode's `II2CPeripheral.Write()`. The first byte after START is the address+R/W byte. Check ACK if configured.
   - **READ**: Read N bytes from the slave device via `II2CPeripheral.Read()` and push them into the RX FIFO. Send ACK/NACK as configured per the command register.
   - **STOP**: Generate I2C STOP condition. Finalize the transaction with `II2CPeripheral.FinishTransmission()`.
   - **END**: Pause command execution and raise END_DETECT interrupt. This allows software to refill the command queue or FIFO for long transfers. Execution resumes when TRANS_START is set again.

3. **FIFO management**: Maintain 32-byte TX and RX FIFOs. Track fill levels for interrupt generation (TXFIFO_EMPTY, RXFIFO_FULL). Handle FIFO overflow/underflow errors.

4. **Interrupt generation**: Raise appropriate interrupts at each stage -- TRANS_COMPLETE when all commands are done, ACK_ERR on unexpected NACK, END_DETECT on END command, timeout on stuck bus.

5. **Status register**: Update I2C_SR_REG to reflect current bus state, master FSM state, and FIFO byte counts.

**Master Mode (PRIMARY)**:
- Parse the 7-bit address from the first WRITE byte after RSTART
- Route transactions to the appropriate `II2CPeripheral` slave on the Renode I2C bus
- Track the current slave address across the transaction

**Slave Mode (LOWER PRIORITY)**:
- Register the controller as an `II2CPeripheral` on the bus
- Respond to address matches against I2C_SLAVE_ADDR_REG
- Load response data from the TX FIFO
- Store received data in the RX FIFO

**Key simplifications for emulation:**
- SCL/SDA timing registers (low period, high period, hold, sample) can be stored but have no functional effect
- Digital filters (SCL_FILTER, SDA_FILTER) can be ignored
- Timeout can be stubbed (or implemented as a simple transfer watchdog)
- Clock stretching is not relevant in emulation
- Arbitration detection is not needed for typical single-master scenarios

**QEMU reference**: The `esp32_i2c.c` QEMU implementation validates this command-queue approach and shows which registers are critical for driver compatibility.

## Complexity Assessment

| Component | Complexity | Priority | Rationale |
|---|---|---|---|
| **Command queue engine** | MEDIUM-HIGH | HIGH | Core of the I2C controller. 5 command types, sequential execution, DONE bit tracking. Must be correct for ESP-IDF driver. |
| **TX/RX FIFOs** | LOW-MEDIUM | HIGH | 32-byte FIFOs with threshold-based interrupts. Standard FIFO implementation. |
| **Master mode** | MEDIUM | HIGH | Address parsing, slave routing via Renode I2C bus, read/write transactions. |
| **Interrupt generation** | MEDIUM | HIGH | 13 interrupt sources. TRANS_COMPLETE, ACK_ERR, and END_DETECT are critical. |
| **Status register** | LOW | MEDIUM | Bus state and FSM state tracking. Mostly read-only reporting. |
| **Slave mode** | MEDIUM | LOW | Needed only when emulating an ESP32 acting as I2C slave. Uncommon scenario. |
| **Clock/timing config** | LOW | LOW | Store register values but no functional effect in emulation. |

**Overall I2C complexity: MEDIUM**

The I2C controller is moderately complex. The command-queue architecture is the key differentiator from simpler I2C controllers and requires careful implementation, but the overall register count is manageable and the QEMU reference implementation provides a proven model. The command queue is a finite set of 5 opcodes with straightforward semantics. Most real-world use cases only require master mode with RSTART-WRITE-READ-STOP sequences.

**Estimated register count**: ~40 registers per instance, ~80 total across both instances.
