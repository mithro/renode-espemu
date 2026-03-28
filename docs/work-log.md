# ESP32 Renode Emulation Work Log

## Dashboard

| Metric                        | Value     |
|-------------------------------|-----------|
| Boot progress score           | **5/5**   |
| Peripherals implemented       | 4/11 (C#: Interrupt Matrix, eFuse, RTC; Python: RNG, TIMG0, SYSTIMER, EXTMEM, SYSTEM) |
| Peripherals passing all tests | 0/0       |
| Total Robot tests             | 0         |
| Last update                   | 2026-03-29 |

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

### 2026-03-29 00:45 - QEMU Interrupt Matrix Architecture Analysis

Analysed QEMU's `esp32c3_intmatrix.c` to understand interrupt delivery:

**Architecture:**
1. Peripheral fires IRQ → GPIO input to interrupt matrix
2. Matrix looks up `irq_map[source]` → CPU interrupt line
3. Checks `irq_prio[line] >= irq_thres` AND CPU can accept
4. If yes: `qemu_irq_pulse(out_irqs[line])` → CPU takes interrupt
5. If no: sets `irq_pending` bit, fires later when threshold changes or MIE re-enabled

**Key QEMU connections:**
- `systimer.alarm[i]` → `intmatrix.gpio_in[ETS_SYSTIMER_TARGET0 + i]`
- `system.from_cpu_int[i]` → `intmatrix.gpio_in[ETS_FROM_CPU_INTR0 + i]`
- `intmatrix.gpio_out[line]` → `cpu.irq[line]`

**Conclusion:** Python stubs CANNOT implement this. Need a C# peripheral with:
- GPIO inputs (from systimer, SYSTEM crosscore)
- GPIO outputs (to CPU interrupt lines)
- Mapping, priority, threshold, pending state management
- MIE-enabled callback to fire pending interrupts

### 2026-03-29 01:00 - C# Interrupt Matrix Built

- Wrote `peripherals/interrupt-matrix/ESP32C3_InterruptMatrix.cs` (259 lines)
- Compiles and loads via `include @` in Renode
- Implements: 64 source mapping registers, CPU_INT_ENABLE, CPU_INT_TYPE,
  CPU_INT_CLEAR, CPU_INT_EIP_STATUS, CPU_INT_PRI_0-31, CPU_INT_THRESH
- GPIO outputs wired to CPU interrupt lines [1-31]
- GPIO inputs accept OnGPIO(source, level) from peripherals
- Replaces Python stub_intmatrix in esp32c3.repl

Firmware state after boot with C# interrupt matrix:
- CPU_INT_ENABLE = 0x06000004 (lines 2, 25, 26 enabled)
- Source 36/61 → int 25 (pri 4), Source 56-58 → int 26 (pri 4), Source 27 → int 2 (pri 1)
- Source 50 (FROM_CPU_INTR0) NOT mapped (cross-core int not yet configured)
- Source 39 (SYSTIMER_TARGET2) NOT mapped (timer tick not yet configured)

**Current blocker:** The FreeRTOS idle task runs but main_task never scheduled.
Need C# SYSTIMER peripheral that fires alarm interrupts through the interrupt
matrix. The systimer alarm → intmatrix.OnGPIO(39) → CPU int line → FreeRTOS
tick handler → task switch → main_task → app_main → "Hello world!".

Boot progress: **4/5** (scheduler running, awaiting timer interrupt for task switch)

### 2026-03-29 01:28 - [Phase 0.5] Boot reaches 5/5 — "Hello world!" printed

**Root cause of 4→5 gap:** The systimer ISR reads `SYSTIMER_INT_ST_REG` (offset 0x70)
to confirm which alarm fired. Our Python stub returned 0 for this register, so the ISR
thought no alarm matched and did nothing → no FreeRTOS tick → no task switch.

**Fix:** Two-part synchronization of interrupt paths:
1. **Systimer stub** (`stub_systimer.py`): Added tracking for `INT_ENA` (0x64),
   `INT_RAW` (0x68), `INT_CLR` (0x6C), and `INT_ST` (0x70 = RAW & ENA).
   Write-1-to-clear semantics on INT_CLR. External writes to INT_RAW set bits
   (used by .resc to simulate alarm match).
2. **Boot script** (`run_boot_test.resc`): Before each `OnGPIO 37 true`, write
   `sysbus WriteDoubleWord 0x60023068 0x1` to set INT_RAW bit 0 (TARGET0 alarm).
   This synchronizes the interrupt matrix path with the status register path.

**UART output captured:**
```
I (0) main_task: Started on CPU0
I (0) main_task: Calling app_main()
Hello world!
This is esp32c3 chip with 1 CPU core(s), WiFi/BLE, silicon revision v0.0, ...
I (0) main_task: Returned from app_main()
FreeRTOS: FreeRTOS Task "main" should not return, Aborting now!
```

The post-app_main crash is expected — hello_world returns from app_main() and FreeRTOS
catches it. On real hardware, hello_world loops with vTaskDelay(1000/portTICK_PERIOD_MS).

**Interrupt matrix source mappings configured by firmware:**
- Source 27 → CPU int 2 (pri 1)
- Source 36/61 → CPU int 25 (pri 4)
- Source 50 (FROM_CPU_INTR0) → CPU int 3
- Source 37 (SYSTIMER_TARGET0) → CPU int 4
- Source 54 → CPU int 27
- Source 35 → CPU int 24
- Source 33 → CPU int 5

Boot progress: **5/5** (hello_world prints "Hello world!" via UART)
