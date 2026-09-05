# Wave 2 Report — Renode SX1278 (RA-02) Radio Model

A Semtech SX1276/77/78 (SX127x) radio peripheral for Renode, attached as an
`ISPIPeripheral` slave of the wave-1 ESP32-C3 SPI2 (GP-SPI) master. It lets the
ESP32-C3 firmware's SX1278 driver do register-level access (`SxReset` / `SxReg` /
`SxStatus` / `identify()`) entirely in emulation, with no hardware.

**Status: complete for the foundation scope. All tests pass in Renode.**

Branch: `feature/renode-sx1278` (on `desktop.buddy.mithis.com`, worktree
`~/renode-espemu-sx1278`, forked from `feature/esp32c3-gpspi-master` @
`83ee682`). Not pushed.

---

## Files added

| File | Purpose |
|------|---------|
| `peripherals/sx1278/SX1278.cs` | The radio model. `ISPIPeripheral` + `INumberedGPIOOutput` (DIO0). Namespace `Antmicro.Renode.Peripherals.SPI`, short form `SPI.SX1278`. |
| `peripherals/sx1278/sx1278_tester.repl` | Test overlay: `radio: SPI.SX1278 @ spi2` with `0 -> gpio@4` (DIO0). |
| `peripherals/sx1278/firmware/` | ESP-IDF app (`main/test_sx1278.c`) driving SPI2 with the SX127x address framing, printing `[SX1278]` lines. |
| `peripherals/sx1278/test.robot` | Robot Framework suite (3 tests). |
| `docs/peripherals/sx1278.md` | Peripheral documentation. |
| `SX1278-MODEL-REPORT.md` | This file. |

No shared files changed: the model is a test fixture included by the robot suite
(mirroring how wave-1's `SPILoopbackTester.cs` was NOT added to
`esp32c3_setup.resc`), so `tests/esp32c3_setup.resc` and `platforms/` are
untouched. `firmware/build/` and `sdkconfig` are git-ignored by the existing
root `.gitignore`.

---

## Exact test command

```bash
# On desktop.buddy.mithis.com, in ~/renode-espemu-sx1278
source ~/esp/esp-idf/export.sh
idf.py -C peripherals/sx1278/firmware set-target esp32c3
idf.py -C peripherals/sx1278/firmware build

renode-test \
  --variable "BASE:$PWD" \
  --variable "ROM_ELF:$HOME/esp/esp-rom-elfs/esp32c3_rev3_rom.elf" \
  peripherals/sx1278/test.robot
```

## Result: PASS (3/3 robot tests, 9/9 firmware assertions)

```
+++++ Finished test 'test.Should Read RegVersion As 0x12' ... OK
+++++ Finished test 'test.Should Round Trip RegOpMode And Registers' ... OK
+++++ Finished test 'test.Should Report All SX1278 Tests Passed' ... OK
Tests finished successfully :)
```

Firmware UART assertions (captured from the Renode run):

```
[SX1278] === ESP32-C3 SX1278 Radio Test ===
[SX1278] TEST_PASS version got=0x12            # RegVersion 0x42 == 0x12
[SX1278] TEST_PASS opmode_default got=0x01     # RegOpMode 0x01 POR default
[SX1278] TEST_PASS opmode_sleep got=0x00       # write SLEEP, read back
[SX1278] TEST_PASS opmode_lora got=0x80        # LongRangeMode set in SLEEP
[SX1278] TEST_PASS opmode_lora_locked got=0x81 # bit7 locked outside SLEEP
[SX1278] TEST_PASS dio_mapping got=0x40        # ordinary reg write/read-back
[SX1278] TEST_PASS burst got=0x01              # burst write+read auto-increment
[SX1278] === Tests Complete ===
[SX1278] PASSED=9 FAILED=0 TOTAL=9
```

(The `burst` line's `got=0x01` is the firmware's own boolean-compare result of
the three read-back bytes `11 22 33`; `bitrate_default`=0x1A and
`reg_roundtrip`=0x07 are the remaining two of the nine passes.)

---

## How a test attaches the SX1278 in a `.repl`

The SPI2 master is a `NullRegistrationPointPeripheralContainer<ISPIPeripheral>`;
the radio attaches as its single child, and DIO0 (numbered GPIO output 0) wires
to a GPIO input pin:

```
// platforms/cpus/esp32c3.repl already provides spi2 and gpio:
//   spi2: SPI.ESP32C3_SPI2 @ sysbus 0x60024000
//   gpio: GPIOPort.ESP32C3_GPIO @ sysbus 0x60004000

radio: SPI.SX1278 @ spi2
    0 -> gpio@4          // DIO0 -> GPIO pin 4
```

`SX1278.cs` must be `include`d before the `.repl` that references it (the robot
keyword does `include @.../SX1278.cs` then `machine LoadPlatformDescription
@.../sx1278_tester.repl`). The class is in `Antmicro.Renode.Peripherals.SPI`
because Renode's `.repl` resolver only searches `Antmicro.Renode.Peripherals.*`.

---

## What is modeled (no overclaiming)

- **SPI framing**: address byte with bit7 = wnr (1 write / 0 read), 7-bit
  address; data bytes follow with burst address auto-increment; a new
  transaction starts after `FinishTransmission()` (CS deassert).
- **Register file**: flat `0x00`–`0x7F` byte array; writes persist and read back.
- **`RegVersion` (0x42)** = `0x12`; read-only (writes ignored).
- **`RegOpMode` (0x01)**: mode field and the rest read back writes;
  `LongRangeMode` (bit7, LoRa) is writable only while in SLEEP mode, per the
  datasheet — proven by the `opmode_lora` / `opmode_lora_locked` cases.
- **Reset map**: POR defaults from the SX1276/77/78/79 datasheet (Rev. 7)
  register table, FSK/OOK page. Registers not listed reset to `0x00`.
- **DIO0**: exposed as `INumberedGPIOOutput` (numbered output 0), wired in the
  `.repl`, for later waves.

## What is deferred (later wave, not claimed to work)

- **RF data path / FSK-OOK packet engine** — needs the shared 433 MHz air medium.
- **`RegFifo` (0x00)** as a real FIFO queue — currently a plain register byte.
- **DIO0 interrupt generation** (TxDone/RxDone/…): the line is exposed but the
  model never drives it high yet.
- **RSSI / AFC / PLL / temperature** dynamics — plain registers only.
- **RST GPIO input (`IGPIOReceiver`)**: not implemented/wired, because the
  ESP32-C3 GPIO model does not yet expose per-pin outputs to drive it. Machine
  reset restores POR defaults via `Reset()`.
- **Reset-map fidelity of reserved registers**: only the well-documented FSK/OOK
  defaults are populated; obscure reserved registers default to `0x00` rather
  than their exact silicon value.
