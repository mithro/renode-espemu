# ESP32 SD/SDIO/MMC Host Controller

## Overview

The ESP32 contains a single SD/SDIO/MMC host controller (referred to as SDMMC or SDHOST) that supports SD memory cards, SDIO devices, and MMC cards. The controller implements the SD Host Controller specification with an internal DMA engine (IDMAC -- Internal DMA Controller) for efficient data transfer. It supports both 1-bit and 4-bit SD bus modes and can operate at default speed (25 MHz) and high speed (50 MHz).

The SDMMC host controller is used for two primary purposes on the ESP32:
1. **SD card storage**: Accessing SD/SDHC/SDXC memory cards via the SD bus protocol (faster than SPI mode)
2. **SDIO peripherals**: Communicating with SDIO devices, most notably SDIO-based WiFi modules

The controller uses a command/response model where software issues SD commands via the CMD register and the hardware handles the command/response sequence on the bus. Data transfers use the IDMAC with linked-list descriptors in memory.

The SDMMC controller's register interface is based on the Synopsys DesignWare Mobile Storage Host Controller (DWC_mshc) IP, which has documentation and implementations available from other platforms.

## Hardware Specifications

| Feature | Value |
|---|---|
| **Number of instances** | 1 |
| **Base address** | 0x3FF68000 |
| **Supported protocols** | SD Memory (v3.01), SDIO (v3.0), MMC (v4.41) |
| **Bus widths** | 1-bit, 4-bit |
| **Max clock** | 50 MHz (high speed), 25 MHz (default speed) |
| **Card slots** | 2 (active one at a time, selected via card number in CMD register) |
| **DMA** | Internal DMA Controller (IDMAC) with linked-list descriptors |
| **FIFO depth** | 1024 bytes (256 x 32-bit words) |
| **Max block size** | 4096 bytes |
| **Response types** | Short (48-bit), Long (136-bit) |
| **Auto-stop** | Hardware auto-stop (CMD12) after multi-block transfers |
| **Data transfer modes** | Block transfer, stream transfer |

### Supported SD Commands
The controller supports all standard SD/SDIO/MMC commands:
- **Basic**: CMD0 (GO_IDLE), CMD2 (ALL_SEND_CID), CMD3 (SEND_RELATIVE_ADDR), CMD7 (SELECT_CARD), CMD8 (SEND_IF_COND)
- **Read**: CMD17 (READ_SINGLE_BLOCK), CMD18 (READ_MULTIPLE_BLOCK)
- **Write**: CMD24 (WRITE_BLOCK), CMD25 (WRITE_MULTIPLE_BLOCK)
- **App-specific**: ACMD6 (SET_BUS_WIDTH), ACMD41 (SD_SEND_OP_COND), ACMD51 (SEND_SCR)
- **SDIO**: CMD5 (IO_SEND_OP_COND), CMD52 (IO_RW_DIRECT), CMD53 (IO_RW_EXTENDED)

## TRM Chapter Reference

**ESP32 Technical Reference Manual, Chapter 15: SD/SDIO/MMC Host Controller**

- https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf

Key sections:
- 15.1: Overview of SDMMC host controller features
- 15.2: Controller block diagram and IDMAC architecture
- 15.3: Command path operation (command issue, response receive)
- 15.4: Data path operation (read/write, IDMAC linked-list)
- 15.5: Clock control and card detection
- 15.6: Interrupt handling
- 15.7: Register descriptions

Note: The register interface is based on the Synopsys DesignWare Mobile Storage Host Controller IP, so documentation for that IP block from other platforms (e.g., Rockchip, Allwinner) can provide additional insight.

## Register Map Summary

The SDMMC register space is approximately 0x200 bytes. Key registers include:

