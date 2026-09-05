# Wave 1 Report — ESP32-C3 GP-SPI (SPI2) Master + GPIO Pin-Edge Interrupts

Substrate for emulating 433 MHz radios (CC1101 / SX1278) on an emulated
ESP32-C3 in Renode. This wave delivers a working SPI2 master that clocks bytes
to a connected `ISPIPeripheral` slave, and GPIO pin-edge → interrupt support so
a radio's IRQ line (GDO/DIO) can raise a CPU interrupt.

**Status: complete. All acceptance criteria met and proven in Renode.**

Branch: `feature/esp32c3-gpspi-master` (on `desktop.buddy.mithis.com`, repo
`~/renode-espemu-gpspi`, forked from `main` @ `91abb0c`).

---

## Files added / changed

### Added
| File | Purpose |
|------|---------|
| `peripherals/spi2/ESP32C3_SPI2.cs` | ESP32-C3 GP-SPI (SPI2/FSPI) **master** model at 0x60024000. `NullRegistrationPointPeripheralContainer<ISPIPeripheral>` that holds one slave and clocks bytes to it. |
| `peripherals/spi2/SPILoopbackTester.cs` | Trivial `ISPIPeripheral` + `INumberedGPIOOutput` test slave (returns `byte ^ 0xA5`, raises an IRQ line on byte `0xAA`). Test fixture, not production. |
| `peripherals/spi2/spi2_tester.repl` | Test-only overlay: attaches the tester to `spi2` and wires its IRQ to `gpio@4`. |
| `peripherals/spi2/firmware/` | ESP-IDF app (`main/test_spi2.c`) doing `spi_bus_initialize` + `spi_device_polling_transmit` + a GPIO-IRQ test, printing `[SPI2]` results. |
| `peripherals/spi2/test.robot` | Robot Framework suite (3 tests). |
| `docs/peripherals/spi2.md` | Peripheral documentation. |
| `WAVE1-REPORT.md` | This file. |

### Changed
| File | Change |
|------|--------|
| `platforms/cpus/esp32c3.repl` | Replaced the SPI2 Python zero-stub with `spi2: SPI.ESP32C3_SPI2 @ sysbus 0x60024000`. Added `0 -> intmatrix@16` under `gpio` (aggregate GPIO interrupt → matrix source 16). |
| `peripherals/gpio/ESP32C3_GPIO.cs` | Implemented `IGPIOReceiver` (per-pin edge/level detection vs `GPIO_PINn_INT_TYPE`) and `INumberedGPIOOutput` (output 0 = aggregate GPIO interrupt). `GPIO_IN` now also reflects externally-driven input pins; `PCPU_INT` reflects `STATUS & INT_ENA`. Existing output/loopback behavior preserved. |
| `tests/esp32c3_setup.resc` | Added `include $base/peripherals/spi2/ESP32C3_SPI2.cs`. |
| `hello_world/setup.resc` | Added the same include (this resc also loads `esp32c3.repl`). |

---

## How to run the test (exact command)

Firmware artifacts (`build/`) are git-ignored, so build once, then run the suite
exactly as CI does:

```bash
# On desktop.buddy.mithis.com, in ~/renode-espemu-gpspi
source ~/esp/esp-idf/export.sh
idf.py -C peripherals/spi2/firmware set-target esp32c3
idf.py -C peripherals/spi2/firmware build

renode-test \
  --variable "BASE:$PWD" \
  --variable "ROM_ELF:$HOME/esp/esp-rom-elfs/esp32c3_rev3_rom.elf" \
  peripherals/spi2/test.robot
```

(`renode-test` needs `robotframework` for its `python3`; installed on buddy via
`pip3 install --user --break-system-packages robotframework==6.1`.)

---

## What passes

`peripherals/spi2/test.robot` — 3/3 OK:

