# Gap Analysis and Roadmap

Consolidated analysis of emulation gaps, prioritised development path, wireless emulation strategies, and phased implementation plan.

> **See also:**
> - [Emulation Platform Status](02-emulation-platform-status.md) — what works today
> - [Wireless Hardware Documentation](03-wireless-hardware-documentation.md) — register docs informing peripheral models
> - [Development Methodology](04-development-methodology.md) — how to validate emulation against hardware
> - [Test Station Hardware](06-test-station-hardware.md) — available hardware for testing
> - [Document Index](README.md)

---

## 1. Gap Analysis: Peripheral Coverage

### Critical Gaps for Renode ESP Emulation

#### Tier 1: Must-Have for Basic Firmware Execution

These are required to boot ESP-IDF firmware and run basic applications:

1. **ESP32 platform definition (.repl)** - Memory map, peripheral register addresses, interrupt routing
2. **Interrupt Matrix / Controller** - ESP32's custom interrupt routing (not standard Xtensa INTC)
3. **Timer Groups** - Used by FreeRTOS tick and ESP-IDF system timer
4. **System Timer** (for C-series/H-series) - Primary system time source
5. **Flash SPI + MMU** - Execute-in-place from flash, critical for ESP-IDF
6. **Cache controller** - Instruction and data cache management
7. **RTC Control** - Clock configuration, reset reasons, boot mode
8. **DPORT / System Registers** - System configuration, peripheral clock gating
9. **Clock control** - PLL, CPU frequency, peripheral clocks
10. **GPIO (basic)** - Pin muxing, boot strapping pins

#### Tier 2: Required for Common Application Development

11. **SPI Master/Slave** - External device communication
12. **I2C Master/Slave** - Sensor communication
13. **DMA (GDMA/PDMA)** - Required by many peripheral drivers
14. **eFuse** - MAC address, calibration data, secure boot config
15. **ADC** - Analog sensor reading
16. **LEDC / PWM** - LED control, motor control
17. **RMT** - IR remote, WS2812 LED strips (very popular in maker community)
18. **TWAI / CAN** - Automotive and industrial applications
19. **Watchdog Timers** - System reliability

#### Tier 3: Required for WiFi/Bluetooth (the stated goal)

20. **WiFi Radio Peripheral** - The ESP32 WiFi hardware is largely undocumented (proprietary blob). Options:
    - **Approach A:** Model the WiFi HAL interface (intercept at the ESP-IDF `esp_wifi` API layer)
    - **Approach B:** Model the WiFi peripheral registers (requires reverse engineering the binary WiFi blob)
    - **Approach C:** Use Renode's network abstraction (virtual Ethernet, like QEMU's OpenCores approach)
21. **Bluetooth/BLE Radio Peripheral** - Similarly proprietary. Options:
    - Leverage Renode's existing `BLEMedium` infrastructure
    - Model at the HCI (Host Controller Interface) level
    - Intercept at the ESP-IDF Bluetooth API layer
22. **IEEE 802.15.4 Radio** (for ESP32-C6, ESP32-H2) - Options:
    - Leverage Renode's existing `IEEE802_15_4Medium` infrastructure
    - Model at the 802.15.4 MAC layer
23. **Coexistence Manager** - WiFi/BT/802.15.4 radio time-sharing

#### Tier 4: Nice to Have

24. USB OTG / USB Serial/JTAG
25. I2S / Audio
26. Camera interface
27. LCD/Display interface
28. Touch sensor
29. Hardware crypto accelerators (AES, SHA, RSA, etc.)
30. Secure Boot chain
31. Flash Encryption
32. Deep sleep / power management
33. Temperature sensor
34. MCPWM
35. PCNT (Pulse Counter)

### Estimated Effort

