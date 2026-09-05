# Shared 433 MHz air medium (`Wireless.Air433Medium`)

`peripherals/air/Air433Medium.cs` is a **functional** delivery bus that lets a
frame reach every receiving radio on the emulated 433 MHz band. It models *who
hears a frame*, not the RF/PHY — there is no modulation, bitrate, RSSI, timing
or channel selectivity.

## Model

- A receiving radio implements `IAir433Node` (one `ReceiveAirFrame(byte[])`
  callback) and joins the medium through an optional `medium` **constructor
  argument** set in the `.repl`. `CC1101` and `SX1278` both do this.
- `Transmit(frame, sender)` delivers the frame synchronously to every registered
  node except `sender`. `sender == null` is a synthetic **external** transmitter.
- `InjectFrame("A7 11 22 …")` parses a space/comma-separated hex string and calls
  `Transmit(frame, null)`.

The medium is registered on the sysbus at `0x60030000` (a tiny window it does
not otherwise use — reads return 0, writes are ignored) so the Renode monitor /
Robot can address it by name and inject frames.

## Attach + inject

```
air: Wireless.Air433Medium @ sysbus 0x60030000

radio: SPI.CC1101 @ spi2
    medium: air
    0 -> gpio@4          // GDO0 packet-ready IRQ
    2 -> gpio@5
```

```
# from a test / the monitor, once the radio firmware has armed RX:
air InjectFrame "A7 11 22 33 44 55"
```

The receiving model loads the payload into its RX FIFO, sets the byte count
(`RXBYTES` / `RegFifo`), and asserts its packet-ready line (`GDO0` for CC1101,
`DIO0` for SX1278), which raises a GPIO interrupt to the ESP32-C3 firmware.

## Receive semantics

- **CC1101** — accepts a frame only in RX (`SRX`). GDO configured `0x06`
  (sync word) or `0x07` (CRC OK) asserts on reception and de-asserts when the RX
  FIFO is drained. The chip returns to IDLE after a good packet.
- **SX1278** — accepts a frame only in FSK RX (`RegOpMode` LongRangeMode=0, mode
  `0x05`). `DIO0` (PayloadReady) asserts on reception; `RegFifo` (0x00) reads pop
  the payload and `DIO0` de-asserts when drained.

## Deferred

OOK-edge (raw GDO2 waveform) RX, the TX path (`Transmit(frame, this)` exists but
no strobe/opmode change drives it yet), packet-engine realism (CRC / address
filter / status bytes), and channel/frequency selectivity. See
`WAVE3-REPORT.md`.
