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

**Current state:** Patch rom_phyFuns (0x3FCDF5B8) and
rom_cache_internal_table_ptr (0x3FCDFFD4) after BSS clear with CPU hook
at 0x40380338.

**Root cause:** ROM ELF has BSS segments at address 0 which the firmware
clears during startup. This zeroes out the ROM function table pointers.
We patch them back after BSS clear.

**Approach to eliminate:** Instead of patching after BSS clear:
- Option 1: Map the ROM function tables to a non-BSS address
- Option 2: Pre-initialize the function table pointers in the ROM data segment
- Option 3: Use a CPU hook at the ROM BSS clear routine to skip zeroing
  the function table region

**Verdict:** Fixable with better ROM data initialization.

## Summary: Workaround Status

| # | Workaround | Status | Notes |
|---|---|---|---|
| W.1 | init_flash skip | **Eliminated** | SPI MEM C# + ROM spiflash data |
| W.2 | Delay function skip | **Eliminated** | CSR 0x802 handler returns ExecutedInstructions |
| W.3 | Memprot skip | **Eliminated** | Sensitive C# + ROM table re-patch after memprot |
| W.4 | Brownout ISR skip | **Eliminated** | RTC C# reports no brownout |
| W.5 | Force MIE/MSTATUS | **Eliminated** | CLIC handles interrupt enable |
| W.6 | Manual interrupt kick | **Eliminated** | CLIC delivers naturally |
| W.7 | mcause override | **Eliminated** | CLIC sets mcause correctly |
| W.8 | ROM function tables | Active (minor) | BSS clear patch still needed |