| Work Item                 | Complexity  | Notes                                                    |
| ------------------------- | ----------- | -------------------------------------------------------- |
| ESP32-C3 platform (.repl) | Medium      | RISC-V core already works; need memory map + peripherals |
| ESP32 platform (.repl)    | High        | Xtensa config needs ESP32-specific tuning                |
| Interrupt matrix          | Medium-High | Custom to ESP32, well-documented in TRM                  |
| Timer groups + SysTimer   | Medium      | Well-documented, straightforward register model          |
| Flash SPI + MMU           | High        | Complex memory mapping, XIP                              |
| GPIO + pin mux            | Medium      | Register model, boot strapping                           |
| WiFi (HAL intercept)      | Very High   | Proprietary blob, would need API-level interception      |
| WiFi (virtual Ethernet)   | Medium      | Follow QEMU's OpenCores approach                         |
| BLE (HCI level)           | High        | Could leverage existing BLEMedium                        |
| 802.15.4                  | Medium-High | Could leverage existing IEEE802_15_4Medium               |

### Recommended Priority Path

**Phase 1: ESP32-C3 (lowest friction)** — see [Test Station Hardware](06-test-station-hardware.md) for the physical ESP32-C3 on rpi4-esp
- RISC-V core already works in Renode
- Use Espressif QEMU C3 source as reference for peripheral models
- Target: boot ESP-IDF "hello_world" with UART output

**Phase 2: ESP32 (highest demand)**
- Complete Xtensa LX6 ESP32-specific configuration
- Port key peripheral models from Phase 1 (many are shared)
- Target: boot ESP-IDF with FreeRTOS on dual-core

