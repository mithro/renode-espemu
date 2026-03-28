# Development Methodology: JTAG, Tracing, and Coverage-Guided Emulation

How to use the rpi4-esp test station for hardware/emulation comparison, and how AFL-style approaches can progressively improve Renode's ESP32 peripheral emulation.

> **See also:**
> - [Test Station Hardware](06-test-station-hardware.md) — board inventory and per-board emulation status
> - [Emulation Platform Status](02-emulation-platform-status.md) — Renode's current capabilities
> - [Gap Analysis and Roadmap](05-gap-analysis-and-roadmap.md) — phased implementation plan
> - [Wireless Hardware Documentation](03-wireless-hardware-documentation.md) — register maps for peripheral modelling
> - [Document Index](README.md)

---

## 1. JTAG Capabilities on rpi4-esp Hardware

### ESP32-C3 Board -- Best JTAG Target (No External Hardware Needed)

The ESP32-C3 on the [rpi4-esp test station](06-test-station-hardware.md) has **built-in USB-JTAG/serial** via its USB interface. This is the most convenient debug target:

| Capability               | Status                                                |
| ------------------------ | ----------------------------------------------------- |
| **JTAG connection**      | Built-in via USB (Interface 2 = vendor-specific JTAG) |
| **OpenOCD config**       | `board/esp32c3-builtin.cfg`                           |
| **No external hardware** | Direct USB connection to `/dev/ttyESP32C3`            |
| **Hardware breakpoints** | 8 (RISC-V trigger module)                             |
| **Hardware watchpoints** | 8 (data access triggers)                              |
| **Single-stepping**      | Yes (instruction and source-level)                    |
| **GDB integration**      | Full (via OpenOCD GDB server)                         |
| **Flash programming**    | Via OpenOCD (`program_esp`)                           |
| **FreeRTOS awareness**   | Yes (thread enumeration, stack inspection)            |
| **Application tracing**  | Yes (via JTAG, UART, or USB simultaneously)           |
| **SystemView**           | Yes (SEGGER real-time OS analysis)                    |
| **Code coverage (gcov)** | Yes (via JTAG + esp_gcov component)                   |
| **Semihosting**          | Yes (file I/O, printf via debug channel)              |

To start debugging the ESP32-C3 from rpi4-esp:

```bash
# On rpi4-esp:
# Terminal 1: Start OpenOCD
openocd -f board/esp32c3-builtin.cfg

# Terminal 2: Connect GDB
riscv32-esp-elf-gdb -ex "target remote :3333" build/app.elf
```

### ESP32-CAM-MB and ESP32 DevKit -- External JTAG Required

The two ESP32 (Xtensa LX6) boards need external JTAG hardware:

| Board                  | JTAG Interface                                         | Additional Hardware Needed  |
| ---------------------- | ------------------------------------------------------ | --------------------------- |
| ESP32-CAM-MB (D0WD-V3) | GPIO12 (TDI), GPIO13 (TCK), GPIO14 (TMS), GPIO15 (TDO) | ESP-Prog or FT2232H adapter |
| ESP32 DevKit (D0WDQ6)  | Same GPIO pins                                         | ESP-Prog or FT2232H adapter |

**Important caveat:** On the ESP32-CAM-MB, GPIO12-15 are likely shared with the camera or SD card interface, making JTAG potentially conflicting with camera operation.

The ESP32 (Xtensa) has additional debug capabilities not available on RISC-V:

