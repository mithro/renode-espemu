#!/usr/bin/env python3
"""Patch rom_iram_data.bin to redirect ROM function pointers to stubs.

Currently: NO patches needed. All ROM function tables work with the
existing peripheral implementations:
- rom_phyFuns: ROM PHY functions work with Python stubs for FE/FE2/NRX/BB
- rom_cache_internal_table_ptr: ROM cache functions work with FREEZE_DONE fix
- rom_spiflash_legacy_data: original ROM spiflash data works with SPI MEM

This script exists for reference and can be used if future changes
require redirecting specific ROM function pointers to stubs.
"""

import struct
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
BINARY = REPO_ROOT / "platforms" / "rom_iram_data.bin"
IRAM_BASE = 0x40059590

# No patches needed — all ROM function tables work as-is
PATCHES = []


def main():
    if not PATCHES:
        print("No patches needed — rom_iram_data.bin uses pure ROM data")
        return

    data = bytearray(BINARY.read_bytes())

    for name, iram_addr, new_val in PATCHES:
        offset = iram_addr - IRAM_BASE
        old_val = struct.unpack_from('<I', data, offset)[0]
        struct.pack_into('<I', data, offset, new_val)
        print(f"{name}:")
        print(f"  IRAM 0x{iram_addr:08X} (offset 0x{offset:X})")
        print(f"  {old_val:#010x} → {new_val:#010x}")

    BINARY.write_bytes(bytes(data))
    print(f"\nPatched {BINARY}")


if __name__ == "__main__":
    main()