1. **Should Round Trip Bytes Through SPI2 Slave** — the SPI gate.
2. **Should Deliver Slave Driven Interrupt To CPU** — the interrupt gate.
3. **Should Report All SPI2 Tests Passed** — `PASSED=8 FAILED=0 TOTAL=8`.

Actual firmware UART transcript (captured in Renode):

```
[SPI2] === ESP32-C3 SPI2 Master Test ===
[SPI2] spi_bus_initialize rc=0
[SPI2] spi_bus_add_device rc=0
[SPI2] === Part A: loopback round-trip ===
[SPI2] xfer rc=0 rx=a4 a7 a6 a1
[SPI2] TEST_PASS rt0 got=0xa4          # 0x01 ^ 0xA5
[SPI2] TEST_PASS rt1 got=0xa7          # 0x02 ^ 0xA5
[SPI2] TEST_PASS rt2 got=0xa6          # 0x03 ^ 0xA5
[SPI2] TEST_PASS rt3 got=0xa1          # 0x04 ^ 0xA5
[SPI2] xfer2 rc=0 rx=7b 08 1b          # {DE,AD,BE} ^ 0xA5
[SPI2] TEST_PASS rt2_0 got=0x7b
[SPI2] TEST_PASS rt2_1 got=0x08
[SPI2] TEST_PASS rt2_2 got=0x1b
[SPI2] === Part B: slave-driven IRQ ===
[SPI2] irq_count before=0
[SPI2] irq_count after=1
[SPI2] TEST_PASS irq_received count=1
[SPI2] === Tests Complete ===
[SPI2] PASSED=8 FAILED=0 TOTAL=8
```

The bytes round-trip through the real ESP-IDF `spi_master` driver: it clocks
each MOSI byte to `slave.Transmit()` and reads the returned MISO byte back from
the W0..W15 data buffer. Part B proves a **slave-driven** interrupt reaches the
CPU: the tester asserts its IRQ line during an SPI byte, the GPIO edge routes
through interrupt-matrix source 16 → CLIC → CPU → the ESP-IDF GPIO ISR.

Regression: `hello_world`, `gpio`, `interrupt-matrix`, `systimer`, `efuse`
suites still pass with these changes.

---

## What is modeled to the TRM (no captured baseline)

There is **no captured ESP32-C3 hardware baseline for SPI2** in this repo. The
register offsets, bit positions and reset values are modeled from the ESP32-C3
TRM and the ESP-IDF v5.4.1 headers (`spi_reg.h`, `spi_ll.h`). Specifically the
CPU polling path the driver uses:

- `CMD` (0x00): `SPI_UPDATE` (bit23) and `SPI_USR` (bit24) — both self-clearing.
- `USER`/`USER1`/`USER2` (0x10/0x14/0x18): phase enables, command/address bit
  lengths and command value.
- `MS_DLEN` (0x1C): data phase bit length.
- `W0..W15` (0x98..0xD4): CPU data buffer (full-duplex).
- `DMA_INT_RAW/ENA/CLR` (0x3C/0x34/0x38): `TRANS_DONE` (bit12) handshake that
  `spi_ll_usr_is_done()` polls.
- `DATE` (0xF0): `0x02007220` (TRM default; **not** silicon-verified).

## What is stubbed / deferred

- **Interrupt-driven SPI transfers** (`SPI2_INTR` / interrupt-matrix source 19).
  Only the polling path is wired; firmware uses `spi_device_polling_transmit`.
  `DMA_INT_ENA` is stored so a real IRQ line can be added later with no register
  changes.
- **GDMA-backed transfers** (`SPI_DMA_CONF`). Firmware uses `SPI_DMA_DISABLED`
  (CPU data buffer). Adding DMA is a later wave if a driver needs it.
- **Clock divider / CS setup-hold timing, dual/quad line modes** — accepted as
  register writes, no timing effect (functional model, not cycle-accurate).
- **Non-byte-multiple command/address/data bit lengths** — rounded up to whole
  bytes. Radio drivers use byte-multiple transfers.