### Control and Configuration
| Register | Offset | Description |
|---|---|---|
| SDMMC_CTRL_REG | 0x00 | Controller control: reset, interrupt enable, DMA enable, read-wait |
| SDMMC_PWREN_REG | 0x04 | Power enable per card slot |
| SDMMC_CLKDIV_REG | 0x08 | Clock divider value (0-255, actual divider = 2 * value) |
| SDMMC_CLKSRC_REG | 0x0C | Clock source selection per card |
| SDMMC_CLKENA_REG | 0x10 | Clock enable and low-power mode per card |
| SDMMC_TMOUT_REG | 0x14 | Response timeout and data timeout values |
| SDMMC_CTYPE_REG | 0x18 | Card type: 1-bit or 4-bit bus width per card |
| SDMMC_BLKSIZ_REG | 0x1C | Block size (1-65535 bytes) |
| SDMMC_BYTCNT_REG | 0x20 | Byte count for data transfer |
| SDMMC_FIFOTH_REG | 0x4C | FIFO threshold watermark (TX threshold, RX threshold, DMA burst size) |
| SDMMC_CARDTHRCTL_REG | 0x100 | Card read threshold (for busy signaling) |

### Command Path
| Register | Offset | Description |
|---|---|---|
| SDMMC_CMDARG_REG | 0x28 | Command argument (32-bit value sent with command) |
| SDMMC_CMD_REG | 0x2C | Command register: command index (6 bits), response expect, response long, data expected, read/write, auto-stop, send-init, card number, **start_cmd** bit |
| SDMMC_RESP0_REG | 0x30 | Response bits [31:0] (short response) / bits [31:0] (long response) |
| SDMMC_RESP1_REG | 0x34 | Response bits [63:32] (long response) |
| SDMMC_RESP2_REG | 0x38 | Response bits [95:64] (long response) |
| SDMMC_RESP3_REG | 0x3C | Response bits [127:96] (long response) |

### Status
| Register | Offset | Description |
|---|---|---|
| SDMMC_STATUS_REG | 0x48 | Status: FIFO count, DMA request, state machine states, card-present, data-busy, data-3-status |

### Interrupt Handling
| Register | Offset | Description |
|---|---|---|
| SDMMC_RINTSTS_REG | 0x44 | Raw interrupt status (write-1-to-clear) |
| SDMMC_INTMASK_REG | 0x24 | Interrupt mask (enable/disable individual sources) |
| SDMMC_MINTSTS_REG | 0x40 | Masked interrupt status (RINTSTS & INTMASK) |

### Key Interrupt Sources
- **CMD_DONE (bit 2)**: Command completed
- **DATA_OVER (bit 3)**: Data transfer complete
- **TXDR (bit 4)**: TX FIFO threshold reached (needs data)
- **RXDR (bit 5)**: RX FIFO threshold reached (data available)
- **RESP_ERR (bit 1)**: Response CRC error
- **RCRC (bit 6)**: Response CRC error
- **DCRC (bit 7)**: Data CRC error
- **RTO (bit 8)**: Response timeout
- **DTO (bit 9)**: Data timeout (data starvation)
- **HTO (bit 10)**: Data starvation by host timeout
- **FRUN (bit 11)**: FIFO underrun/overrun
- **HLE (bit 12)**: Hardware locked write error
- **SBE (bit 13)**: Start bit error
- **CD (bit 0)**: Card detect
- **SDIO_INTERRUPT (bit 16)**: SDIO card interrupt (for SDIO devices)

### Internal DMA Controller (IDMAC)
| Register | Offset | Description |
|---|---|---|
| SDMMC_BMOD_REG | 0x80 | Bus mode: DMA enable, fixed burst, descriptor skip length, software reset |
| SDMMC_PLDMND_REG | 0x84 | Poll demand: trigger DMA to re-read descriptors |
| SDMMC_DBADDR_REG | 0x88 | Descriptor list base address (start of linked-list in memory) |
| SDMMC_IDSTS_REG | 0x8C | IDMAC status: normal/abnormal interrupt summary, specific DMA interrupts |
| SDMMC_IDINTEN_REG | 0x90 | IDMAC interrupt enable |
| SDMMC_DSCADDR_REG | 0x94 | Current host descriptor address (read-only) |
| SDMMC_BUFADDR_REG | 0x98 | Current buffer descriptor address (read-only) |

