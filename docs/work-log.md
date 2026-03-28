# ESP32 Renode Emulation Work Log

## Dashboard

| Metric                        | Value     |
|-------------------------------|-----------|
| Boot progress score           | 4/5       |
| Peripherals implemented       | 0/11 (stubs with Python scripts for RTC, TIMG0, SYSTIMER, EXTMEM) |
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

### 2026-03-29 00:10 - [Phase 0.5 continued] Boot reaches 4/5

Massive debugging session. Fixes applied iteratively:

1. **Custom CSRs**: Registered ESP32-C3 vendor CSRs (MPCER 0x7E2, PMAADDR 0x800/801, PMACFG0 0x802) via `init:` block
2. **Timer Group 0**: Python stub returns RTC_CALI_RDY (bit 15) at offset 0x68
3. **RTC time**: Incrementing counter at offsets 0x10/0x14
4. **ROM function tables**: 544-byte stub binary with 128 ret-pointers, patched via CPU hook after BSS clear for rom_phyFuns (0x3FCDF5B8) and rom_cache_internal_table_ptr (0x3FCDFFD4)
5. **Delay skip**: ets_delay_us and esp_rom_delay_us hooked to return immediately (CSR 0x802 cycle counter doesn't increment)
6. **Memprot skip**: Entire memprot section in call_start_cpu0 skipped via PC redirect (PMS peripheral not implemented)
7. **App image header**: Raw hello_world.bin loaded at DROM 0x3C020000 so magic byte 0xE9 is valid
8. **ROM data segment**: Extracted and loaded at 0x3FF00000 (data bus view of ROM), fixing ets_rom_layout struct and pointer
9. **EXTMEM cache**: Returns ICACHE_ENABLE (bit 0) and sync/preload done bits
10. **SYSTIMER**: Latch-based counter with stable reads for consistency check
11. **Assert skip**: __assert_func hooked to log and return
12. **IROM hook discovery**: CPU hooks on flash addresses (0x42xxxxxx) DO NOT WORK in Renode -- must hook IRAM call sites instead

Boot messages captured via esp_rom_printf hook:
- "Unicore app", "Pro cpu start user code", "Application information:"
- "Project name: %s", "App version: %s", "ESP-IDF: %s"
- "Chip rev: v%d.%d", "Initializing. RAM available for dynamic allocation:"
- Memory region listings

Current blocker: **Interrupt Matrix** (0x600C2000)
- MIE=0, MIP=0, MSTATUS.MIE=0: firmware uses ESP32-C3 CLIC interrupt controller
- Standard RISC-V mie/mip not used by ESP-IDF on ESP32-C3
- Without interrupt matrix, FreeRTOS tick never fires
- main_task (→ app_main → "Hello world!") never scheduled

Boot progress: **4/5** (all init done, FreeRTOS scheduler running, main_task waiting for tick)