- **GPIO level-triggered interrupt types** (`INT_TYPE` 4/5) — modeled simply
  (status set while the level holds when the line changes). Edge types (1/2/3)
  are exact. Radios use edge IRQs.

---

## Interface the CC1101 / SX1278 radio-model agents build against

Both radios follow the **exact** shape of `SPILoopbackTester` (which in turn
mirrors Renode's own `Wireless.CC2520`): an `ISPIPeripheral` that is also an
`INumberedGPIOOutput` for its IRQ line(s).

### 1. Attaching an `ISPIPeripheral` slave in the .repl

The SPI2 master is a container. A slave attaches as its child with `@ spi2`:

```
// platforms/cpus/esp32c3.repl already defines:
//   spi2: SPI.ESP32C3_SPI2 @ sysbus 0x60024000
//   gpio: GPIOPort.ESP32C3_GPIO @ sysbus 0x60004000
//       0 -> intmatrix@16

radio: SPI.CC1101 @ spi2            // <-- your radio, child of spi2
    0 -> gpio@4                     // <-- radio IRQ (GDO0) -> GPIO pin 4
```

Only one slave per master (`NullRegistrationPoint`). The C# class MUST live in
namespace `Antmicro.Renode.Peripherals.<Category>` (e.g. `...SPI` or
`...Wireless`) and be referenced in the .repl by the short form
`<Category>.<Class>` — Renode's .repl type resolver only searches the
`Antmicro.Renode.Peripherals.*` namespace tree. Include the `.cs` via the setup
`.resc` before `LoadPlatformDescription`.

### 2. Implementing the radio (C# contract)

```csharp
namespace Antmicro.Renode.Peripherals.Wireless   // or .SPI
{
    public class CC1101 : ISPIPeripheral, INumberedGPIOOutput
    {
        public byte Transmit(byte data) { /* your register FSM */ return miso; }
        public void FinishTransmission() { /* CS deasserted: reset byte counter */ }
        public void Reset() { /* power-on register defaults */ }

        // IRQ lines (GDO0/GDO2 for CC1101; DIO0.. for SX1278).
        public IReadOnlyDictionary<int, IGPIO> Connections { get; }  // 0 -> gpio@N
    }
}
```

`Transmit(b)` is called once per SPI byte, in order (command phase, then address,
then MOSI/MISO data), and its return value is what the CPU reads back on MISO.
`FinishTransmission()` is called at the end of each transfer (chip-select
deassert) — use it to reset your per-transaction byte index.

### 3. Raising an IRQ that reaches the CPU

Assert a numbered GPIO output (your `Connections[n]`) and wire it to a GPIO input
pin in the .repl (`n -> gpio@PIN`). Firmware configures that pin with
`gpio_config()` (edge type) + `gpio_install_isr_service()` +
`gpio_isr_handler_add()`; ESP-IDF maps `ETS_GPIO_INTR_SOURCE` (**16**) through
the interrupt matrix to a CLIC line. Setting the GPIO high (rising edge) delivers
the interrupt to the CPU. Proven working end-to-end in Part B above.

> NOTE: the GPIO interrupt-matrix source is **16** (`ETS_GPIO_INTR_SOURCE`,
> ESP-IDF v5.4.1 `soc/esp32c3/include/soc/interrupts.h`), not 14 as
> `docs/peripherals/interrupt-matrix.md` currently states (that value is for a
> different SoC). SPI2's own interrupt would be source 19, but it is not wired
> (polling path only).

### 4. Test-slave pattern to copy

`peripherals/spi2/SPILoopbackTester.cs` + `peripherals/spi2/spi2_tester.repl` +
`peripherals/spi2/firmware/main/test_spi2.c` + `peripherals/spi2/test.robot` are
a complete, working template for a radio model, its .repl wiring, an ESP-IDF
driver-level firmware test, and a Robot suite.
