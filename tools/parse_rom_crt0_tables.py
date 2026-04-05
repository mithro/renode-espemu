#!/usr/bin/env python3
"""Parse the ROM CRT0 data-copy and BSS-clear tables from the ROM ELF.

The ROM _init function at 0x40001E90 uses two tables:
- unpackloop: copies data from ROM IRAM to DRAM (16-byte entries)
- clearloop: zeroes BSS ranges in DRAM (12-byte entries)

This script reads the tables from the ROM ELF and shows what they do.
"""

import struct
import subprocess
import sys
from pathlib import Path

OBJCOPY = str(Path.home() / ".espressif/tools/riscv32-esp-elf/esp-14.2.0_20241119/riscv32-esp-elf/bin/riscv32-esp-elf-objcopy")
ROM_ELF = Path.home() / "esp" / "esp-rom-elfs" / "esp32c3_rev3_rom.elf"

# Table addresses from disassembly of _init
DATA_TABLE_START = 0x40059200  # _data_end_btdm_rom
DATA_TABLE_END   = 0x40059400  # _data_end
BSS_TABLE_START  = 0x40059410  # _bss_start
BSS_TABLE_END    = 0x40059590  # _bss_end


def extract_raw(elf, start, size):
    """Extract raw bytes from an ELF at a given virtual address."""
    import tempfile, os
    with tempfile.NamedTemporaryFile(suffix='.bin', delete=False) as f:
        tmpfile = f.name
    try:
        subprocess.run([
            OBJCOPY, '-O', 'binary',
            f'--only-section=.text',
            str(elf), tmpfile
        ], check=True, capture_output=True)
        # .text starts at 0x40038DEC in the ELF. But we need offsets relative
        # to the LOAD segment. Let's use a different approach — read the ELF directly.
        return None
    finally:
        os.unlink(tmpfile)


def read_elf_at_vaddr(elf_path, vaddr, size):
    """Read bytes from an ELF file at a given virtual address using objdump."""
    result = subprocess.run(
        [str(Path.home() / ".espressif/tools/riscv32-esp-elf/esp-14.2.0_20241119/riscv32-esp-elf/bin/riscv32-esp-elf-objdump"),
         "-s", f"--start-address=0x{vaddr:x}", f"--stop-address=0x{vaddr+size:x}",
         str(elf_path)],
        capture_output=True, text=True
    )
    data = bytearray()
    for line in result.stdout.splitlines():
        line = line.strip()
        if not line or not line[0].isalnum():
            continue
        parts = line.split()
        try:
            addr = int(parts[0], 16)
        except (ValueError, IndexError):
            continue
        for word in parts[1:5]:
            if len(word) == 8:
                try:
                    data.extend(bytes.fromhex(word))
                except ValueError:
                    break
    return bytes(data)


def main():
    print("=" * 70)
    print("ROM CRT0 Data Copy Table (unpackloop)")
    print(f"Table at 0x{DATA_TABLE_START:08X}-0x{DATA_TABLE_END:08X}")
    print("Format: 16-byte entries {dest, end, source, pad}")
    print("=" * 70)

    data_table = read_elf_at_vaddr(ROM_ELF, DATA_TABLE_START, DATA_TABLE_END - DATA_TABLE_START)

    key_addrs = {
        0x3FCDF5B8: "rom_phyFuns",
        0x3FCDFFD4: "rom_cache_internal_table_ptr",
        0x3FCDFFF0: "rom_spiflash_legacy_data",
    }

    entry_size = 16
    for i in range(0, len(data_table), entry_size):
        if i + entry_size > len(data_table):
            break
        dest, end, source, pad = struct.unpack('<IIII', data_table[i:i+entry_size])
        size = end - dest
        if size == 0:
            continue
        label = ""
        for addr, name in key_addrs.items():
            if dest <= addr < end:
                label = f"  *** CONTAINS {name} ***"
        print(f"  copy 0x{source:08X} -> 0x{dest:08X}..0x{end:08X} ({size:4d} bytes){label}")

    print()
    print("=" * 70)
    print("ROM CRT0 BSS Clear Table (clearloop)")
    print(f"Table at 0x{BSS_TABLE_START:08X}-0x{BSS_TABLE_END:08X}")
    print("Format: 12-byte entries {start, end, pad}")
    print("=" * 70)

    bss_table = read_elf_at_vaddr(ROM_ELF, BSS_TABLE_START, BSS_TABLE_END - BSS_TABLE_START)

    entry_size = 12
    for i in range(0, len(bss_table), entry_size):
        if i + entry_size > len(bss_table):
            break
        start, end, pad = struct.unpack('<III', bss_table[i:i+entry_size])
        size = end - start
        if size == 0:
            continue
        label = ""
        for addr, name in key_addrs.items():
            if start <= addr < end:
                label = f"  *** CLEARS {name} ***"
        print(f"  zero 0x{start:08X}..0x{end:08X} ({size:5d} bytes){label}")


if __name__ == "__main__":
    main()