**Phase 3: Networking**
- Implement virtual Ethernet approach (like QEMU's OpenCores) for basic TCP/IP
- Implement BLE at HCI level using Renode's BLEMedium
- Implement 802.15.4 using Renode's IEEE802_15_4Medium
- Target: ESP-IDF networking stack operational

**Phase 4: ESP32-C6 + ESP32-S3**
- ESP32-C6: RISC-V + WiFi 6 + 802.15.4 (Matter-ready)
- ESP32-S3: Xtensa LX7 + AI extensions (HMI applications)

---

## 2. Strategic Recommendations

### Strategic Observations

1. **Start with RISC-V (ESP32-C3):** Renode already has strong RISC-V support. The ESP32-C3 is the easiest path to a working ESP platform in Renode. Espressif's QEMU fork provides excellent reference implementations for all peripheral register models.

2. **Leverage QEMU source as reference:** The Espressif QEMU fork is Apache-2.0 / GPLv2 licensed. While Renode peripheral models are written in C# (vs QEMU's C), the QEMU source provides authoritative register layouts, behaviour specifications, and test cases that can guide Renode implementation.

3. **WiFi is the hardest problem:** No open-source emulator has achieved true WiFi emulation for ESP32. The WiFi hardware is controlled by a proprietary binary blob. Realistic options are:
   - **Virtual Ethernet** (QEMU's approach): Replace WiFi with a virtual Ethernet MAC. Applications using TCP/IP work, but WiFi-specific APIs (scanning, AP mode, mesh, etc.) do not.
   - **API-level interception:** Hook ESP-IDF's `esp_wifi` API functions in the emulator to provide simulated responses. More complete but requires maintenance as ESP-IDF evolves.
   - **Wokwi's approach:** Wokwi has WiFi simulation, but their implementation is closed-source.

4. **BLE is more tractable:** The BLE stack in ESP-IDF uses a well-defined HCI interface between the host stack (NimBLE/Bluedroid) and the controller. Renode already has BLE medium simulation. An ESP BLE controller model that speaks HCI would enable BLE application development.

5. **802.15.4 (Thread/Zigbee) is the most tractable wireless:** The IEEE 802.15.4 radio interface is well-documented, and Renode already has 802.15.4 medium simulation with several radio models. This is the lowest-hanging fruit for wireless emulation on ESP32-C6 and ESP32-H2.

6. **No emulator is complete:** Even Wokwi (the most complete) lacks BLE and has limited crypto. This is an area where Renode could differentiate by being the first open-source platform with comprehensive ESP wireless emulation.

### Key Resources

- **Espressif Technical Reference Manuals:** [ESP32 TRM](https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf) (and similar for each chip)
- **Espressif QEMU fork:** [github.com/espressif/qemu](https://github.com/espressif/qemu) (branch `esp-develop`)
- **Renode Xtensa support:** [Antmicro blog](https://antmicro.com/blog/2022/01/xtensa-isa-in-renode-for-sof-project/)
- **Renode GitHub issues:** [#262 (ESP32 feature request)](https://github.com/renode/renode/issues/262), [#704 (ESP32 CPU status)](https://github.com/renode/renode/issues/704)
- **ESP-IDF source:** [github.com/espressif/esp-idf](https://github.com/espressif/esp-idf) (HAL layer shows register-level access patterns)
- **Renode infrastructure:** [github.com/renode/renode-infrastructure](https://github.com/renode/renode-infrastructure) (wireless medium classes)


---

## 3. Wireless Emulation Strategies

### Strategy by Wireless Technology

#### WiFi Emulation Strategies (in order of feasibility)

| Strategy                     | Complexity  | Fidelity    | Approach                                                                                                         |
| ---------------------------- | ----------- | ----------- | ---------------------------------------------------------------------------------------------------------------- |
| **Virtual Ethernet**         | Low         | Low         | Replace WiFi with fake Ethernet MAC (QEMU's approach). TCP/IP works, WiFi APIs don't.                            |
| **API-level interception**   | Medium      | Medium      | Hook `esp_wifi_*()` API functions in the emulator. Simulate scanning, association, etc.                          |
| **OS adapter interception**  | Medium      | Medium-High | Hook the `wifi_os_adapter` callback table. The blob calls these for OS services.                                 |
| **Register-level emulation** | High        | High        | Use esp32-open-mac's register knowledge to model the WiFi hardware. Most faithful but most effort.               |
| **Hybrid**                   | Medium-High | High        | Use esp32-open-mac's open-source WiFi driver instead of the blob, modeling only the hardware primitives it uses. |

**Recommended:** Start with Virtual Ethernet (quick win), then pursue the **Hybrid** approach -- using esp32-open-mac's open WiFi stack means the emulator only needs to model the hardware primitives (DMA, interrupts, filters) that the open stack uses, rather than reverse-engineering the entire register space that the proprietary blob accesses.

#### BLE Emulation Strategy

| Strategy                     | Complexity | Fidelity  |
| ---------------------------- | ---------- | --------- |
| **VHCI interception**        | Medium     | High      |
| **Register-level emulation** | Very High  | Very High |

**Recommended:** VHCI interception. Implement a virtual BLE controller that speaks standard HCI. The host stack (NimBLE/Bluedroid) communicates via VHCI, so intercepting at this level gives high fidelity without reverse engineering the radio hardware. Combined with Renode's existing `BLEMedium`, this could enable multi-node BLE simulation.

#### IEEE 802.15.4 Emulation Strategy

| Strategy                     | Complexity | Fidelity  |
| ---------------------------- | ---------- | --------- |
| **Register-level emulation** | Medium     | Very High |

**Recommended:** Direct register-level emulation. The hardware is fully documented in ESP-IDF. The register definitions in `ieee802154_reg.h` / `ieee802154_struct.h` can be directly translated into an emulator peripheral model. Combined with Renode's existing `IEEE802_15_4Medium`, this enables full Thread/Zigbee simulation.

### Priority Order for Emulation Development

1. **IEEE 802.15.4** -- Fully documented, Renode has medium simulation, highest ROI
2. **BLE via VHCI** -- Clean interface, Renode has BLE medium, no RE needed
3. **WiFi via Virtual Ethernet** -- Quick win for TCP/IP connectivity
4. **WiFi via hybrid (esp32-open-mac)** -- Higher fidelity, depends on open-mac project maturity

### Key Collaboration Opportunities

1. **esp32-open-mac project:** Their reverse-engineered register knowledge is directly applicable. Their modified QEMU fork logs register accesses that define what an emulator needs to model. NLnet/NGI0 funded.

2. **Tarlogic ESP32-Bluetooth-Reversing:** Their SVD patches at [TarlogicSecurity/esp-pacs](https://github.com/TarlogicSecurity/esp-pacs) provide machine-readable BT register definitions. Could be used to build a register-level BT peripheral model as an alternative to VHCI interception.

3. **Espressif's IEEE 802.15.4 source:** The complete register-level HAL can be used as a specification for emulator development.

4. **Renode's wireless infrastructure:** `BLEMedium` and `IEEE802_15_4Medium` provide the multi-node simulation backbone.

5. **esp-rs/esp-ieee802154:** Open-source Rust 802.15.4 driver ([github.com/esp-rs/esp-ieee802154](https://github.com/esp-rs/esp-ieee802154)) with raw frame TX/RX -- useful as additional test/reference code.


---

## 4. Practical Implementation Plan

### Phase 1: Establish the Comparison Infrastructure (Week 1-2)

See [Development Methodology](04-development-methodology.md) for detailed JTAG setup, Renode instrumentation commands, and quick-reference guides.

**On rpi4-esp:**
1. Install ESP-IDF on the RPi4 (or cross-compile on a dev machine and flash remotely)
2. Set up OpenOCD for ESP32-C3: `openocd -f board/esp32c3-builtin.cfg`
3. Build and flash `hello_world` to ESP32-C3
4. Capture UART output as baseline
5. Set up app-level tracing with gcov for code coverage on real hardware

**In Renode:**
1. Create a minimal ESP32-C3 platform file (.repl) with:
   - RV32IMC CPU
   - Memory map from ESP-IDF `reg_base.h`
   - ESP32_UART (already exists)
   - Python catch-all peripherals for all other ranges
2. Load the same `hello_world` binary
3. Enable full peripheral access logging
4. Compare UART output and identify first divergence point

### Phase 2: Iterative Register Modelling (Week 3-6)

For each divergence:
1. Identify the register address firmware is stuck on
2. Check ESP-IDF source for what value is expected (often a "ready" bit)
3. Update the Python stub to return the correct value
4. Verify against real hardware using JTAG watchpoint on that address
5. Repeat until `hello_world` boots and prints to UART in Renode

Expected order of register modelling needed (based on ESP-IDF boot flow):
1. **eFuse** (MAC address, chip revision) -- read during early boot
2. **RTC/clock control** -- PLL lock, CPU frequency setting
3. **Cache/MMU** -- Flash memory mapping for XIP
4. **System registers** -- Peripheral clock gating, reset control
5. **Timer groups** -- FreeRTOS tick timer
6. **Interrupt matrix** -- RISC-V PLIC/CLIC configuration

### Phase 3: Coverage-Guided Expansion (Week 7+)

1. Move from `hello_world` to `wifi_station` example
2. The WiFi init path will hit many more registers
3. Use the Fuzzware-style approach: fuzz register responses, keep values that advance boot progress
4. Cross-reference against esp32-open-mac QEMU trace logs for correct values
5. Graduate frequently-accessed registers to proper C# models

### Phase 4: Automated Comparison CI

Set up automated testing:
1. Build ESP-IDF example
2. Flash to ESP32-C3 on rpi4-esp, capture output
3. Run in Renode, capture output
4. Diff comparison
5. Report new unhandled accesses and divergence points

### Hardware Needed

| Item                   | Purpose                              | Priority | Est. Cost |
| ---------------------- | ------------------------------------ | -------- | --------- |
| **ESP-Prog**           | JTAG for ESP32 DevKit + CAM-MB       | High     | ~$15      |
| **ESP32-C6 dev board** | 802.15.4 testing + Thread validation | High     | ~$10      |
| **ESP32-H2 dev board** | Pure 802.15.4 + BLE device           | Medium   | ~$10      |
| **ESP32-S3 dev board** | QEMU-supported Xtensa target         | Low      | ~$10      |


---

## Appendix: Data Sources

This document was compiled from:
- Wikipedia ESP32 article (family comparison table, chip variant tables)
- Espressif official product pages (espressif.com/en/products/socs/*)
- Espressif QEMU fork README (github.com/espressif/esp-toolchain-docs/blob/main/qemu/README.md)
- Espressif QEMU source tree (github.com/espressif/qemu, esp-develop branch)
- Renode documentation (renode.readthedocs.io)
- Renode source tree (github.com/renode/renode, github.com/renode/renode-infrastructure)
- Renode GitHub issues #262, #704
- Antmicro blog: "Adding Xtensa ISA support in Renode for the Sound Open Firmware project" (2022-01-28)
- Wokwi documentation (docs.wokwi.com/guides/esp32)
