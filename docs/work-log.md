# ESP32 Renode Emulation Work Log

## Dashboard

| Metric                        | Value     |
|-------------------------------|-----------|
| Boot progress score           | 1/5       |
| Peripherals implemented       | 0/11      |
| Peripherals passing all tests | 0/0       |
| Total Robot tests             | 0         |
| Last update                   | 2026-03-28 |

## Peripheral Status

| Peripheral       | Branch | Status      | L1 | L2 | L3 | L4 | L5 |
|------------------|--------|-------------|----|----|----|----|-----|
| eFuse            |        | not started |    |    |    |    |     |
| RTC Controller   |        | not started |    |    |    |    |     |
| DPORT/System     |        | not started |    |    |    |    |     |
| Watchdog         |        | not started |    |    |    |    |     |
| Clock Control    |        | not started |    |    |    |    |     |
| Timer Groups     |        | not started |    |    |    |    |     |
| Interrupt Matrix |        | not started |    |    |    |    |     |
| Cache/MMU        |        | not started |    |    |    |    |     |
| SPI Flash        |        | not started |    |    |    |    |     |
| GPIO             |        | not started |    |    |    |    |     |
| RNG              |        | not started |    |    |    |    |     |

## Log Entries

### 2026-03-28 21:10 - [Phase 0] Infrastructure Setup

- Created ESP32-C3 platform definition (esp32c3.repl) with 42 peripheral stubs
- Built hello_world firmware for ESP32-C3 (ESP-IDF v5.4.1)
- Flashed to real ESP32-C3 on rpi4-esp, captured hardware baseline (70 lines)
- Hardware baseline confirms: "Hello world!" at boot, full ROM→bootloader→app trace
- Created boot progress measurement and output comparison tooling
- Boot progress score: not yet measured (pending first Renode run)

### 2026-03-28 21:21 - [Phase 0.5] First Boot Attempt

- Fixed Python stubs: `request.value` → `request.Value` (PascalCase for Renode API)
- Downloaded ESP32-C3 ROM ELF from espressif/esp-rom-elfs (rev3 for our v0.4 chip)
- Loaded ROM ELF + app ELF into Renode -- CPU starts executing ROM code at 0x40039xxx
- Boot progress score: **1/5** (CPU executing, hitting peripheral stubs)
- CPU reaches ROM code, reads peripheral registers, then crashes at PC=0x0
- Key unhandled accesses: 0x1B4, 0x1AC (low addresses -- possibly interrupt vector related)
- First real peripheral access from ROM: reads at 0x60008xxx range (RTC/eFuse)
- Next steps: investigate the PC=0x0 crash -- likely needs interrupt vector table or
  `mtvec` CSR setup, plus the RTC/eFuse peripheral stubs need to return sensible values
- Committed: platform fix + ROM loading approach
