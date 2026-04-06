# Renode RISC-V Customization for ESP32-C3

> Technical reference for ESP32-C3 non-standard features and their
> Renode implementation.

## Overview

The ESP32-C3 uses several non-standard RISC-V features. This document
describes how each is handled in the emulation.

## 1. CLIC Interrupt Controller

Renode has a complete CLIC implementation via
`IRQControllers.CoreLocalInterruptController` (since v1.16.0).

The interrupt matrix outputs are wired through CLIC (`[0-31] -> clic@[0-31]`)
which provides correct `mcause = 0x80000000 | cpu_line_number` automatically.
The CPU uses `PrivilegedArchitecture.PrivUnratified` for CLIC CSR support.

See [CLIC integration plan](clic-integration-plan.md) for full details.

**Renode quirks:**
- mtvec must be set via Renode API (`cpu MTVEC`), not `csrw` -- the CLIC's
  `IIndirectCSRPeripheral` intercepts `csrw` but doesn't update the TLIB raw
  register that the CPU checks for CLIC mode.
- SHV=1 (vectored) CLIC delivery doesn't work in the TLIB backend; SHV=0
  (non-vectored) works reliably with a dispatch hook at mtvec_base.

## 2. Cycle Counter CSR (0x802)

The ESP32-C3 maps its cycle counter to CSR 0x802 (not the standard 0xC00).
The ROM's `ets_delay_us` reads this CSR in a spin loop.

`RegisterCSRHandlerFromString` for CSR 0x802 returns
`cpu.ExecutedInstructions` as a monotonically incrementing proxy for cycles:

```python
set cycle_counter_handler
"""
if request.IsRead:
    request.Value = cpu.ExecutedInstructions
"""
cpu RegisterCSRHandlerFromString 0x802 $cycle_counter_handler
```

## 3. Custom CSR Callbacks

Two mechanisms available:

### A. Simple storage (RegisterCustomCSR)
```
cpu.RegisterCustomCSR "MPCER" 0x7E2 Machine
```
Used in .repl `init:` blocks. Stores/retrieves values without behavior.
Used for: USTATUS (0x000), MPCER (0x7E2), MPCMR (0x7E3), PMAADDR0/1 (0x800/0x801).

### B. Python callbacks (RegisterCSRHandlerFromString)
```python
set csr_handler
"""
if request.IsRead:
    request.Value = some_value
"""
cpu RegisterCSRHandlerFromString 0xF0D $csr_handler
```
- Full Python access to `cpu.GetRegister()`, `cpu.SetRegister()`
- CANNOT override standard CSRs (cycle, time, mcause)
- CAN define NEW custom CSRs with arbitrary behavior

## 4. ROM CRT0 Execution

The CPU starts at ROM `_init` (0x40001E90), matching the real hardware
reset vector. The ROM CRT0 runs fully:

1. **HW init** -- mstatus, mtvec, interrupt matrix, PMA CSRs, stack pointer
2. **Data copy** (`unpackloop` at 0x40001EF8) -- copies ROM data from IRAM
   to DRAM using a table at 0x40059200 (16-byte entries: {dest, end, source, pad})
3. **BSS clear** (`clearloop` at 0x40001F2A) -- zeroes ROM BSS using a table
   at 0x40059410 (12-byte entries: {start, end, pad})
4. **Jump to ROM main** (0x40047E3C) -- redirected to firmware entry since we
   load firmware directly rather than booting from flash

### IRAM data gap

The ROM ELF's IRAM LOAD segment ends at 0x40059590, but the CRT0 data-copy
sources are at 0x40059590-0x40059AC4 (1332 bytes). These are in ELF sections
but not LOAD segments, so `LoadELF` skips them. `rom_iram_data.bin` provides
this data (extracted by `tools/extract_rom_iram_data.py`).

### ROM function tables

All ROM function tables use original unmodified values from the ROM ELF:

- `rom_phyFuns` (0x3FCDF5B8) -- ROM PHY functions work with the Python
  stubs for FE/FE2/NRX/BB peripherals
- `rom_cache_internal_table_ptr` (0x3FCDFFD4) -- ROM cache functions work
  with the ExtMem C# peripheral (ICACHE_FREEZE_DONE returns true when
  FREEZE_ENA is set)
- `rom_spiflash_legacy_data` (0x3FCDFFF0) -- original ROM spiflash chip
  data works with the SPI MEM C# peripheral

### Analysis tools

- `tools/parse_rom_crt0_tables.py` -- dumps the CRT0 data-copy and BSS-clear tables
- `tools/find_rom_iram_gap.py` -- identifies IRAM addresses outside LOAD segments
- `tools/extract_rom_iram_data.py` -- extracts rom_iram_data.bin from ROM ELF

## 5. Sensitive Peripheral (PMS)

The firmware's `esp_memprot_init()` configures the Permission Management
System via the Sensitive peripheral at 0x600C1000. The C# peripheral
(94 registers) accepts all PMS boundary/lock writes correctly.

## 6. ExtMem Cache Control

The ExtMem peripheral at 0x600C4000 provides ICache control registers.
ROM cache functions access these registers for cache enable/disable,
invalidate, preload, and freeze operations.

Key implementation detail: `ICACHE_FREEZE_DONE` (bit 2 of 0x600C40CC)
returns true when `ICACHE_FREEZE_ENA` is set. The ROM's
`Cache_Freeze_ICache_Enable` function spins on this bit.
