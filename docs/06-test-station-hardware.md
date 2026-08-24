# Test Station Hardware: rpi4-esp Board Inventory

Physical ESP/Nordic hardware on the rpi4-esp test station, per-board emulation capabilities, and recommended additions.

> **See also:**
> - [Development Methodology](04-development-methodology.md) — how to use this hardware for emulation validation
> - [Emulation Platform Status](02-emulation-platform-status.md) — what each emulator supports
> - [Accessing the Test Station](#5-accessing-the-test-station) (below) — SSH host, serial device names, flashing, USB power cycling
> - [rpi4-esp Test Station](07-rpi4-esp-test-station.md) — full station documentation: network config, USB topology, udev rules, WiFi adapter, power management
> - [Document Index](README.md)

---

## 1. Physical Hardware Inventory

The `rpi4-esp` test station has four development boards and a WiFi adapter connected to a Raspberry Pi 4 via USB:

| Board               | Chip                       | CPU                                  | Wireless                               | Key Peripherals                        |
| ------------------- | -------------------------- | ------------------------------------ | -------------------------------------- | -------------------------------------- |
| **ESP32-C3 board**  | ESP32-C3 (QFN32, rev v0.4) | RISC-V RV32IMC, 160 MHz, single-core | WiFi 802.11 b/g/n + BLE 5.0            | USB-JTAG/serial, 4MB XMC flash         |
| **ESP32-CAM-MB**    | ESP32-D0WD-V3 (rev v3.1)   | Xtensa LX6, 240 MHz, dual-core       | WiFi 802.11 b/g/n + BT 4.2 Classic+BLE | CH340 UART, 4MB flash, camera (OV2640) |
| **ESP32 DevKit**    | ESP32-D0WDQ6 (rev v1.0)    | Xtensa LX6, 240 MHz, dual-core       | WiFi 802.11 b/g/n + BT 4.2 Classic+BLE | CP2102 UART, 4MB flash                 |
| **nRF52840 dongle** | nRF52840 (PCA10059)        | ARM Cortex-M4F                       | BLE 5.0 + 802.15.4 (Thread/Zigbee)     | USB, currently in DFU bootloader       |
| **RTL8188CUS**      | Realtek RTL8188CUS         | --                                   | WiFi 802.11 b/g/n (2.4 GHz)            | USB WiFi adapter (for AP/client)       |

---

## 2. Per-Board Emulation Status

### ESP32-C3 Board — Best Candidate for Emulation

| Aspect                   | Physical Hardware                     | Renode                | QEMU (Espressif)              | Wokwi    |
| ------------------------ | ------------------------------------- | --------------------- | ----------------------------- | -------- |
| **CPU (RV32IMC)**        | 160 MHz single-core                   | **ISA supported**     | **Full**                      | **Full** |
| **UART**                 | USB-Serial/JTAG (built-in)            | **ESP32_UART exists** | **Yes**                       | **Yes**  |
| **WiFi (b/g/n)**         | 802.11 b/g/n, MAC `e8:3d:c1:8c:5c:ac` | No                    | No (OpenCores ETH workaround) | **Yes**  |
| **BLE 5.0**              | BLE 5.0 LE                            | No                    | No                            | No       |
| **Flash (4MB XMC)**      | SPI flash, mfg ID `0x46`              | No                    | **Yes** (SPI + MMU)           | **Yes**  |
| **GPIO strapping**       | Boot mode pins                        | No                    | **Yes**                       | **Yes**  |
| **Timer Groups**         | TIMG0, TIMG1                          | No                    | **Yes**                       | **Yes**  |
| **System Timer**         | 52-bit systimer                       | No                    | **Yes**                       | **Yes**  |
| **GDMA**                 | General DMA                           | No                    | **Yes**                       | Partial  |
| **Interrupt Matrix**     | RISC-V PLIC variant                   | No                    | **Yes**                       | **Yes**  |
| **eFuse**                | MAC addr, calibration                 | No                    | **Yes**                       | Partial  |
| **Crypto (AES/SHA/RSA)** | Hardware accelerators                 | No                    | **Yes**                       | No       |
| **USB Serial/JTAG**      | Built-in CDC+JTAG                     | No                    | No                            | Partial  |
| **RTC Control**          | Clock, reset, boot mode               | No                    | **Yes**                       | Partial  |
| **RNG**                  | True random number gen                | No                    | **Yes**                       | **Yes**  |

`★ Insight ─────────────────────────────────────`

The ESP32-C3 is the **single best starting point** for Renode emulation work. Here's why this board matters:

1. **RISC-V core already works** in Renode (RV32IMC is fully covered). No CPU architecture work needed.
2. **QEMU has 20+ peripherals** already implemented for ESP32-C3, providing reference implementations for every register model Renode would need.
3. **The physical board on rpi4-esp** provides a ground-truth test oracle -- flash the same binary on hardware and emulator, compare UART output.
4. The USB-JTAG/serial interface means the physical board can be **automatically flashed and monitored** via SSH to rpi4-esp for CI/comparison testing.

`─────────────────────────────────────────────────`

### ESP32-CAM-MB (ESP32-D0WD-V3) — Highest Demand, Hardest to Emulate

| Aspect               | Physical Hardware                    | Renode                            | QEMU (Espressif)         | Wokwi               |
| -------------------- | ------------------------------------ | --------------------------------- | ------------------------ | ------------------- |
| **CPU (Xtensa LX6)** | 240 MHz dual-core, rev v3.1          | Partial (generic Xtensa, not LX6) | **Full** (dual-core SMP) | **Full**            |
| **UART**             | Via CH340 USB bridge                 | **ESP32_UART exists**             | **Yes**                  | **Yes**             |
| **WiFi (b/g/n)**     | 802.11 b/g/n, soft-AP `ESP32-CAM-MB` | No                                | No (OpenCores ETH)       | **Yes**             |
| **BT 4.2 + BLE**     | Classic + BLE                        | No                                | No                       | No                  |
| **Flash (4MB)**      | SPI flash, mfg ID `0x68`             | No                                | **Yes**                  | **Yes**             |
| **Camera (OV2640)**  | DVP camera interface                 | No                                | No                       | **Yes** (simulated) |
| **PSRAM**            | Likely present (CAM boards)          | No                                | **Yes**                  | **Yes**             |
| **eFuse**            | Vref calibration in eFuse            | No                                | **Yes**                  | Partial             |

Key challenge: The ESP32-CAM-MB uses the **Xtensa LX6** architecture. Renode's current Xtensa support is for a generic `sample_controller` configuration, not the ESP32's specific LX6 dual-core configuration. QEMU's Espressif fork has the full ESP32 Xtensa configuration and is the only open-source reference for this.

The camera interface (OV2640 via DVP/I2S) is not emulated in any open-source tool. Wokwi has simulated camera support but it's closed-source.

### ESP32 DevKit (ESP32-D0WDQ6) — Same Architecture as CAM-MB

| Aspect               | Physical Hardware                   | Renode                | QEMU (Espressif) | Wokwi    |
| -------------------- | ----------------------------------- | --------------------- | ---------------- | -------- |
| **CPU (Xtensa LX6)** | Dual-core, rev v1.0 (older silicon) | Partial               | **Full**         | **Full** |
| **UART**             | Via CP2102 USB bridge               | **ESP32_UART exists** | **Yes**          | **Yes**  |
| **WiFi (b/g/n)**     | 802.11 b/g/n, soft-AP `ESP_1144E8`  | No                    | No               | **Yes**  |
| **BT 4.2 + BLE**     | Classic + BLE                       | No                    | No               | No       |
| **Flash (4MB)**      | GigaDevice (mfg `0xc8`)             | No                    | **Yes**          | **Yes**  |

Same emulation profile as the ESP32-CAM-MB minus the camera. Note: This is rev v1.0 (older silicon), while the CAM-MB is rev v3.1. For emulation purposes, the differences are minor (some errata fixes in later revisions).

### nRF52840 Dongle (PCA10059) — Already Well-Supported in Renode

| Aspect               | Physical Hardware            | Renode                               | QEMU            | Wokwi |
| -------------------- | ---------------------------- | ------------------------------------ | --------------- | ----- |
| **CPU (Cortex-M4F)** | ARM Cortex-M4 with FPU       | **Full**                             | Full (upstream) | No    |
| **BLE 5.0**          | BLE radio                    | **Yes** (NRF52840_Radio + BLEMedium) | No              | No    |
| **802.15.4**         | Thread/Zigbee radio          | **Yes** (via 802.15.4 medium)        | No              | No    |
| **USB**              | USB 2.0 Full Speed           | Partial                              | No              | No    |
| **GPIO/peripherals** | Full nRF52840 peripheral set | **Extensive**                        | Partial         | No    |

`★ Insight ─────────────────────────────────────`

The nRF52840 is notable because **Renode already has excellent support for it**, including wireless simulation. This makes the nRF52840 dongle on the test station a **bridge technology**: it can be used today in Renode for BLE and 802.15.4 testing, and the wireless medium infrastructure it uses (`BLEMedium`, `IEEE802_15_4Medium`) is exactly what would need to be extended for ESP32 wireless emulation.

A practical near-term scenario: emulate the ESP32-C6's 802.15.4 radio in Renode, use the existing nRF52840 model as the other node, and run Thread networking between them -- all in simulation. The physical nRF52840 dongle can validate the simulation against real hardware.

`─────────────────────────────────────────────────`


---

## 3. Recommended Test Station Additions

Based on the emulation research, these additions to the rpi4-esp station would maximize its value for emulation validation:

| Priority | Board              | Why                                                                                       |
| -------- | ------------------ | ----------------------------------------------------------------------------------------- |
| **High** | ESP32-C6 dev board | 802.15.4 radio is fully documented; can validate Thread emulation against nRF52840 dongle |
| **High** | ESP32-H2 dev board | Pure 802.15.4 + BLE device; ideal for Thread border router testing                        |
| Medium   | ESP32-S3 dev board | Most popular for AI/HMI; QEMU has full support; Xtensa LX7                                |
| Low      | ESP32-P4 dev board | Newest, highest performance; no wireless (needs companion chip)                           |


---

## 4. Summary: What Can Be Emulated Today

| Board on rpi4-esp          | Can run in Renode today?                     | Can run in QEMU today?                              | Can run in Wokwi today?          |
| -------------------------- | -------------------------------------------- | --------------------------------------------------- | -------------------------------- |
| **ESP32-C3**               | No (CPU works, no peripherals)               | **Yes** (boot ESP-IDF, UART, flash, crypto, timers) | **Yes** (most features + WiFi)   |
| **ESP32-CAM-MB** (D0WD-V3) | No (Xtensa not ESP32-configured)             | **Yes** (boot ESP-IDF, no camera)                   | **Yes** (incl. simulated camera) |
| **ESP32 DevKit** (D0WDQ6)  | No (same as above)                           | **Yes**                                             | **Yes**                          |
| **nRF52840 dongle**        | **Yes** (extensive support + BLE + 802.15.4) | Partial                                             | No                               |

**The nRF52840 is the only board on the test station that works in Renode today.** The ESP32-C3 is the closest to working -- it only needs peripheral models, not CPU architecture work.


---

## 5. Accessing the Test Station

The station is a Raspberry Pi 4 (`rpi4-esp`) on the Welland IoT VLAN. It is reachable only from that network (or via VPN into it); the hostname below is the same one `tools/capture_hardware_baseline.py` and `tools/ci.py` use by default.

```bash
ssh eth0.rpi4-esp.iot.welland.mithis.com
```

### Serial devices

udev rules on the station (`/etc/udev/rules.d/`) give each board a stable symlink, so scripts do not depend on enumeration order. All ports are `MODE=0666`, so no `sudo` is needed for serial access.

| Symlink            | Kernel device  | USB path  | Board                 | Bridge                     |
| ------------------ | -------------- | --------- | --------------------- | -------------------------- |
| `/dev/ttyESP32C3`  | `/dev/ttyACM1` | `1-1.2.4` | ESP32-C3              | Built-in USB-Serial/JTAG   |
| `/dev/ttyESP32CAM` | `/dev/ttyUSB1` | `1-1.2.3` | ESP32-CAM-MB          | CH340                      |
| `/dev/ttyESP32DEV` | `/dev/ttyUSB0` | `1-1.4`   | ESP32 DevKit          | CP2102                     |
| `/dev/ttyNRF52840` | `/dev/ttyACM0` | `1-1.2.2` | nRF52840 dongle       | Native USB (DFU bootloader)|

Prefer the symlinks. `tools/capture_hardware_baseline.py`, `tools/capture_all_baselines.py` and `tools/serial_capture.py` all default to `/dev/ttyESP32C3`, so they are unaffected by USB enumeration order.

### Flashing and monitoring the ESP32-C3

Two esptool installs exist on the station:

| Path                              | Notes                                                        |
| --------------------------------- | ------------------------------------------------------------ |
| `~/.venvs/esptool/bin/esptool`    | Per-user venv — this is what `tools/capture_*_baseline.py` invoke over SSH |
| `/opt/esptool/bin/esptool`        | System-wide venv (root-owned), also has `nrfutil` for the nRF52840 |

```bash
# Probe the chip
~/.venvs/esptool/bin/esptool --port /dev/ttyESP32C3 chip-id

# Flash a full ESP-IDF image (bootloader + partition table + app)
~/.venvs/esptool/bin/esptool --port /dev/ttyESP32C3 --baud 460800 write_flash \
    0x0 bootloader.bin 0x8000 partition-table.bin 0x10000 app.bin

# Monitor UART output
~/.venvs/esptool/bin/python3 -m serial.tools.miniterm /dev/ttyESP32C3 115200

# Or, with ESP-IDF on the station
idf.py -p /dev/ttyESP32C3 flash monitor
```

OpenOCD (`/usr/bin/openocd`) is installed for the ESP32-C3's built-in JTAG — see [Development Methodology §1](04-development-methodology.md#1-jtag-capabilities-on-rpi4-esp-hardware).

### USB power cycling (`uhubctl`)

Hung boards can be power-cycled without touching the hardware, but the power topology matters:

| Hub                      | Power switching     | Devices                                   |
| ------------------------ | ------------------- | ----------------------------------------- |
| VIA Labs (`1-1`)         | **Per-port**        | CP2102/ESP32 DevKit (port 4), Genesys hub (port 2) |
| Genesys Logic (`1-1.2`)  | **Ganged**          | wlanE, nRF52840, ESP32-CAM, ESP32-C3      |

Cycling the Genesys hub resets **all four** of its devices at once (including the RTL8188CUS WiFi adapter); only the ESP32 DevKit can be cycled independently. `uhubctl` lives in `/usr/sbin`, so it needs `sudo`.

```bash
sudo uhubctl                               # show current port power state
sudo uhubctl -l 1-1 -p 2 -a cycle -d 2     # ESP32-C3 + nRF52840 + ESP32-CAM + wlanE (all together)
sudo uhubctl -l 1-1 -p 4 -a cycle -d 2     # ESP32 DevKit only

# Softer alternative: rebind a single USB device without cutting power
echo '1-1.2.4' | sudo tee /sys/bus/usb/drivers/usb/unbind
echo '1-1.2.4' | sudo tee /sys/bus/usb/drivers/usb/bind
```

### Full station documentation

Network configuration (IPs, MACs, switch port), the complete USB topology, per-board details, udev rule details, the RTL8188CUS hostapd/client setup, and LLDP config are in [rpi4-esp Test Station](07-rpi4-esp-test-station.md).
