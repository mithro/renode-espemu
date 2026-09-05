# ESP32-C3 GP-SPI (SPI2 / FSPI) Master

Renode model: `peripherals/spi2/ESP32C3_SPI2.cs`
Class: `Antmicro.Renode.Peripherals.SPI.ESP32C3_SPI2`
Base address: `0x60024000` (`DR_REG_SPI2_BASE`), size `0x1000`.

## Overview

SPI2 (a.k.a. FSPI / `SPI2_HOST`) is the ESP32-C3's general-purpose SPI. This
model implements it as an SPI **master** that clocks bytes to a single connected
`ISPIPeripheral` slave. It models enough of the register set for the ESP-IDF
`spi_master` driver's CPU (non-DMA) **polling** path:

```
spi_bus_initialize(SPI2_HOST, &cfg, SPI_DMA_DISABLED);
spi_bus_add_device(SPI2_HOST, &devcfg, &dev);
spi_device_polling_transmit(dev, &transaction);   // full-duplex
```

It is a functional model, not cycle-accurate. There is **no captured hardware
baseline**; registers are modeled from the ESP32-C3 TRM and ESP-IDF v5.4.1
(`components/soc/esp32c3/register/soc/spi_reg.h`,
`components/hal/esp32c3/include/hal/spi_ll.h`).

## Attaching a slave

The peripheral is a `NullRegistrationPointPeripheralContainer<ISPIPeripheral>`.
A slave attaches in the `.repl` as a child:

```
spi2: SPI.ESP32C3_SPI2 @ sysbus 0x60024000
radio: SPI.CC1101 @ spi2
    0 -> gpio@4          // optional: radio IRQ -> GPIO pin 4
```

Only one slave (NullRegistrationPoint). The slave class must be in namespace
`Antmicro.Renode.Peripherals.*` and its `.cs` included before the platform is
loaded.

## Registers modeled

| Offset | Name | Notes |
|-------:|------|-------|
| 0x00 | CMD | `SPI_UPDATE` (bit23) and `SPI_USR` (bit24) self-clear. Writing `SPI_USR=1` runs the transfer. |
| 0x04 | ADDR | Address phase value. |
| 0x08 | CTRL | Stored. |
| 0x0C | CLOCK | Stored (no timing effect). |
| 0x10 | USER | Phase enables: `DOUTDIN`(0), `USR_MOSI`(27), `USR_MISO`(28), `USR_DUMMY`(29), `USR_ADDR`(30), `USR_COMMAND`(31). |
| 0x14 | USER1 | `USR_ADDR_BITLEN` [31:27]. |
| 0x18 | USER2 | `USR_COMMAND_VALUE` [15:0], `USR_COMMAND_BITLEN` [31:28]. |
| 0x1C | MS_DLEN | `MS_DATA_BITLEN` [17:0] (data phase bits − 1). |
| 0x20 | MISC | Stored. |
| 0x30 | DMA_CONF | Stored (DMA transfers not modeled). |
| 0x34 | DMA_INT_ENA | Stored (`TRANS_DONE` bit12). |
| 0x38 | DMA_INT_CLR | W1C into DMA_INT_RAW. |
| 0x3C | DMA_INT_RAW | `TRANS_DONE` (bit12) set on transfer completion; polled by `spi_ll_usr_is_done()`. |
| 0x40 | DMA_INT_ST | `RAW & ENA`. |
| 0x98..0xD4 | W0..W15 | 64-byte CPU data buffer (full-duplex). |
| 0xE0 | SLAVE | Stored. |
| 0xE8 | CLK_GATE | Stored. |
| 0xF0 | DATE | `0x02007220` (TRM default, unverified vs silicon). |

## Transfer semantics

On `SPI_USR`:
1. **Command** phase (if `USR_COMMAND`): `USR_COMMAND_BITLEN+1` bits from
   `USER2[15:0]`, byte-multiples, low byte first.
2. **Address** phase (if `USR_ADDR`): `USR_ADDR_BITLEN+1` bits from `ADDR`,
   byte-multiples, MSB first.
3. **Data** phase (if `USR_MOSI`/`USR_MISO`): `MS_DATA_BITLEN+1` bits from
   `W0..`. For each byte: `in = slave.Transmit(mosi ? bufByte : 0)`; if
   `USR_MISO`, `in` is written back to the buffer.
4. `slave.FinishTransmission()`, then `DMA_INT_RAW.TRANS_DONE = 1` and
   `SPI_USR`/`SPI_UPDATE` self-clear.

## Not modeled (deferred)

- Interrupt-driven transfers (`SPI2_INTR`, interrupt-matrix source 19) — polling
  only. `DMA_INT_ENA` is stored so an IRQ line can be added later.
- GDMA transfers (`SPI_DMA_CONF`) — use `SPI_DMA_DISABLED`.
- Clock/CS timing, dual/quad line modes.
- Non-byte-multiple bit lengths (rounded up to whole bytes).

## Tests

- Model: `peripherals/spi2/ESP32C3_SPI2.cs`
- Test slave: `peripherals/spi2/SPILoopbackTester.cs` (returns `byte ^ 0xA5`,
  raises IRQ on byte `0xAA`)
- Overlay: `peripherals/spi2/spi2_tester.repl`
- Firmware: `peripherals/spi2/firmware/main/test_spi2.c`
- Suite: `peripherals/spi2/test.robot`

```bash
source ~/esp/esp-idf/export.sh
idf.py -C peripherals/spi2/firmware set-target esp32c3
idf.py -C peripherals/spi2/firmware build
renode-test --variable "BASE:$PWD" \
  --variable "ROM_ELF:$HOME/esp/esp-rom-elfs/esp32c3_rev3_rom.elf" \
  peripherals/spi2/test.robot
```
