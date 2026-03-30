# Renode RISC-V Customization for ESP32-C3

> Research findings for eliminating boot workarounds (Phase 6)

## Overview

The ESP32-C3 uses several non-standard RISC-V features that Renode doesn't
natively support. This document catalogs what Renode CAN do, what it CAN'T,
and the approach for each workaround elimination.

## 1. CLIC Interrupt Controller

**Renode status: NOT IMPLEMENTED**

- GitHub Issue #423 (opened Jan 2023) requests CLIC support — still OPEN
- Renode only supports standard PLIC and CLINT
- ESP32-C3 uses CLIC-like interrupt delivery where mcause = 0x80000000 | cpu_line_number
- Standard RISC-V sets mcause = 11 (MEIP) for all external interrupts

**Impact on ESP32-C3:**
The firmware's `_interrupt_handler` reads mcause to determine which CPU interrupt
line fired, then dispatches to the appropriate ISR. Without CLIC, Renode sets
mcause=0x8000000B (MEIP) for all interrupts, and the firmware can't distinguish
between different interrupt sources.

**Current workaround:** Python hook at 0x4038022E overrides s1 register after
the firmware's `csrr s1, mcause` instruction, replacing the standard value
with the ESP32-C3's interrupt matrix PendingMcause.

**Approach to eliminate:** Two options:
1. **Custom mcause delivery (preferred):** If Renode adds support for
   per-interrupt-source mcause values, configure the interrupt matrix to set
   mcause = 0x80000000 | line for each GPIO output. Requires Renode core change.
2. **CSR intercept (fallback):** Use `RegisterCSRHandlerFromString` on a custom
   CSR, but this CANNOT override the standard mcause CSR (0x342). The Python
   hook approach remains the viable workaround.

**Verdict:** Cannot eliminate without Renode core modification. Python hook
is the correct long-term approach until CLIC support is added.

## 2. Cycle Counter CSR (0xC00)

**Renode status: NOT AUTOMATICALLY INCREMENTED**

The standard RISC-V `cycle` CSR (0xC00) is supposed to increment with each
clock cycle. On ESP32-C3, the firmware reads this CSR in delay loops
(`ets_delay_us`, `esp_rom_delay_us`). Without an incrementing counter, the
delay loops spin forever.

**Key finding:** Renode's CSR 0xC00 exists but is fixed at 0. It does NOT
increment with instruction execution.

**Current workaround:** Skip delay functions by hooking their entry addresses
(0x400462CC, 0x40000050) and jumping to return address.

**Approach to eliminate:**
- `RegisterCSRHandlerFromString` CANNOT override standard CSRs (0xC00 is built-in)
- Option 1: Modify Renode source to auto-increment cycle counter
- Option 2: Hook all delay function call sites (current approach, fragile)
- Option 3: Use a LimitTimer-based C# peripheral that provides a
  monotonically incrementing value, mapped to a custom CSR that the firmware
  reads. Requires patching the firmware's delay function to read our custom CSR.

**Verdict:** Cannot fully eliminate without Renode core modification. The
current hook-based skip is the practical approach.

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

**Renode status: STANDARD RISC-V BEHAVIOR**

Standard RISC-V interrupt delivery requires:
- MSTATUS.MIE = 1 (global machine interrupt enable)
- MIE.MEIE = 1 (machine external interrupt enable, bit 11)

ESP32-C3 firmware uses the interrupt matrix (CLIC-like) instead of standard
mie/mip. It doesn't set MIE.MEIE because it relies on the interrupt matrix's
own enable/threshold mechanism.

**Current workaround:** Force `cpu MIE 0x800` and `cpu MSTATUS 0x1808` after
boot init completes.

**Approach to eliminate:** If CLIC support were added to Renode, the interrupt
matrix could directly trigger CPU interrupts without going through the standard
MIE/MIP path. Without CLIC, the force-enable is necessary because our interrupt
delivery uses MEIP (GPIO 11) which requires MIE.MEIE=1.

**Verdict:** Tied to CLIC implementation. Cannot eliminate independently.

## 5. Single Manual Interrupt Kick

**Current state:** After boot init, we manually assert OnGPIO 37 (SYSTIMER)
and OnGPIO 50 (FROM_CPU_INTR0) to deliver the first FreeRTOS tick. After that,
the C# SYSTIMER auto-fires subsequent alarms.

**Root cause:** During boot init, the firmware configures the interrupt matrix
and SYSTIMER. The SYSTIMER starts firing alarms (confirmed by logs), but the
first alarm delivery doesn't produce a FreeRTOS tick because:
1. MIE is not yet enabled (firmware uses CLIC, not standard mie)
2. The mcause override hook may not fire correctly for the first interrupt

**Approach to eliminate:** Once MIE is properly enabled (either via CLIC or
the current force-enable earlier in boot), the SYSTIMER auto-fire should work.
The key is timing: MIE must be enabled before the first SYSTIMER alarm fires.

**Verdict:** May be fixable by moving the MIE force-enable to a CPU hook
at the point where firmware enables interrupts in the interrupt matrix.

## 6. Memprot Skip

**Current state:** Skip memprot section at 0x403804B2 via PC redirect to
0x403804E4. The firmware tries to configure the Permission Management System
(PMS/Sensitive peripheral at 0x600C1000).

**Approach to eliminate:** Implement a C# Sensitive peripheral that accepts
the PMS configuration writes without errors. The firmware writes permission
boundaries and locks — a simple read/write register model would suffice.

**Verdict:** Fixable with a C# peripheral implementation (similar to other
stubs we've converted).

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

## Summary: Workaround Elimination Feasibility

| # | Workaround | Feasibility | Requires |
|---|---|---|---|
| W.1 | init_flash skip | Medium | Debug SPI MEM command sequence |
| W.2 | Delay function skip | Hard | Renode cycle counter increment |
| W.3 | Memprot skip | Easy | C# Sensitive peripheral |
| W.4 | Brownout ISR skip | Easy | Verify RTC brownout model |
| W.5 | Force MIE/MSTATUS | Hard | CLIC support or early hook |
| W.6 | Manual interrupt kick | Medium | Fix MIE timing |
| W.7 | mcause override | Hard | CLIC support |
| W.8 | ROM function tables | Easy | Better ROM init |