| Capability               | Status                                                               |
| ------------------------ | -------------------------------------------------------------------- |
| **TRAX trace memory**    | Yes -- dedicated on-chip trace buffer for instruction flow recording |
| **Hardware breakpoints** | 2 instruction + 2 data (fewer than ESP32-C3's RISC-V)                |
| **Dual-core debug**      | Yes -- can halt/step each core independently                         |
| **OpenOCD config**       | `board/esp32-wrover-kit-1.8v.cfg` (with adapter)                     |

**TRAX** is particularly relevant: it's an Xtensa-specific on-chip trace buffer that can record instruction execution flow without halting the CPU. Key details:
- Records branch decisions and uninferable PC discontinuities (interrupts, exceptions, indirect jumps)
- Circular buffer -- limited size, records until PC reaches a specific address or externally stopped
- **Decoding tools:** [jcmvbkbc/trax-tools](https://github.com/jcmvbkbc/trax-tools) -- `trax-decode` converts binary dumps into control transfer sequences, `trax-trace.sh` produces full disassembly of executed instructions
- ESP-IDF's `esp_app_trace` library uses TRAX to stream trace data over JTAG to OpenOCD
- This allows capturing execution traces of the WiFi blob running at full speed

**Important:** The ESP32-C3 (RISC-V) does **NOT** have TRAX or any hardware instruction trace (no E-Trace or N-Trace). Full PC tracing on ESP32-C3 is only possible in Renode (emulation), not on real hardware. This makes the two platforms complementary: use TRAX on ESP32 Xtensa for blob tracing, use Renode for complete ESP32-C3 traces.

### nRF52840 Dongle -- SWD (Not JTAG)

The nRF52840 uses ARM SWD (Serial Wire Debug), not JTAG. It has:
- 6 hardware breakpoints, 4 watchpoints
- ETM (Embedded Trace Macrocell) for instruction tracing (requires external trace probe)
- No SWD pins easily accessible on the PCA10059 dongle form factor

### Recommended Hardware Addition

To enable JTAG on the ESP32 boards, add an **ESP-Prog** board (~$10-15):
- Provides FT2232H-based JTAG interface
- Direct connection to ESP32's JTAG pins
- Also provides a USB-UART bridge
- OpenOCD config: `board/esp-prog.cfg`
- Powers from USB, level-shifted for 3.3V

---

## 2. Execution Tracing on Real Hardware

### ESP-IDF Application Level Tracing

ESP-IDF provides a comprehensive tracing library that works via JTAG, UART, or USB:

**Modes:**
- **Post-mortem:** Trace data written to a circular buffer. Host reads after crash/halt. Low overhead.
- **Streaming:** Real-time data transfer to host via JTAG. Host consumes data continuously.

**Capabilities:**
1. **Custom application data:** Send arbitrary data (variable values, state snapshots) from firmware to host with minimal overhead
2. **Lightweight logging:** Printf-style logging over JTAG (faster than UART, no pin conflicts)
3. **SEGGER SystemView:** Real-time OS task/ISR/event visualization
4. **Gcov coverage:** Source code coverage analysis via JTAG

### Capturing MMIO Traces on Real Hardware

For comparing hardware vs emulation, we need to know which memory-mapped registers the firmware accesses. Several approaches:

**Approach A: OpenOCD watchpoints (slow, precise)**
- Set data watchpoints on specific register address ranges
- OpenOCD halts CPU on each access, logs address + value + direction
- Very slow (CPU halts on every hit), but gives exact data
- Limited by hardware watchpoint count (8 on ESP32-C3)

**Approach B: TRAX trace (ESP32 Xtensa only, fast)**
- TRAX records instruction flow into on-chip trace RAM
- Can reconstruct which memory accesses occurred by matching instruction trace to disassembly
- Non-invasive (no CPU halts), but limited trace RAM size
- Not available on ESP32-C3 (RISC-V has no TRAX)

**Approach C: esp32-open-mac QEMU fork (comprehensive)**
- The [esp32-open-mac/qemu](https://github.com/esp32-open-mac/qemu) fork instruments QEMU to log ALL peripheral register accesses
- Run the same firmware in instrumented QEMU, capture complete MMIO trace
- This gives the full register access sequence without hardware limitations
- Can then compare this QEMU trace against Renode's peripheral access log

**Approach D: GDB scripting (flexible, medium speed)**
- Use GDB connected via JTAG to set conditional breakpoints on register access functions
- Log access patterns via GDB Python scripting
- More flexible than watchpoints but still slows execution

### What Can Be Traced on the ESP32-C3

| Trace Type                  | Mechanism                | Speed Impact        | Completeness            |
| --------------------------- | ------------------------ | ------------------- | ----------------------- |
| UART output                 | Serial monitor           | None                | Application output only |
| App-level trace data        | esp_app_trace via JTAG   | Low (~1-2%)         | Custom data points      |
| SystemView (RTOS)           | SEGGER via JTAG/UART     | Low                 | Task/ISR/event timeline |
| Source coverage (gcov)      | esp_gcov via JTAG        | Low-Medium          | Line/branch coverage    |
| PC trace (instruction flow) | Not available on RV32    | N/A                 | N/A (no TRAX on RISC-V) |
| Register watchpoints        | OpenOCD triggers         | High (halts on hit) | 8 addresses at a time   |
| Full MMIO trace             | esp32-open-mac QEMU fork | N/A (runs in QEMU)  | Complete                |

---

## 3. Renode Execution Tracing and Instrumentation

Renode has extensive built-in instrumentation that's directly useful for emulation validation:

### Peripheral Access Logging (`LogPeripheralAccess`)

```
# Log all accesses to a specific peripheral
sysbus LogPeripheralAccess sysbus.uart

# Log ALL peripheral accesses (global)
sysbus LogPeripheralAccess true
```

Output includes: access type (Read/Write), address, register name, value, **active CPU name and current PC**. The PC is critical -- it tells you exactly which firmware instruction triggered each register access.

```
# Also log to file for offline analysis
logFile @trace.log
```

### Unhandled Access Behaviour

When firmware accesses a register address with no peripheral model, Renode:
- **Reads:** Logs a WARNING and **returns 0x0**: `ReadDoubleWord from non existing peripheral at 0x400D0118, returning 0x0`
- **Writes:** Logs a WARNING: `WriteDoubleWord to non existing peripheral at 0x400D0114, value 0xFFFFFFFF`
- Firmware **keeps running** (doesn't crash) -- this is the key for progressive development
- Use `sysbus SilenceRange <0x80000 0x1000>` to suppress warnings for known-unimportant ranges

Each unhandled access tells you: "firmware tried to access address X from PC Y, and no peripheral responded." This is the emulation gap signal.

### Execution Tracing (Full Instruction Trace)

Renode has a complete `ExecutionTracing` system:

```
# Enable per-instruction PC tracing
cpu MaximumBlockSize 1
cpu CreateExecutionTracing "tracer" @trace.log PC

# Options: PC, Opcode, PCAndOpcode, Disassembly, TraceBasedModel
# Also track all memory load/store addresses:
tracer TrackMemoryAccesses
```

- **PC mode:** Saves every program counter value
- **PCAndOpcode mode:** PC + instruction opcode
- **Disassembly mode:** Full human-readable disassembly via LLVM
- **TraceBasedModel:** Google's trace-based performance simulation format
- Output can be binary (`isBinary=True`) and compressed (`compress=True`)

### Coverage Reports (LCOV Format)

Renode's Execution Tracer can produce **LCOV-format coverage reports** compatible with standard tools like `lcov`/`genhtml`. This enables:
- Visualising which code paths the firmware actually executed
- Comparing coverage between real hardware (via gcov) and emulation
- Measuring progress as peripheral models are added

### Python Peripherals (Dynamic Register Stubs)

Renode supports writing peripherals in Python, enabling dynamic register responses:

```python
# In .repl platform file:
stub_wifi: Python.PythonPeripheral @ sysbus 0x60033000
    size: 0x3000
    initable: true
    script: "request.value = 0x0"  # Default: return 0 for all reads
```

Stateful peripherals are also possible (e.g., a flip-flop for polling loops):

```python
# flipflop.py -- toggles a status bit on each read
if request.isInit:
    lastVal = 0
else:
    lastVal = 1 - lastVal
    request.value = lastVal * 0xFFFFFFFF
```

The `request` object provides: `value`, `offset`, `type` (READ/WRITE), `isInit`.

### System Bus Hooks (Read/Write Interception)

Renode provides direct read/write interception on the system bus:

```
# Hook after any peripheral read
sysbus SetHookAfterPeripheralRead peripheral "python_script"

# Hook before any peripheral write
sysbus SetHookBeforePeripheralWrite peripheral "python_script"
```

Hook variables include: `self` (peripheral), `sysbus`, `machine`, `value`, `offset`. These can record all MMIO transactions for replay or comparison.

### Python Hooks

| Hook Type               | API                                  | Use Case                             |
| ----------------------- | ------------------------------------ | ------------------------------------ |
| **System bus hooks**    | `SetHookAfterPeripheralRead/Write`   | Intercept/record all register access |
| **CPU hooks**           | `cpu AddHook address "script"`       | Break at specific PCs                |
| **Watchpoint hooks**    | Trigger on specific address patterns | Data-dependent breakpoints           |
| **UART hooks**          | React to specific output lines       | Detect boot milestones               |
| **Packet interception** | Intercept network TX/RX              | Validate networking behaviour        |

### State Save/Restore (Snapshot Support)

Renode supports full machine state snapshots via `Save`/`Load` commands. This enables:
- **Fork-server pattern** for fuzzing (snapshot before peripheral access, try value, restore)
- Checkpoint/rollback for systematic exploration
- Saving a known-good boot state to skip slow init during development

### GDB Integration

Renode exposes a GDB server (`machine StartGdbServer port`), allowing the same GDB scripts used on real hardware to work on emulated firmware. This enables direct comparison workflows.

### pyrenode3 (Full Python 3 Control)

[pyrenode3](https://github.com/antmicro/pyrenode3) provides native CLR bindings -- a Python 3 wrapper around all of Renode. This is the most promising integration point for building a fuzzing harness:
- Programmatic machine creation and configuration
- Execution stepping and state inspection
- Memory and register reads/writes
- Peripheral registration at runtime

---

## 4. Hardware vs Emulation Comparison Workflow

### The Core Loop

```
┌──────────────────────────────────────────────────────────┐
│                    Build firmware.bin                      │
│              (ESP-IDF for ESP32-C3)                       │
└──────────────┬───────────────────────────┬───────────────┘
               │                           │
               ▼                           ▼
┌──────────────────────┐    ┌──────────────────────────────┐
│   Real Hardware       │    │   Renode Emulation            │
│   (rpi4-esp)          │    │                              │
│                       │    │ ESP32-C3 platform (.repl)    │
│ 1. Flash via esptool  │    │ + stub peripherals           │
│ 2. Monitor UART       │    │                              │
│ 3. Trace via JTAG     │    │ 1. Load same firmware.bin    │
│ 4. Capture MMIO       │    │ 2. Monitor UART              │
│    (OpenOCD/QEMU)     │    │ 3. Log peripheral accesses   │
│                       │    │ 4. Log unhandled accesses    │
└──────────┬────────────┘    └──────────────┬───────────────┘
           │                                │
           ▼                                ▼
┌──────────────────────────────────────────────────────────┐
│                    Compare Results                         │
│                                                           │
│ • UART output matches?                                    │
│ • Same peripheral accesses in same order?                 │
│ • Unhandled accesses → new peripherals to model           │
│ • Divergence point → incorrect register return value      │
└──────────────────────────────────────────────────────────┘
```

### Step-by-Step Process

1. **Start simple:** Use ESP-IDF `hello_world` example. It only needs UART + basic boot.
2. **Flash to real ESP32-C3** on rpi4-esp. Capture UART output.
3. **Run in Renode** with ESP32_UART + stub peripherals. Compare UART output.
4. **Identify first divergence:** Renode will likely fail during boot when it hits unmodelled system registers (RTC, clock, eFuse).
5. **Add stub for that register:** Use Renode Python peripheral to return a plausible value.
6. **Repeat:** Each iteration gets further into the boot process.
7. **Graduate stubs to real models:** Once a peripheral's access pattern is understood, implement a proper C# model.

---

## 5. Coverage-Guided Emulation Development (AFL Approach)

### The Concept

Traditional AFL works by: (1) instrumenting a target for code coverage, (2) mutating inputs, (3) keeping inputs that increase coverage. We adapt this for emulation development:

**Instead of mutating inputs, we mutate peripheral register responses.** Instead of maximizing code coverage in a target, we maximize how far the firmware gets through its initialization and main loop.

### The Feedback Loop

```
┌─────────────────────────────────────┐
│     Firmware binary (ESP-IDF)        │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│     Renode with stub peripherals     │
│                                     │
│  All unmodelled registers return    │
│  values from a "response table"     │
│                                     │
│  Instrumentation:                   │
│  • PC trace (code coverage)         │
│  • Peripheral access log            │
│  • UART output capture              │
│  • Crash/hang detection             │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│     Coverage Analysis                │
│                                     │
│  Did we get further than before?    │
│  New code paths reached?            │
│  UART output progress?              │
│  Boot stage advanced?               │
└──────────────┬──────────────────────┘
               │
        ┌──────┴──────┐
        │ Yes         │ No
        ▼             ▼
   Keep this       Discard/try
   register        different
   response        value
```

### Implementation with Renode

#### Phase 1: Passive Recording

Use Renode's Python peripheral to create a "catch-all" peripheral at each unmodelled register range:

```python
# catch_all_peripheral.py
# Attached to WiFi register range, timer range, etc.
#
# On read: log address, return 0 (or configurable value)
# On write: log address + value, ignore

access_log = []

if request.isRead:
    access_log.append(('R', request.offset, 0))
    request.value = 0
else:
    access_log.append(('W', request.offset, request.value))
```

Run firmware, collect the access log. This tells you exactly which registers firmware touches and in what order.

#### Phase 2: Response Table Fuzzing

Create a "response table" mapping register addresses to return values. Start with all-zeros. Then systematically try different values:

1. **From real hardware:** Use JTAG watchpoints or QEMU traces to capture what the real hardware returns for each register
2. **From ESP-IDF source:** Read the HAL driver code to understand expected register values (e.g., clock ready bits, status flags)
3. **From datasheets:** Timer count values, eFuse data, etc.
4. **By fuzzing:** For registers where the expected value is unknown, try common patterns (0x0, 0x1, 0xFFFFFFFF, specific bit patterns) and keep values that let firmware progress further

#### Phase 3: Graduate to Proper Models

Once a peripheral's access pattern and expected responses are understood:
1. Write a proper C# peripheral model in Renode
2. Implement the register state machine (not just static responses)
3. Test against the access log from real hardware
4. Validate by running the same firmware and comparing UART output

### Comparison with Fuzzware

[Fuzzware](https://github.com/fuzzware-fuzzer/fuzzware) (365 stars, USENIX Security 2022) uses a closely related approach:
- Runs firmware in an emulator (based on Unicorn/QEMU)
- Automatically generates peripheral models by fuzzing register responses
- Uses coverage feedback to find register values that maximise firmware execution
- Achieved state-of-the-art results in firmware fuzzing

**Key difference for our use case:** Fuzzware aims to find bugs (fuzzing for crashes). We aim to build faithful emulation (fuzzing for correctness). But the mechanism is identical: try register values → measure how far firmware gets → keep values that help.

**Potential approach:** Adapt Fuzzware's MMIO modelling technique for use with Renode instead of Unicorn, targeting ESP32 firmware specifically.

---

## 6. Related Academic Frameworks

### avatar² (569 stars)

- **URL:** https://github.com/avatartwo/avatar2
- **Approach:** Hardware-in-the-loop. Forwards peripheral accesses from emulator (QEMU/Panda/Unicorn) to real hardware via JTAG/SWD.
- **Relevance:** Could forward unmodelled ESP32 register accesses from Renode to the real ESP32-C3 on rpi4-esp via JTAG. The emulator handles CPU + known peripherals; unknown accesses are forwarded to real hardware.
- **Limitation:** Slow (each forwarded access requires JTAG round-trip). But excellent for identifying correct register responses to build Renode models from.
- **ESP32 compatibility:** avatar² supports OpenOCD as a target backend. The ESP32-C3's built-in JTAG is OpenOCD-compatible.

### Fuzzware (365 stars)

- **URL:** https://github.com/fuzzware-fuzzer/fuzzware
- **Paper:** "Fuzzware: Using Precise MMIO Modeling for Effective Firmware Fuzzing" (USENIX Security 2022)
- **Approach:** Automatically determines correct peripheral responses using coverage-guided fuzzing. Categorises MMIO accesses into patterns (set, passthrough, constant, etc.) and generates minimal models.
- **Relevance:** The MMIO modelling technique is directly applicable. Could be adapted to generate Renode peripheral stubs automatically.

### HALucinator

- **Approach:** Replaces HAL library functions in firmware with high-level handlers that emulate the effect without modelling hardware registers.
- **Relevance:** For ESP-IDF firmware, could intercept `esp_wifi_*()`, `esp_bt_*()` API calls in Renode and provide simulated responses without modelling the underlying hardware.
- **Limitation:** Tight coupling to specific ESP-IDF versions. Breaks when the HAL changes.

### P2IM (Processor-Peripheral Interface Model)

- **Paper:** "P2IM: Scalable and Hardware-independent Firmware Testing via Automatic Peripheral Interface Modeling" (USENIX Security 2020)
- **Approach:** Automatically identifies peripheral register types (control, status, data) from firmware access patterns. Creates minimal models.
- **Relevance:** The register classification heuristics could help prioritise which ESP32 registers need full models vs simple stubs.


---

## 7. Wireless Testing Scenarios

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

## 8. Emulation Development Workflow Using rpi4-esp

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

## Appendix: Quick Reference Commands

### ESP32-C3 JTAG on rpi4-esp

```bash
# Start OpenOCD (ESP32-C3 built-in USB-JTAG)
openocd -f board/esp32c3-builtin.cfg

# Connect GDB
riscv32-esp-elf-gdb -ex "target remote :3333" build/app.elf

# Set hardware breakpoint
(gdb) hbreak *0x60033000  # Break on WiFi register access

# Single step
(gdb) stepi

# Read register at address
(gdb) x/w 0x60033000

# Flash via OpenOCD
openocd -f board/esp32c3-builtin.cfg -c "program_esp firmware.bin 0x10000 verify exit"
```

### Renode Peripheral Access Logging

```
# In .resc script:
logLevel -1 sysbus           # Log ALL bus accesses at NOISY level
logFile @esp32c3_trace.log   # Save to file

# Or per-peripheral:
logLevel -1 sysbus.uart
logLevel -1 sysbus.stub_wifi

# Create catch-all for unmodelled range:
machine LoadPlatformDescriptionFromString "stub: Python.PythonPeripheral @ sysbus 0x60033000 { size: 0x3000; initable: true; script: \"request.value = 0\" }"
```

### ESP-IDF App Tracing

```bash
# Enable in menuconfig:
# Component config > Application Level Tracing > Data Destination = JTAG

# Build with tracing enabled
idf.py build

# Collect trace data via OpenOCD:
openocd -f board/esp32c3-builtin.cfg
# In another terminal:
esp_app_trace_tool.py read_trace trace.bin /dev/null

# Code coverage:
# Enable CONFIG_APPTRACE_GCOV_ENABLE
# Collect after test run:
esp_app_trace_tool.py gcov_dump build/app.elf
```
