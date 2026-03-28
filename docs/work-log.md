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

### 2026-03-28 21:43 - [Phase 0.5] Crash Investigation and Fixes

Root cause analysis of the PC=0x0 crash:

1. **call_start_cpu0** → reads RTC_CNTL_RESET_STATE_REG (0x60008038) for reset reason
2. RTC stub returned 0 (unknown), firmware took wrong path
3. Firmware called ROM `rom_i2c_writeReg_Mask` which dereferences `rom_phyFuns` at 0x3FCDF5B8
4. `rom_phyFuns` = 0 (uninitialised) → NULL pointer dereference → PC=0x0 → crash

Fixes applied:
- **RTC stub**: Now returns POWERON_RESET (1) at offset 0x38 via Python script
- **DRAM extended**: 384KB → 448KB (0x3FC70000-0x3FCDFFFF) to catch stack underflow
- **null_guard**: 8KB at address 0x0 prevents CPU abort on NULL dereference

Remaining issues (next session):
- **ROM BSS zeroing**: Firmware startup zeros address 0x0 (ROM ELF has BSS segments at VA=0), overwriting any ret-stub patches. Need to patch rom_phyFuns AFTER BSS init, or use a CPU hook at a PC after BSS clear.
- **Custom CSR 0x7E2**: ESP32-C3 vendor-specific CSR not implemented in Renode. Causes illegal instruction exception. Need to either add CSR support or skip the instruction.
- **IRAM addresses 0x40386622, 0x403808ba**: Contain 0x0 (uninitialised). May need additional ROM or firmware segments loaded.

Boot progress: **1/5** → investigating (CPU runs ~200K instructions before crash)
Commits: 96d7732, 83ab511, c6f1daf, 0f522f7, f02c1fc, 740f432
