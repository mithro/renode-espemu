# ESP32 WiFi/BLE Hardware: Documentation and Reverse Engineering Status

**Date:** 2026-03-28
**Purpose:** Catalogue all existing documentation, reverse engineering efforts, and register-level information for ESP32 wireless hardware (WiFi, Bluetooth/BLE, IEEE 802.15.4) to inform emulation development.

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [WiFi Hardware Architecture](#2-wifi-hardware-architecture)
3. [The esp32-open-mac Project](#3-the-esp32-open-mac-project)
4. [Espressif Official Documentation (WiFi/BT)](#4-espressif-official-documentation-wifibt)
5. [ESP-IDF Binary Blob Structure](#5-esp-idf-binary-blob-structure)
6. [Known WiFi Register Map](#6-known-wifi-register-map)
7. [BLE Controller Interface](#7-ble-controller-interface)
8. [IEEE 802.15.4 Radio (Thread/Zigbee)](#8-ieee-802154-radio-threadzigbee)
9. [Other Relevant Projects](#9-other-relevant-projects)
10. [Security Research and CVEs](#10-security-research-and-cves)
11. [Implications for Emulation](#11-implications-for-emulation)
12. [Key Resources Index](#12-key-resources-index)

---

## 1. Executive Summary

The openness of ESP32 wireless hardware documentation varies **dramatically** by technology:

| Technology | Documentation Level | Source |
|---|---|---|
| **IEEE 802.15.4** (Thread/Zigbee) | **Fully documented** -- complete register maps in ESP-IDF | Espressif (open source) |
| **WiFi MAC/PHY** | **Partially reverse-engineered** -- active open-source effort | esp32-open-mac project |
| **Bluetooth/BLE controller** | **Opaque** -- binary blob with VHCI interface | Espressif (closed source) |
| **RF PHY calibration** | **Opaque** -- binary blob | Espressif (closed source) |

The critical finding is that **IEEE 802.15.4 is the lowest-hanging fruit** for emulation -- Espressif publishes complete register-level documentation. WiFi is being actively reverse-engineered by the esp32-open-mac project, which has achieved functional TX/RX/association. BLE remains the most opaque.

---

## 2. WiFi Hardware Architecture

### SoftMAC Design

The ESP32 uses a **SoftMAC** architecture, meaning:
- **Hardware handles:** PHY layer (modulation/demodulation), FCS (frame check sequence) computation, automatic ACK generation, MAC address filtering, DMA for packet transfer
- **Software handles:** All MAC layer logic -- association, authentication, scanning, channel management, rate adaptation, power saving, WPA handshakes, packet queuing

This is confirmed by the esp32-open-mac reverse engineering work. It means that an emulator doesn't need to model the full WiFi protocol in hardware -- only the hardware primitives (DMA, filtering, PHY config) that the software MAC layer uses.

### Hardware Components (from reverse engineering)

Based on the esp32-open-mac project's findings, the WiFi hardware consists of:

1. **Baseband (BB)** -- Digital signal processing for WiFi modulation/demodulation
   - Registers at `DR_REG_BB_BASE`
   - Controls FFT engine, DC estimation, power up/down
   - Only a few register bits are documented in ESP-IDF (`bb_reg.h`)

2. **RF Frontend (FE)** -- Analog radio frequency components
   - Registers at `DR_REG_FE_BASE` and `DR_REG_FE2_BASE`
   - Controls IQ estimation, TX interpolation, power up/down
   - Only a few register bits are documented in ESP-IDF (`fe_reg.h`)

3. **MAC Hardware** -- Packet processing and DMA engine
   - DMA descriptor rings for TX and RX
   - Hardware MAC address filtering (RX policy)
   - Automatic ACK generation
   - Interrupt generation on packet events

4. **PLL/Clock** -- Frequency synthesis for channel selection
   - BBPLL registers accessible via I2C-like interface (`regi2c_bbpll.h`)
   - Used for WiFi channel selection and frequency tuning

### Packet Lifecycle (from reverse engineering)

From the Zeus/UGent blog documenting the esp32-open-mac RE process:

**Initialization:**
1. `esp_wifi_start()` -> `esp_phy_enable()` (blob)
2. PHY calibration: I/Q phase matching, antenna matching, carrier leakage compensation, baseband/PA/RF nonlinearity compensation
3. MAC peripheral init: set RX MAC filters, set DMA buffers, set auto-ACK policy, set chip MAC address
4. Set radio properties: TX rate, frequency, TX power
5. Set up power management timer (modem sleep)

**Transmitting a packet:**
1. Wake WiFi peripherals from sleep if needed
2. Prepare DMA descriptor with packet data and metadata (rate, length, etc.)
3. Write descriptor address to hardware TX register
4. Hardware performs: carrier sense, backoff, modulation, transmission
5. Hardware generates interrupt on completion
6. If ACK required, hardware waits for ACK and reports status

**Receiving a packet:**
1. Hardware continuously monitors the channel
2. On valid frame detection, hardware writes packet to DMA buffer via descriptor
3. Hardware checks MAC address filter (RX policy)
4. Hardware computes and checks FCS
5. Hardware generates interrupt
6. Software reads packet from DMA buffer
7. If packet requires ACK, hardware auto-sends ACK

---

## 3. The esp32-open-mac Project

### Overview

**[esp32-open-mac](https://github.com/esp32-open-mac/esp32-open-mac)** is the most significant reverse engineering effort for ESP32 WiFi hardware. Led by Jasper Devreker and collaborators from [Zeus WPI](https://zeus.ugent.be/) at Ghent University.

- **Repository:** https://github.com/esp32-open-mac/esp32-open-mac
- **Website:** https://esp32-open-mac.be/
- **Stars:** 819 (as of 2026-03-28)
- **License:** No formal license specified (check repo for updates)
- **Language:** C (hardware abstraction + init) + Rust (MAC stack)
- **Last updated:** 2026-03-26 (very active)
- **Matrix room:** #esp32-open-mac:matrix.org

### Two Implementations

1. **esp32-open-mac** (C + Rust): Reference implementation. Hardware abstraction and initialization in C, MAC stack in Rust. Uses FreeRTOS tasks.

2. **[Ferris-on-Air (FoA)](https://github.com/esp32-open-mac/FoA)**: Pure async Rust implementation built on Embassy. Uses esp-hal patches for WiFi peripheral support. No FreeRTOS dependency.

### Current Capabilities (from README and blog)

- [x] Sending WiFi frames
- [x] Receiving WiFi frames
- [x] Automatic ACK response
- [x] Connect to open access points + send UDP packets
- [x] Integration with lwIP stack via ESP-NETIF custom IO driver
- [x] Channel switching
- [x] Rate control
- [x] TX power adjustment
- [x] Hardware packet filtering (MAC address-based RX filter)
- [x] Association/authentication via open-source 802.11 MAC implementation
- [x] AP mode
- [x] Dual AP/client mode
- [x] Decoupled blobs from ESP-IDF version
- [x] Eliminated FreeRTOS dependency (2025-01-14)
- [x] WPA crypto hardware acceleration reverse engineered (2025-03-07)
- [x] Standards-compliant 802.11s mesh networking (2025-08-18, **first exclusive feature!**)
- [ ] Full hardware initialization without blobs (currently uses `esp_phy_enable()` blob for init)
- [ ] WPA2 network connection with hardware acceleration
- [ ] WPA3 Dragonfly handshake
- [ ] Bluetooth
- [ ] SVD documentation for reverse-engineered registers

### Reverse Engineering Methodology

Documented in the [Zeus WPI blog series](https://zeus.ugent.be/blog/23-24/open-source-esp32-wifi-mac/):

1. **Static analysis:** Disassembling the proprietary WiFi blobs (libnet80211.a, libpp.a) to understand function behavior and register accesses
2. **Dynamic tracing:** Using JTAG debugging on ESP32 to trace register reads/writes during WiFi operations
3. **Modified QEMU:** A [custom QEMU fork](https://github.com/esp32-open-mac/qemu) (20 stars) instrumented to log all register accesses by the WiFi blob, enabling systematic mapping of the register space
4. **Faraday cage:** Built a [Faraday cage with data passthrough](https://esp32-open-mac.be/posts/0003-faraday-cage/) to isolate RF signals during testing
5. **Iterative experimentation:** Writing minimal firmware to exercise specific hardware functions, observing register state changes

### Chip Support

From the FAQ: "Since the project makes use of some proprietary blobs for initialization, we can only run on the chips for which Espressif has released these blobs. We can also only run on chips for which we have reversed enough to interact with the hardware. At the moment this includes: ESP32 and ESP32C2 (and by extension C3, C6)."

The project has noted that the WiFi hardware appears to be **similar across ESP32 variants**, though register addresses and some details differ.

### Blog Post Timeline

| Date | Title | Key Content |
|---|---|---|
| 2023-12-06 | [Unveiling secrets: creating an open-source MAC Layer](https://zeus.ugent.be/blog/23-24/open-source-esp32-wifi-mac/) | Initial RE methodology, sending first packet |
| 2023-12-07 | [Part 2: Continued RE](https://zeus.ugent.be/blog/23-24/esp32-reverse-engineering-continued/) | Receiving packets, DMA details |
| 2023-12-23 | [Building a Faraday cage](https://esp32-open-mac.be/posts/0003-faraday-cage/) | Test infrastructure |
| 2024-03-29 | [Connecting lwIP stack](https://esp32-open-mac.be/posts/0004-connecting-to-lwip/) | Full TCP/IP integration |
| 2024-05-24 | [The road ahead](https://esp32-open-mac.be/posts/0005-the-road-ahead/) | Future plans |
| 2024-05-30 | [Talk at GPN22](https://esp32-open-mac.be/posts/0006-talk-at-gpn22/) | Public presentation |
| 2024-08-06 | [Talk at RIOT Summit 2024](https://esp32-open-mac.be/posts/0007-talk-at-riot-summit/) | Public presentation |
| 2024-09-26 | [MAC RX filter](https://esp32-open-mac.be/posts/0008-rx-filter/) | Hardware filtering details |
| 2024-12-27 | [Talk at 38C3](https://esp32-open-mac.be/posts/0009-talk-at-38c3/) | "Liberating Wi-Fi on the ESP32" |
| 2025-01-14 | [Eliminating FreeRTOS dependency](https://esp32-open-mac.be/posts/0010-no-more-freertos-dependency/) | Architectural improvement |
| 2025-03-07 | [RE WPA crypto acceleration](https://esp32-open-mac.be/posts/0010-wpa/) | Hardware crypto for WPA |
| 2025-08-18 | [Standards-compliant meshing](https://esp32-open-mac.be/posts/0011-mesh-networking/) | First exclusive open-source feature |
| 2025-11-24 | [Year in review: 2025](https://esp32-open-mac.be/posts/0011-year-in-review/) | Progress summary |

---

## 4. Espressif Official Documentation (WiFi/BT)

### What IS Documented (in Technical Reference Manuals)

The ESP32 Technical Reference Manual (TRM) documents the following wireless-adjacent hardware, but **NOT the WiFi/BT MAC or PHY registers:**

- **Clock and reset configuration** for the WiFi/BT modules (how to power up/down)
- **DMA controller** (general PDMA/GDMA) -- but not WiFi-specific DMA descriptors
- **Interrupt routing** -- WiFi and BT interrupt sources are listed but not the internal interrupt logic
- **Memory map** -- base addresses for BB, FE, and WiFi/BT peripherals are listed
- **Coexistence** -- brief mention that WiFi/BT share the antenna via time-division

### What is NOT Documented

- WiFi MAC register map
- WiFi PHY/baseband control registers (beyond a few power bits)
- Bluetooth baseband registers
- BLE link layer registers
- DMA descriptor format for WiFi TX/RX
- Packet buffer management
- Rate/channel/power control registers
- Auto-ACK configuration registers
- RX filter (MAC address matching) configuration
- WiFi interrupt source details
- Coexistence arbitration registers

### Register Header Files in ESP-IDF

The following header files provide **minimal** register definitions for radio hardware:

| File | Content | Chips | Completeness |
|---|---|---|---|
| `soc/*/bb_reg.h` | Baseband power up/down bits | ESP32, S2, S3, C2, C3 | **Minimal** -- only PD/PU control for FFT, DC_EST |
| `soc/*/fe_reg.h` | RF frontend power up/down bits | ESP32, S2, S3, C2, C3 | **Minimal** -- only PD/PU for IQ_EST, TX_INF |
| `soc/*/regi2c_bbpll.h` | BBPLL I2C register configuration | All chips | Moderate -- PLL tuning params |
| `soc/*/ieee802154_reg.h` | IEEE 802.15.4 registers | C5, C6, H2, H4 | **Complete** |
| `soc/*/ieee802154_struct.h` | IEEE 802.15.4 C structs | C5, C6, H2, H4 | **Complete** |

**Key observation:** `bb_reg.h` and `fe_reg.h` only contain ~5-10 register fields each, all related to power management. The vast majority of WiFi hardware registers are undocumented. In contrast, the IEEE 802.15.4 registers are comprehensively documented.

---

## 5. ESP-IDF Binary Blob Structure

### WiFi Blobs

The WiFi functionality relies on precompiled binary libraries distributed in two places:

1. **[espressif/esp32-wifi-lib](https://github.com/espressif/esp32-wifi-lib)** (197 stars) -- WiFi stack precompiled libraries
   - Contains: `libcore.a`, `libespnow.a`, `libmesh.a`, `libnet80211.a`, `libpp.a`, `libsmartconfig.a`, `libwapi.a`
   - `libnet80211.a` -- The WiFi MAC layer (the main blob)
   - `libpp.a` -- "PP" (packet processing) layer, interfaces between MAC and hardware

2. **ESP-IDF `components/esp_phy/`** -- PHY layer libraries
   - `libphy.a` -- PHY initialization and calibration
   - `librtc.a` -- RTC (real-time clock) calibration (ESP32)
   - `libbtbb.a` -- Bluetooth baseband (ESP32-C2, C3, C6, H2, etc.)

### Open Source WiFi Components in ESP-IDF

The following parts of the WiFi stack ARE open source:

| Component | Path | Description |
|---|---|---|
| WiFi API | `components/esp_wifi/include/esp_wifi.h` | Public WiFi API |
| OS adapter | `components/esp_wifi/*/esp_adapter.c` | OS abstraction layer between ESP-IDF and WiFi blob |
| Private API | `components/esp_wifi/include/esp_private/wifi.h` | Internal WiFi API (called by the blob) |
| OS adapter interface | `components/esp_wifi/include/esp_private/wifi_os_adapter.h` | Function table the blob calls back into |
| WPA supplicant | `components/wpa_supplicant/` | Full WPA2/WPA3 supplicant (open source) |
| Network interface | `components/esp_netif/` | Network interface layer |

### The Blob Boundary

The interface between open-source ESP-IDF and the WiFi blob is the **OS adapter** pattern:

```
[User Application]
       |
[esp_wifi API]  (open source)
       |
[wifi_os_adapter]  (open source - function table)
       |
[libnet80211.a + libpp.a]  (BLOB - MAC layer)
       |
[libphy.a]  (BLOB - PHY init/calibration)
       |
[WiFi Hardware Registers]  (undocumented)
```

The `wifi_os_adapter.h` defines the callback table that the blob uses to interact with the RTOS (memory allocation, task creation, mutexes, timers, etc.). This is well-documented and stable.

### Bluetooth Blobs

| Library | Description | Chips |
|---|---|---|
| `libbtdm_app.a` | Bluetooth dual-mode (Classic+BLE) controller | ESP32 |
| `libbtbb.a` | Bluetooth baseband | ESP32-C2, C3, C6, H2 |
| `libbt.a` | BLE controller | Various |

---

## 6. Known WiFi Register Map

### From ESP-IDF Headers (Official, Minimal)

**Baseband registers** (`DR_REG_BB_BASE`):

| Offset | Register | Known Fields |
|---|---|---|
| 0x0054 | BBPD_CTRL | BB_FFT_FORCE_PU/PD, BB_DC_EST_FORCE_PU/PD |

**RF Frontend registers** (`DR_REG_FE_BASE`, `DR_REG_FE2_BASE`):

| Offset | Register | Known Fields |
|---|---|---|
| FE+0x0090 | FE_GEN_CTRL | FE_IQ_EST_FORCE_PU/PD |
| FE2+0x00F0 | FE2_TX_INTERP_CTRL | FE2_TX_INF_FORCE_PU/PD |

### From esp32-open-mac Reverse Engineering (Extensive, Growing)

The esp32-open-mac project has reverse-engineered significantly more registers, though a complete SVD file has not yet been published (it's on their roadmap). Key discovered functionality includes:

- **TX DMA descriptor registers** -- Format for passing packets to hardware
- **RX DMA descriptor registers** -- Format for receiving packets from hardware
- **TX trigger register** -- Writing here starts transmission
- **RX buffer configuration** -- Setting DMA buffer addresses for reception
- **MAC address filter registers** -- Configuring hardware-level RX filtering
- **Channel configuration** -- Setting the WiFi channel
- **Rate configuration** -- Setting the modulation/coding scheme
- **TX power configuration** -- Adjusting transmit power
- **Interrupt status/enable/clear** -- WiFi interrupt management
- **ACK policy registers** -- Configuring automatic acknowledgement
- **WPA crypto acceleration registers** -- Hardware AES for WPA (documented 2025-03-07)

The most authoritative source for current register knowledge is the [esp32-open-mac source code](https://github.com/esp32-open-mac/esp32-open-mac) and the [FoA (Ferris-on-Air)](https://github.com/esp32-open-mac/FoA) implementation, which contain register-level access in their hardware abstraction layers.

### esp32-open-mac QEMU Fork

The project maintains a [modified QEMU fork](https://github.com/esp32-open-mac/qemu) (20 stars) specifically instrumented for reverse engineering. This fork logs register accesses by the proprietary WiFi blob, enabling systematic identification of all registers the blob uses. This is potentially **directly useful for emulation work** -- the logged register accesses show exactly what an emulator would need to model.

---

## 7. BLE Controller Interface

### Architecture

The ESP32 BLE stack has two main components:

1. **Host stack** (open source): Either Bluedroid or NimBLE, running in the application CPU
2. **Controller** (binary blob): Running either on the same CPU or on a dedicated core, handling the BLE link layer and PHY

### VHCI (Virtual HCI) Interface

The host communicates with the controller via a Virtual HCI (Host Controller Interface):

```
[Application]
      |
[NimBLE / Bluedroid Host]  (open source)
      |
[VHCI Transport]  (open source)
      |
[BLE Controller Blob]  (libbtdm_app.a / libbt.a)
      |
[BLE Radio Hardware]  (undocumented)
```

Key ESP-IDF functions:
- `esp_bt_controller_init()` -- Initialize the BLE controller
- `esp_vhci_host_register_callback()` -- Register host-to-controller callbacks
- `esp_vhci_host_send_packet()` -- Send HCI packet from host to controller
- `esp_vhci_host_check_send_avail()` -- Check if controller can accept packets

### Emulation Implications

The VHCI interface is **standard Bluetooth HCI** (Host Controller Interface), meaning:
- The host stack (NimBLE/Bluedroid) sends standard HCI commands, ACL data, and SCO data
- The controller responds with standard HCI events and data
- **An emulator could implement a virtual BLE controller that speaks HCI** without needing to model the actual BLE radio hardware
- This would allow the host stack to function normally while the emulator provides simulated BLE peers

This is a significantly more tractable approach than trying to reverse-engineer the BLE radio registers.

### What IS Open Source (BLE)

- NimBLE host stack: fully open source (Apache 2.0)
- Bluedroid host stack: open source (in ESP-IDF)
- VHCI transport layer: open source
- BLE Mesh: open source

### What is NOT Open Source (BLE)

- BLE link layer controller
- BLE PHY driver
- BLE baseband registers
- Bluetooth Classic controller (ESP32 only)
- Coexistence arbiter (WiFi/BT radio sharing)

### No Known BLE Reverse Engineering

Unlike WiFi, there is **no known public reverse engineering effort** targeting the ESP32 BLE controller hardware. The esp32-open-mac project lists Bluetooth as a future goal but has not started work on it.

---

## 8. IEEE 802.15.4 Radio (Thread/Zigbee)

### Fully Documented and Open Source

Unlike WiFi and Bluetooth, the IEEE 802.15.4 radio hardware on ESP32-C5, ESP32-C6, ESP32-H2, and ESP32-H4 is **comprehensively documented** in ESP-IDF with complete register definitions and an open-source HAL driver.

### Source Files

| File | Description |
|---|---|
| `components/soc/esp32c6/register/soc/ieee802154_reg.h` | Complete register map with bit field definitions |
| `components/soc/esp32c6/register/soc/ieee802154_struct.h` | C struct overlay for register access |
| `components/esp_hal_ieee802154/esp32c6/include/hal/ieee802154_ll.h` | Low-level HAL (register read/write functions) |
| `components/esp_hal_ieee802154/include/hal/ieee802154_common_ll.h` | Common LL functions across chips |
| `components/esp_hal_ieee802154/esp32c6/ieee802154_periph.c` | Peripheral configuration (interrupts, etc.) |

Equivalent files exist for ESP32-C5, ESP32-H2, and ESP32-H4.

### Register Documentation Quality

The `ieee802154_reg.h` files contain **complete register-level documentation**, including:
- All register offsets
- All bit field definitions with widths and positions
- Read/write permissions
- Reset values

The `ieee802154_struct.h` files provide C struct overlays that make register access straightforward.

The `ieee802154_ll.h` files show exactly how the driver configures the radio: setting channel, TX power, PAN ID, addresses, enabling/disabling features, handling interrupts, etc.

### OpenThread Integration

[OpenThread](https://openthread.io/) runs on ESP32-C6/H2 using this open-source radio driver:
- `esp-idf/components/openthread/` -- OpenThread integration
- The Radio Abstraction Layer (RAL) is fully open source
- Provides a clean, documented interface for radio operations

### Emulation Implications

Because the 802.15.4 radio is fully documented at the register level:
1. An emulator can implement a faithful hardware model directly from the register definitions
2. The open-source HAL driver serves as a test oracle
3. Combined with Renode's existing `IEEE802_15_4Medium`, this enables **full Thread/Zigbee simulation** for ESP32-C6 and ESP32-H2
4. This is the single most tractable wireless emulation target for ESP32

---

## 9. Other Relevant Projects

### esp-rs/esp-wifi-sys (435 stars)

- **URL:** https://github.com/esp-rs/esp-wifi-sys
- WiFi and BT drivers packaged for bare-metal Rust (no_std)
- Still uses the proprietary blobs internally
- Provides a useful Rust abstraction over the blob interface
- Shows the complete function signature interface with the blobs

### espressif/esp-hosted (965 stars)

- **URL:** https://github.com/espressif/esp-hosted
- Uses ESP32 as a WiFi/BT co-processor for Linux or MCU hosts
- Communicates over SPI, SDIO, or UART
- Defines a clean protocol between host and ESP for WiFi/BT operations
- **Emulation relevance:** The esp-hosted protocol could serve as an emulation interface -- an emulator could implement the host side of the protocol

### espressif/esp32-wifi-lib (197 stars)

- **URL:** https://github.com/espressif/esp32-wifi-lib
- The official repository for precompiled WiFi libraries
- Has an [open issue since 2016](https://github.com/espressif/esp32-wifi-lib/issues/2) requesting open-sourcing the MAC layer
- Espressif confirmed in 2016 that open-sourcing the upper MAC was "on their roadmap" -- as of 2026, it has not happened

### esp-hal (Espressif HAL for Rust)

- The esp32-open-mac project submitted **patches to esp-hal** to add WiFi peripheral support
- The FoA implementation builds on these patches
- Shows increasing upstream acceptance of open WiFi hardware access

### JTAG Debugging Setup

- https://github.com/amirgon/ESP32-JTAG -- Setting up JTAG debugging on ESP32
- Useful for register-level debugging and reverse engineering

---

## 10. Security Research and CVEs

Security researchers have analyzed ESP32 wireless internals, sometimes revealing register-level details:

### Known Security Research

1. **ESPRESSIF-SA advisories:** Espressif publishes security advisories that occasionally reference internal wireless behavior
2. **Bluetooth impersonation attacks:** Research into BLE pairing vulnerabilities has revealed some controller behavior
3. **WiFi deauth/injection:** Research into frame injection on ESP32 (enabled by promiscuous mode) has documented some WiFi hardware behavior
4. **[38C3 talk (2024-12-27)](https://esp32-open-mac.be/posts/0009-talk-at-38c3/):** "Liberating Wi-Fi on the ESP32" -- Comprehensive presentation on WiFi hardware reverse engineering

### ESP32 Bluetooth Backdoor (2025)

In early 2025, security researchers from Tarlogic Security disclosed undocumented HCI commands in the ESP32 Bluetooth controller that could be used for memory read/write operations on the chip. While Espressif characterized these as debug commands rather than a backdoor, this research:
- Confirmed the existence of vendor-specific HCI commands
- Showed that the BLE controller has direct memory access capabilities
- Demonstrated that HCI-level analysis can reveal controller internals

---

## 11. Implications for Emulation

### Strategy by Wireless Technology

#### WiFi Emulation Strategies (in order of feasibility)

| Strategy | Complexity | Fidelity | Approach |
|---|---|---|---|
| **Virtual Ethernet** | Low | Low | Replace WiFi with fake Ethernet MAC (QEMU's approach). TCP/IP works, WiFi APIs don't. |
| **API-level interception** | Medium | Medium | Hook `esp_wifi_*()` API functions in the emulator. Simulate scanning, association, etc. |
| **OS adapter interception** | Medium | Medium-High | Hook the `wifi_os_adapter` callback table. The blob calls these for OS services. |
| **Register-level emulation** | High | High | Use esp32-open-mac's register knowledge to model the WiFi hardware. Most faithful but most effort. |
| **Hybrid** | Medium-High | High | Use esp32-open-mac's open-source WiFi driver instead of the blob, modeling only the hardware primitives it uses. |

**Recommended:** Start with Virtual Ethernet (quick win), then pursue the **Hybrid** approach -- using esp32-open-mac's open WiFi stack means the emulator only needs to model the hardware primitives (DMA, interrupts, filters) that the open stack uses, rather than reverse-engineering the entire register space that the proprietary blob accesses.

#### BLE Emulation Strategy

| Strategy | Complexity | Fidelity |
|---|---|---|
| **VHCI interception** | Medium | High |
| **Register-level emulation** | Very High | Very High |

**Recommended:** VHCI interception. Implement a virtual BLE controller that speaks standard HCI. The host stack (NimBLE/Bluedroid) communicates via VHCI, so intercepting at this level gives high fidelity without reverse engineering the radio hardware. Combined with Renode's existing `BLEMedium`, this could enable multi-node BLE simulation.

#### IEEE 802.15.4 Emulation Strategy

| Strategy | Complexity | Fidelity |
|---|---|---|
| **Register-level emulation** | Medium | Very High |

**Recommended:** Direct register-level emulation. The hardware is fully documented in ESP-IDF. The register definitions in `ieee802154_reg.h` / `ieee802154_struct.h` can be directly translated into an emulator peripheral model. Combined with Renode's existing `IEEE802_15_4Medium`, this enables full Thread/Zigbee simulation.

### Priority Order for Emulation Development

1. **IEEE 802.15.4** -- Fully documented, Renode has medium simulation, highest ROI
2. **BLE via VHCI** -- Clean interface, Renode has BLE medium, no RE needed
3. **WiFi via Virtual Ethernet** -- Quick win for TCP/IP connectivity
4. **WiFi via hybrid (esp32-open-mac)** -- Higher fidelity, depends on open-mac project maturity

### Key Collaboration Opportunities

1. **esp32-open-mac project:** Their reverse-engineered register knowledge is directly applicable. Their modified QEMU fork logs register accesses that define what an emulator needs to model.

2. **Espressif's IEEE 802.15.4 source:** The complete register-level HAL can be used as a specification for emulator development.

3. **Renode's wireless infrastructure:** `BLEMedium` and `IEEE802_15_4Medium` provide the multi-node simulation backbone.

---

## 12. Key Resources Index

### Primary Sources

| Resource | URL | Relevance |
|---|---|---|
| esp32-open-mac | https://github.com/esp32-open-mac/esp32-open-mac | WiFi register RE, open WiFi stack |
| esp32-open-mac website | https://esp32-open-mac.be/ | Blog posts documenting RE process |
| esp32-open-mac QEMU fork | https://github.com/esp32-open-mac/qemu | Instrumented QEMU for register logging |
| Ferris-on-Air (FoA) | https://github.com/esp32-open-mac/FoA | Pure Rust WiFi implementation |
| Zeus WPI blog (Part 1) | https://zeus.ugent.be/blog/23-24/open-source-esp32-wifi-mac/ | Detailed RE methodology |
| Zeus WPI blog (Part 2) | https://zeus.ugent.be/blog/23-24/esp32-reverse-engineering-continued/ | Continued RE details |
| ESP-IDF source | https://github.com/espressif/esp-idf | Official SDK, HAL, register headers |
| esp32-wifi-lib | https://github.com/espressif/esp32-wifi-lib | WiFi binary blob repository |
| esp-hosted | https://github.com/espressif/esp-hosted | WiFi co-processor protocol |
| esp-wifi-sys (Rust) | https://github.com/esp-rs/esp-wifi-sys | Rust WiFi/BT blob bindings |

### Register Documentation Sources

| File Path (in ESP-IDF) | Content |
|---|---|
| `components/soc/esp32/include/soc/bb_reg.h` | Baseband power registers |
| `components/soc/esp32/include/soc/fe_reg.h` | RF frontend power registers |
| `components/soc/*/regi2c_bbpll.h` | PLL configuration |
| `components/soc/esp32c6/register/soc/ieee802154_reg.h` | 802.15.4 full register map |
| `components/soc/esp32c6/register/soc/ieee802154_struct.h` | 802.15.4 register structs |
| `components/esp_hal_ieee802154/*/include/hal/ieee802154_ll.h` | 802.15.4 low-level HAL |

### Talks and Presentations

| Date | Title | Event |
|---|---|---|
| 2024-05-30 | Reversing the ESP32 Wi-Fi hardware | Gulaschprogrammiernacht 22 (GPN22) |
| 2024-08-06 | Reverse engineering the ESP32 Wi-Fi hardware | RIOT Summit 2024 |
| 2024-12-27 | Liberating Wi-Fi on the ESP32 | 38th Chaos Communication Congress (38C3) |
