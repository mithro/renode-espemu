# ESP32 Emulation in Renode: Research Documentation

Research into the current state of ESP chip emulation and a roadmap for enabling Renode to fully emulate ESP32 WiFi/Bluetooth chipsets.

## Reading Order

| #   | Document                                                                 | What it covers                                                                     |
| --- | ------------------------------------------------------------------------ | ---------------------------------------------------------------------------------- |
| 1   | [ESP Hardware Reference](01-esp-hardware-reference.md)                   | All ESP chip families (ESP8266-ESP32-P4): specs, peripherals, CPU architectures    |
| 2   | [Emulation Platform Status](02-emulation-platform-status.md)             | What Renode, QEMU, and Wokwi support today, per-chip peripheral matrices           |
| 3   | [Wireless Hardware Documentation](03-wireless-hardware-documentation.md) | WiFi/BLE/802.15.4 reverse engineering, register maps, binary blob structure        |
| 4   | [Development Methodology](04-development-methodology.md)                 | JTAG debugging, execution tracing, AFL-style coverage-guided emulation development |
| 5   | [Gap Analysis and Roadmap](05-gap-analysis-and-roadmap.md)               | What's missing, wireless emulation strategies, phased implementation plan          |
| 6   | [Test Station Hardware](06-test-station-hardware.md)                     | rpi4-esp board inventory, per-board emulation status, recommended additions        |
| 7   | [rpi4-esp Test Station](07-rpi4-esp-test-station.md)                     | Full station reference: network, USB topology, udev rules, flashing, power cycling |

## Quick Summary

**Current state:** Renode has 1 ESP peripheral (UART). QEMU has ~20 peripherals for ESP32/S3/C3. No open-source emulator has WiFi or Bluetooth.

**Best starting point:** ESP32-C3 (RISC-V) -- Renode's CPU support already works, QEMU provides reference peripheral implementations, and the rpi4-esp test station has one for hardware validation.

**Wireless tractability (most to least):**

| Technology                    | Openness                     | Emulation approach                                           |
| ----------------------------- | ---------------------------- | ------------------------------------------------------------ |
| IEEE 802.15.4 (Thread/Zigbee) | Fully documented registers   | Register-level model + Renode's IEEE802_15_4Medium           |
| BLE                           | Clean VHCI/HCI interface     | Virtual HCI controller + Renode's BLEMedium                  |
| WiFi                          | Partially reverse-engineered | Virtual Ethernet (quick) or esp32-open-mac hybrid (faithful) |

**Key external projects:**
- [esp32-open-mac](https://github.com/esp32-open-mac/esp32-open-mac) (819 stars) -- open-source WiFi MAC via reverse engineering
- [Espressif QEMU fork](https://github.com/espressif/qemu) -- reference peripheral implementations
- [Tarlogic BT Reversing](https://github.com/TarlogicSecurity/ESP32-Bluetooth-Reversing) -- 40+ Bluetooth register definitions with SVD patches