### IDMAC Descriptor Format
Each DMA descriptor is 16 bytes (4 x 32-bit words):
- **Word 0 (DES0)**: Control flags -- OWN, ER (end of ring), CH (chain mode), FS (first descriptor), LS (last descriptor), DIC (disable interrupt on completion)
- **Word 1 (DES1)**: Buffer size (bits [12:0] = buffer 1 size, bits [25:13] = buffer 2 size)
- **Word 2 (DES2)**: Buffer address 1 (pointer to data buffer in memory)
- **Word 3 (DES3)**: Buffer address 2 / next descriptor address (in chain mode)

### Data FIFO
| Register | Offset | Description |
|---|---|---|
| SDMMC_DATA_REG | 0x200 | Data FIFO read/write port (also accessible as 0x100-0x1FF mirror) |

## Source Code References

### SOC Register Definitions
- **Register header**: https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/sdmmc_reg.h
- **Register struct**: https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/register/soc/sdmmc_struct.h

### HAL Layer
- **SDMMC HAL LL**: https://github.com/espressif/esp-idf/blob/master/components/esp_hal_sd/esp32/include/hal/sdmmc_ll.h

### API Documentation
- **SDMMC Host API**: https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-reference/peripherals/sdmmc_host.html

### Examples
- **SD Card example**: https://github.com/espressif/esp-idf/blob/master/examples/storage/sd_card

### QEMU Implementation
No QEMU implementation exists for the ESP32 SDMMC peripheral.

## Renode Implementation Analysis

### Existing Renode Models

While Renode does not have a specific ESP32 SDMMC model, it does have SD card infrastructure and other SD host controller models that can serve as references. The Synopsys DesignWare MMC (DWC_mshc) IP is used in several SoCs, meaning implementations from other platforms may be partially reusable.

Renode's SD card device model (`SDCard.cs`) provides the card-side protocol implementation (command/response handling, block read/write, CSD/CID registers) which the host controller model needs to interface with.

### Implementation Approach

**Architecture Overview:**

The SDMMC host controller emulation has three main components:
1. **Command path**: Issue SD commands and receive responses
2. **Data path**: Transfer data blocks to/from the card using IDMAC
3. **Card interface**: Connect to Renode's SD card model

**Component 1: Command Path**

1. **Command issue**: When software writes to SDMMC_CMD_REG with start_cmd=1, the controller must:
   - Parse the command index (CMD0-CMD63) and argument from SDMMC_CMDARG_REG
   - Determine response type from CMD register flags (no response, short, long)
   - Send the command to the attached SD card model
   - Store the response in RESP0-RESP3 registers
   - Generate CMD_DONE interrupt

2. **Command handling flow**:
   - Software writes command argument to SDMMC_CMDARG_REG
   - Software writes command index and flags to SDMMC_CMD_REG with start_cmd=1
   - Hardware clears start_cmd when command is accepted
   - Hardware executes command on SD bus
   - Response is loaded into RESP0-RESP3
   - CMD_DONE interrupt is raised
   - If data is expected, data transfer begins

3. **Special commands**:
   - CMD0 (GO_IDLE): Reset card to idle state
   - CMD2/CMD3: Card identification and address assignment
   - CMD7: Card selection
   - CMD8/ACMD41: Voltage negotiation and initialization
   - CMD12: Stop transmission (auto-stop)

**Component 2: Data Path (IDMAC)**

1. **Descriptor chain parsing**: Read IDMAC descriptors from memory starting at SDMMC_DBADDR_REG. Each descriptor specifies a buffer address and size.

2. **Write (card program)**: For write commands (CMD24/CMD25), read data from DMA buffers (via descriptors) and send to the SD card model's write interface. Generate DATA_OVER interrupt when complete.

3. **Read (card read)**: For read commands (CMD17/CMD18), read data from the SD card model and write to DMA buffers (via descriptors). Generate DATA_OVER interrupt when complete.

4. **Descriptor control bits**: Handle OWN (DMA owns descriptor), FS/LS (first/last), CH (chain to next), ER (end of ring -- wrap to base address), DIC (disable interrupt per descriptor).

5. **IDMAC interrupts**: Generate TI (transmit complete), RI (receive complete), NIS/AIS (normal/abnormal interrupt summary) in SDMMC_IDSTS_REG.

