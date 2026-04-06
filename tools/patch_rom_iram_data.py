#!/usr/bin/env python3
"""Patch rom_iram_data.bin to redirect ROM function pointers to stubs.

The ROM CRT0 copies initialization data from IRAM to DRAM. By patching
the IRAM source values, the CRT0 naturally initializes DRAM with our
stub addresses — no runtime hooks needed.

Three pointers need redirection:
1. rom_phyFuns (0x3FCDF5B8) → 0x50001D00 (PHY stubs)
2. rom_cache_internal_table_ptr (0x3FCDFFD4) → 0x50001D00 (cache stubs)
3. rom_spiflash_legacy_data[0] (0x3FCDFFF0) → 0x50001E00 (spiflash data)
"""

import struct
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
BINARY = REPO_ROOT / "platforms" / "rom_iram_data.bin"
IRAM_BASE = 0x40059590

# Mapping from copy table analysis:
# DRAM addr → IRAM source addr → binary offset
PATCHES = [
    # (name, iram_addr, new_value)
    ("rom_phyFuns",                 0x40059A94, 0x50001D00),  # PHY stubs (ROM PHY functions crash)
    ("rom_cache_internal_table_ptr", 0x4005964C, 0x50001D00),  # cache stubs (ROM table causes reboot)
    # spiflash_legacy keeps original ROM value (0x3FCDF5C0) — works with SPI MEM
]


def main():
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
