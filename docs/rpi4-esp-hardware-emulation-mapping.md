# rpi4-esp Test Station: Hardware vs Emulation Capability Mapping

**Date:** 2026-03-28
**Purpose:** Map the physical ESP/Nordic hardware on the `rpi4-esp` test station to the emulation capabilities documented in the companion reports, identifying what can be emulated today and what gaps remain.

---

## 1. Physical Hardware Inventory

The `rpi4-esp` test station has four development boards and a WiFi adapter connected to a Raspberry Pi 4 via USB:

| Board | Chip | CPU | Wireless | Key Peripherals |
|---|---|---|---|---|
| **ESP32-C3 board** | ESP32-C3 (QFN32, rev v0.4) | RISC-V RV32IMC, 160 MHz, single-core | WiFi 802.11 b/g/n + BLE 5.0 | USB-JTAG/serial, 4MB XMC flash |
| **ESP32-CAM-MB** | ESP32-D0WD-V3 (rev v3.1) | Xtensa LX6, 240 MHz, dual-core | WiFi 802.11 b/g/n + BT 4.2 Classic+BLE | CH340 UART, 4MB flash, camera (OV2640) |
| **ESP32 DevKit** | ESP32-D0WDQ6 (rev v1.0) | Xtensa LX6, 240 MHz, dual-core | WiFi 802.11 b/g/n + BT 4.2 Classic+BLE | CP2102 UART, 4MB flash |
| **nRF52840 dongle** | nRF52840 (PCA10059) | ARM Cortex-M4F | BLE 5.0 + 802.15.4 (Thread/Zigbee) | USB, currently in DFU bootloader |
| **RTL8188CUS** | Realtek RTL8188CUS | -- | WiFi 802.11 b/g/n (2.4 GHz) | USB WiFi adapter (for AP/client) |

---

## 2. Per-Board Emulation Status

### ESP32-C3 Board — Best Candidate for Emulation

| Aspect | Physical Hardware | Renode | QEMU (Espressif) | Wokwi |
|---|---|---|---|---|
| **CPU (RV32IMC)** | 160 MHz single-core | **ISA supported** | **Full** | **Full** |
| **UART** | USB-Serial/JTAG (built-in) | **ESP32_UART exists** | **Yes** | **Yes** |
| **WiFi (b/g/n)** | 802.11 b/g/n, MAC `44:1b:f6:2e:a9:a4` | No | No (OpenCores ETH workaround) | **Yes** |
| **BLE 5.0** | BLE 5.0 LE | No | No | No |
| **Flash (4MB XMC)** | SPI flash, mfg ID `0x46` | No | **Yes** (SPI + MMU) | **Yes** |
| **GPIO strapping** | Boot mode pins | No | **Yes** | **Yes** |
| **Timer Groups** | TIMG0, TIMG1 | No | **Yes** | **Yes** |
| **System Timer** | 52-bit systimer | No | **Yes** | **Yes** |
| **GDMA** | General DMA | No | **Yes** | Partial |
| **Interrupt Matrix** | RISC-V PLIC variant | No | **Yes** | **Yes** |
| **eFuse** | MAC addr, calibration | No | **Yes** | Partial |
| **Crypto (AES/SHA/RSA)** | Hardware accelerators | No | **Yes** | No |
| **USB Serial/JTAG** | Built-in CDC+JTAG | No | No | Partial |
| **RTC Control** | Clock, reset, boot mode | No | **Yes** | Partial |
| **RNG** | True random number gen | No | **Yes** | **Yes** |

`★ Insight ─────────────────────────────────────`

The ESP32-C3 is the **single best starting point** for Renode emulation work. Here's why this board matters:

1. **RISC-V core already works** in Renode (RV32IMC is fully covered). No CPU architecture work needed.
2. **QEMU has 20+ peripherals** already implemented for ESP32-C3, providing reference implementations for every register model Renode would need.
3. **The physical board on rpi4-esp** provides a ground-truth test oracle -- flash the same binary on hardware and emulator, compare UART output.
4. The USB-JTAG/serial interface means the physical board can be **automatically flashed and monitored** via SSH to rpi4-esp for CI/comparison testing.

`─────────────────────────────────────────────────`

### ESP32-CAM-MB (ESP32-D0WD-V3) — Highest Demand, Hardest to Emulate

