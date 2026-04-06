# Renode RISC-V Customization for ESP32-C3

> Research findings for eliminating boot workarounds (Phase 6)

## Overview

The ESP32-C3 uses several non-standard RISC-V features that Renode doesn't
natively support. This document catalogs what Renode CAN do, what it CAN'T,
and the approach for each workaround elimination.

## 1. CLIC Interrupt Controller

**Renode status: FULLY IMPLEMENTED (since v1.16.0, Sep 2024)**

Renode has a complete CLIC implementation via the
`IRQControllers.CoreLocalInterruptController` class. It was missed in the
initial research. See [CLIC integration plan](clic-integration-plan.md) for
full details.

**ESP32-C3 integration (completed 2026-04-05):**

The interrupt matrix outputs are wired through CLIC (`[0-31] -> clic@[0-31]`)
which provides correct `mcause = 0x80000000 | cpu_line_number` automatically.
The CPU uses `PrivilegedArchitecture.PrivUnratified` for CLIC CSR support.

**Workarounds eliminated:** W.5 (MIE force), W.6 (manual kick), W.7 (mcause hook).

**Renode quirks discovered:**
- mtvec must be set via Renode API (`cpu MTVEC`), not `csrw` — the CLIC's
  `IIndirectCSRPeripheral` intercepts `csrw` but doesn't update the TLIB raw
  register that the CPU checks for CLIC mode.
- SHV=1 (vectored) CLIC delivery doesn't work in the TLIB backend; SHV=0
  (non-vectored) works reliably with a dispatch hook at mtvec_base.

## 2. Cycle Counter CSR (0x802)

**Status: ELIMINATED (2026-04-05)**

The ESP32-C3 maps its cycle counter to CSR 0x802 (not the standard 0xC00).
The ROM's `ets_delay_us` reads this CSR in a spin loop.

**Fix:** `RegisterCSRHandlerFromString` for CSR 0x802 returns
`cpu.ExecutedInstructions` as a monotonically incrementing proxy for cycles.
This works because 0x802 is a vendor-specific CSR (not a standard one),
so `RegisterCSRHandlerFromString` can handle it.

```python
set cycle_counter_handler
"""
if request.IsRead:
    request.Value = cpu.ExecutedInstructions
"""
cpu RegisterCSRHandlerFromString 0x802 $cycle_counter_handler
```

## 3. Custom CSR Callbacks

**Renode status: FULLY SUPPORTED**

Two mechanisms available:

### A. Simple storage (RegisterCustomCSR)
```
cpu.RegisterCustomCSR "MPCER" 0x7E2 Machine
```
Used in .repl `init:` blocks. Stores/retrieves values without behavior.
We already use this for MPCER (0x7E2), PMAADDR0/1, PMACFG0.

### B. Python callbacks (RegisterCSRHandlerFromString)
```python
set csr_handler
"""
if request.IsRead:
    request.Value = some_incrementing_value
elif request.IsWrite:
    stored_value = request.Value
"""
cpu RegisterCSRHandlerFromString 0xF0D $csr_handler
```
- Full Python access to `cpu.GetRegister()`, `cpu.SetRegister()`
- Can perform side effects and modify CPU state
- CANNOT override standard CSRs (cycle, time, mcause)
- CAN define NEW custom CSRs with arbitrary behavior

**Useful for:** ESP32-C3 vendor CSRs (MPCER, MPCMR) if they need behavior
beyond simple storage.

## 4. MIE/MSTATUS Enable

**Status: ELIMINATED (2026-04-05)**

With CLIC integrated, the firmware's own MSTATUS.MIE setting suffices.
CLIC handles per-interrupt enable through its own registers. No manual
MIE/MSTATUS force is needed.

## 5. Single Manual Interrupt Kick

**Status: ELIMINATED (2026-04-05)**

With CLIC integrated, the systimer fires naturally through the
intmatrix → CLIC → CPU path. No manual kick is needed.