**Component 3: Card Interface**

1. Connect to Renode's `SDCard` model which handles:
   - Card state machine (idle, ready, identification, standby, transfer, etc.)
   - CSD, CID, SCR register emulation
   - Block read/write to a backing storage file
   - OCR (Operating Conditions Register) and voltage negotiation

2. For SDIO devices, a separate SDIO device model would be needed that implements the SDIO command set (CMD5, CMD52, CMD53) and function-based I/O.

**Initialization Sequence (must be supported):**
The SD card initialization sequence that the ESP-IDF driver performs:
1. CMD0 (GO_IDLE_STATE) -- reset card
2. CMD8 (SEND_IF_COND) -- voltage check (SD v2.0+)
3. ACMD41 (SD_SEND_OP_COND) -- voltage negotiation, capacity class
4. CMD2 (ALL_SEND_CID) -- get card identification
5. CMD3 (SEND_RELATIVE_ADDR) -- get relative card address (RCA)
6. CMD9 (SEND_CSD) -- get card-specific data
7. CMD7 (SELECT_CARD) -- select card with RCA
8. ACMD6 (SET_BUS_WIDTH) -- switch to 4-bit mode
9. CMD16 (SET_BLOCKLEN) -- set block length (usually 512)

**Key simplifications:**
- Clock configuration (CLKDIV, CLKENA, CLKSRC) can be stored but has no timing effect
- Card detect can be hardwired to "card present" when an SD card model is attached
- CRC checking can be skipped (always report success)
- Timeout values can be stored but transfers complete instantly in emulation
- FIFO threshold configuration affects only interrupt timing, which can be simplified to trigger at transfer boundaries
- Power enable (PWREN) can be a no-op (always powered)

## Complexity Assessment

| Component | Complexity | Priority | Rationale |
|---|---|---|---|
| **Command path** | MEDIUM | HIGH | Core of the controller. Must handle command issue, response capture, and CMD_DONE interrupt. Well-defined protocol. |
| **IDMAC (DMA engine)** | MEDIUM-HIGH | HIGH | Linked-list descriptor parsing with control flags (OWN, FS, LS, CH, ER). Critical for data transfers. |
| **SD card read** | MEDIUM | HIGH | CMD17/CMD18 with IDMAC for block reads. Primary use case for SD card storage. |
| **SD card write** | MEDIUM | HIGH | CMD24/CMD25 with IDMAC for block writes. Required for filesystem write support. |
| **Card initialization** | MEDIUM | HIGH | Must support full init sequence (CMD0/CMD8/ACMD41/CMD2/CMD3/CMD7). Many command-response pairs. |
| **Interrupt handling** | MEDIUM | HIGH | CMD_DONE, DATA_OVER, and error interrupts are critical. IDMAC interrupts for DMA completion. |
| **Status register** | LOW-MEDIUM | MEDIUM | FIFO count, state machine state, card-present. Mostly read-only reporting. |
| **SDIO support** | HIGH | LOW | CMD52/CMD53 and function-based I/O. Complex protocol. Only needed for SDIO WiFi. |
| **Multi-block + auto-stop** | MEDIUM | MEDIUM | CMD18/CMD25 with automatic CMD12. Common for filesystem I/O. |
| **Clock/power control** | LOW | LOW | Store values, no functional effect in emulation. |

**Overall SDMMC complexity: MEDIUM-HIGH**

The SDMMC controller is moderately complex with a well-defined register interface based on the Synopsys DWC_mshc IP (which provides external documentation). The command/response model is straightforward but requires implementing a significant number of SD commands for card initialization alone. The IDMAC is a standard linked-list DMA engine. The main complexity comes from the breadth of SD protocol commands needed, the DMA descriptor management, and correct interrupt sequencing.

The lack of a QEMU reference implementation means the register-level behavior must be derived primarily from the TRM, the HAL LL source, and Synopsys DWC_mshc documentation from other platforms. However, Renode's existing SD card infrastructure handles much of the card-side protocol complexity.

**Estimated register count**: ~45 registers (single instance).