| Aspect | Physical Hardware | Renode | QEMU (Espressif) | Wokwi |
|---|---|---|---|---|
| **CPU (Xtensa LX6)** | 240 MHz dual-core, rev v3.1 | Partial (generic Xtensa, not LX6) | **Full** (dual-core SMP) | **Full** |
| **UART** | Via CH340 USB bridge | **ESP32_UART exists** | **Yes** | **Yes** |
| **WiFi (b/g/n)** | 802.11 b/g/n, soft-AP `ESP32-CAM-MB` | No | No (OpenCores ETH) | **Yes** |
| **BT 4.2 + BLE** | Classic + BLE | No | No | No |
| **Flash (4MB)** | SPI flash, mfg ID `0x68` | No | **Yes** | **Yes** |
| **Camera (OV2640)** | DVP camera interface | No | No | **Yes** (simulated) |
| **PSRAM** | Likely present (CAM boards) | No | **Yes** | **Yes** |
| **eFuse** | Vref calibration in eFuse | No | **Yes** | Partial |

Key challenge: The ESP32-CAM-MB uses the **Xtensa LX6** architecture. Renode's current Xtensa support is for a generic `sample_controller` configuration, not the ESP32's specific LX6 dual-core configuration. QEMU's Espressif fork has the full ESP32 Xtensa configuration and is the only open-source reference for this.

The camera interface (OV2640 via DVP/I2S) is not emulated in any open-source tool. Wokwi has simulated camera support but it's closed-source.

### ESP32 DevKit (ESP32-D0WDQ6) — Same Architecture as CAM-MB

| Aspect | Physical Hardware | Renode | QEMU (Espressif) | Wokwi |
|---|---|---|---|---|
| **CPU (Xtensa LX6)** | Dual-core, rev v1.0 (older silicon) | Partial | **Full** | **Full** |
| **UART** | Via CP2102 USB bridge | **ESP32_UART exists** | **Yes** | **Yes** |
| **WiFi (b/g/n)** | 802.11 b/g/n, soft-AP `ESP_1144E8` | No | No | **Yes** |
| **BT 4.2 + BLE** | Classic + BLE | No | No | No |
| **Flash (4MB)** | GigaDevice (mfg `0xc8`) | No | **Yes** | **Yes** |

Same emulation profile as the ESP32-CAM-MB minus the camera. Note: This is rev v1.0 (older silicon), while the CAM-MB is rev v3.1. For emulation purposes, the differences are minor (some errata fixes in later revisions).

### nRF52840 Dongle (PCA10059) — Already Well-Supported in Renode

| Aspect | Physical Hardware | Renode | QEMU | Wokwi |
|---|---|---|---|---|
| **CPU (Cortex-M4F)** | ARM Cortex-M4 with FPU | **Full** | Full (upstream) | No |
| **BLE 5.0** | BLE radio | **Yes** (NRF52840_Radio + BLEMedium) | No | No |
| **802.15.4** | Thread/Zigbee radio | **Yes** (via 802.15.4 medium) | No | No |
| **USB** | USB 2.0 Full Speed | Partial | No | No |
| **GPIO/peripherals** | Full nRF52840 peripheral set | **Extensive** | Partial | No |

`★ Insight ─────────────────────────────────────`

The nRF52840 is notable because **Renode already has excellent support for it**, including wireless simulation. This makes the nRF52840 dongle on the test station a **bridge technology**: it can be used today in Renode for BLE and 802.15.4 testing, and the wireless medium infrastructure it uses (`BLEMedium`, `IEEE802_15_4Medium`) is exactly what would need to be extended for ESP32 wireless emulation.

A practical near-term scenario: emulate the ESP32-C6's 802.15.4 radio in Renode, use the existing nRF52840 model as the other node, and run Thread networking between them -- all in simulation. The physical nRF52840 dongle can validate the simulation against real hardware.

`─────────────────────────────────────────────────`

---

## 3. Wireless Testing Scenarios

The rpi4-esp station has a unique capability: a dedicated WiFi adapter (RTL8188CUS on `wlanE`) that can act as either an AP or client for the ESP boards. This creates several hardware-validated test scenarios:

### Scenario 1: ESP32-C3 WiFi Client → wlanE AP

```
[ESP32-C3] --WiFi--> [wlanE (hostapd AP)] --eth→ [network]
```

- **Physical:** ESP32-C3 connects to `esp-test` AP on wlanE, sends UDP/TCP packets
- **Emulation gap:** No emulator has real WiFi. QEMU uses virtual Ethernet. Renode has nothing.
- **Emulation approach:** In Renode, replace WiFi with virtual Ethernet (like QEMU). Test TCP/IP stack, not WiFi-specific APIs.

### Scenario 2: ESP32 Soft-AP → wlanE Client

```
[wlanE (client)] --WiFi--> [ESP32 DevKit (soft-AP "ESP_1144E8")]
```

- **Physical:** RTL8188CUS connects to ESP32's soft-AP network
- **Emulation gap:** Soft-AP mode requires WiFi MAC emulation. esp32-open-mac has achieved this (AP mode works), but no emulator supports it.