## 6. Memprot Skip

**Status: ELIMINATED (2026-04-05)**

The firmware's `esp_memprot_init()` at 0x403804B2 configures the PMS
(Permission Management System) via the Sensitive peripheral at 0x600C1000.
The Sensitive C# peripheral (94 registers) accepts all PMS writes correctly.

**Root cause of previous failure:** The memprot code also writes to DRAM
at 0x3FCDFFD4 (rom_cache_internal_table_ptr), overwriting the ROM function
table stub installed by the BSS-clear hook. The fix is to re-patch the
pointer immediately after memprot returns:

```
cpu AddHook 0x403804E4 "machine.SystemBus.WriteDoubleWord(0x3FCDFFD4, 0x50001D00)"
```

This lets memprot run fully (PMS registers configured correctly) while
restoring the ROM table pointer that memprot's DRAM writes corrupted.

## 7. Brownout ISR Skip

**Current state:** Skip brownout ISR at 0x403805EE. The interrupt fires
spuriously because the RTC brownout detection isn't properly modeled.

**Approach to eliminate:** In the RTC C# peripheral, ensure BROWN_OUT_REG
bit 31 (brownout detected) stays 0, and the brownout interrupt source
is never asserted to the interrupt matrix.

**Verdict:** Likely already fixed — our RTC C# returns bit 31 = 0. The
issue may be that the firmware enables the brownout interrupt and the
interrupt matrix delivers it spuriously. Need to verify.

## 8. ROM Function Table Stubs

**Status: ELIMINATED (2026-04-06)**

The CPU now starts at ROM `_init` (0x40001E90), which runs the CRT0
`unpackloop` (data copy from IRAM to DRAM) and `clearloop` (BSS zero).
This naturally initializes all ROM function table pointers:
- `rom_phyFuns` (0x3FCDF5B8) — ROM PHY functions work with Python stubs
- `rom_cache_internal_table_ptr` (0x3FCDFFD4) — works after FREEZE_DONE fix
- `rom_spiflash_legacy_data` (0x3FCDFFF0) — works with SPI MEM peripheral

**Root cause analysis:** The ROM ELF's IRAM LOAD segment ends at 0x40059590,
but the CRT0 data-copy sources are at 0x40059590-0x40059AC4 (1332 bytes).
These are in ELF sections but not LOAD segments, so `LoadELF` skips them.
Loading `rom_iram_data.bin` at 0x40059590 provides this data.

The previous SP value (0x3FCE0000) caused the firmware's stack frame to
overlap `rom_cache_internal_table_ptr` at 0x3FCDFFD4. Fixing SP to
0x3FCDE710 (matching real ROM) eliminated this. The `ICACHE_FREEZE_DONE`
bit fix in ExtMem allowed the ROM cache functions to work.

See `tools/parse_rom_crt0_tables.py` and `tools/extract_rom_iram_data.py`
for the CRT0 table analysis and data extraction tools.

## Summary: Workaround Status

| # | Workaround | Status | Notes |
|---|---|---|---|
| W.1 | init_flash skip | **Eliminated** | SPI MEM C# + ROM spiflash data |
| W.2 | Delay function skip | **Eliminated** | CSR 0x802 handler returns ExecutedInstructions |
| W.3 | Memprot skip | **Eliminated** | Sensitive C# accepts PMS writes |
| W.4 | Brownout ISR skip | **Eliminated** | RTC C# reports no brownout |
| W.5 | Force MIE/MSTATUS | **Eliminated** | CLIC handles interrupt enable |
| W.6 | Manual interrupt kick | **Eliminated** | CLIC delivers naturally |
| W.7 | mcause override | **Eliminated** | CLIC sets mcause correctly |
| W.8 | ROM function table stubs | **Eliminated** | ROM CRT0 + SP fix + FREEZE_DONE |
| W.8 | ROM function tables | Active (minor) | BSS clear patch still needed |