### Scenario 3: ESP32-C3 BLE → nRF52840 BLE

```
[ESP32-C3 (BLE peripheral)] <--BLE--> [nRF52840 (BLE central)]
```

- **Physical:** ESP32-C3 advertises BLE service, nRF52840 connects to it
- **Emulation gap:** Renode has nRF52840 BLE but not ESP32 BLE
- **Emulation approach:** Implement ESP32 BLE controller as VHCI endpoint, connect via Renode's BLEMedium to nRF52840_Radio model. This is the most tractable multi-chip wireless scenario.

### Scenario 4: ESP32-C6/H2 Thread ↔ nRF52840 Thread (Future)

```
[ESP32-C6 (Thread router)] <--802.15.4--> [nRF52840 (Thread end device)]
```

- **Not yet on rpi4-esp** (no ESP32-C6/H2 board), but adding one would enable:
- **Emulation opportunity:** ESP32-C6's 802.15.4 radio is fully documented. Combined with Renode's existing nRF52840 802.15.4 support and `IEEE802_15_4Medium`, this is the **most immediately achievable** multi-chip wireless emulation scenario.

---

## 4. Recommended Test Station Additions

Based on the emulation research, these additions to the rpi4-esp station would maximize its value for emulation validation:

| Priority | Board | Why |
|---|---|---|
| **High** | ESP32-C6 dev board | 802.15.4 radio is fully documented; can validate Thread emulation against nRF52840 dongle |
| **High** | ESP32-H2 dev board | Pure 802.15.4 + BLE device; ideal for Thread border router testing |
| Medium | ESP32-S3 dev board | Most popular for AI/HMI; QEMU has full support; Xtensa LX7 |
| Low | ESP32-P4 dev board | Newest, highest performance; no wireless (needs companion chip) |

---

## 5. Emulation Development Workflow Using rpi4-esp

The test station enables a **hardware-in-the-loop validation workflow** for emulation development:

```
┌─────────────────────────────────────────────────┐
│                Development Machine               │
│  ┌───────────┐    ┌────────────────────────┐    │
│  │  Renode   │    │  ESP-IDF Build System   │    │
│  │ Emulator  │    │  (builds firmware.bin)  │    │
│  └─────┬─────┘    └───────────┬────────────┘    │
│        │                      │                  │
│   Run in Renode          Upload via SSH           │
│   Compare output         to rpi4-esp              │
│        │                      │                  │
└────────┼──────────────────────┼──────────────────┘
         │                      │
         ▼                      ▼
┌─────────────┐    ┌─────────────────────────────────┐
│  Emulated   │    │         rpi4-esp (RPi4)          │
│  ESP32-C3   │    │  ┌─────────┐  ┌──────────────┐  │
│  (Renode)   │    │  │ esptool │→ │  ESP32-C3    │  │
│             │    │  │  flash  │  │  (physical)  │  │
│ UART output │    │  └─────────┘  └──────┬───────┘  │
│      ↓      │    │                      │          │
│  [compare]  │◄───│──── UART output ─────┘          │
│             │    │                                  │
└─────────────┘    └──────────────────────────────────┘
```

Steps:
1. Build ESP-IDF firmware for ESP32-C3
2. Flash to physical ESP32-C3 on rpi4-esp via `esptool --port /dev/ttyESP32C3`
3. Capture UART output from physical board
4. Run same firmware binary in Renode ESP32-C3 emulation
5. Compare UART outputs -- differences reveal emulation gaps
6. Fix emulation, repeat

This workflow is particularly powerful because:
- The ESP32-C3 has USB-JTAG, enabling **real-time register inspection** on hardware to validate emulator register behavior
- Multiple boards on the same station allow testing inter-chip communication scenarios
- Remote SSH access means this can be integrated into CI/CD pipelines

---

## 6. Summary: What Can Be Emulated Today

| Board on rpi4-esp | Can run in Renode today? | Can run in QEMU today? | Can run in Wokwi today? |
|---|---|---|---|
| **ESP32-C3** | No (CPU works, no peripherals) | **Yes** (boot ESP-IDF, UART, flash, crypto, timers) | **Yes** (most features + WiFi) |
| **ESP32-CAM-MB** (D0WD-V3) | No (Xtensa not ESP32-configured) | **Yes** (boot ESP-IDF, no camera) | **Yes** (incl. simulated camera) |
| **ESP32 DevKit** (D0WDQ6) | No (same as above) | **Yes** | **Yes** |
| **nRF52840 dongle** | **Yes** (extensive support + BLE + 802.15.4) | Partial | No |

**The nRF52840 is the only board on the test station that works in Renode today.** The ESP32-C3 is the closest to working -- it only needs peripheral models, not CPU architecture work.
