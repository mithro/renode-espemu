#!/usr/bin/env python3
"""Find the ROM IRAM data gap — copy sources not covered by LOAD segments."""

import struct
import subprocess
from pathlib import Path

OBJDUMP = str(Path.home() / ".espressif/tools/riscv32-esp-elf/esp-14.2.0_20241119/riscv32-esp-elf/bin/riscv32-esp-elf-objdump")
READELF = str(Path.home() / ".espressif/tools/riscv32-esp-elf/esp-14.2.0_20241119/riscv32-esp-elf/bin/riscv32-esp-elf-readelf")
ROM_ELF = Path.home() / "esp" / "esp-rom-elfs" / "esp32c3_rev3_rom.elf"

# IRAM LOAD segment from readelf -l
IRAM_LOAD_START = 0x40000000
IRAM_LOAD_END = 0x40000000 + 0x59590  # FileSiz = MemSiz = 0x59590

# Data copy table addresses from _init disassembly
DATA_TABLE_START = 0x40059200
DATA_TABLE_END = 0x40059400


def read_elf_at_vaddr(vaddr, size):
    result = subprocess.run(
        [OBJDUMP, "-s", f"--start-address=0x{vaddr:x}", f"--stop-address=0x{vaddr+size:x}",
         str(ROM_ELF)],
        capture_output=True, text=True
    )
    data = bytearray()
    for line in result.stdout.splitlines():
        line = line.strip()
        if not line or not line[0].isalnum():
            continue
        parts = line.split()
        try:
            int(parts[0], 16)
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
    print(f"IRAM LOAD segment: 0x{IRAM_LOAD_START:08X}-0x{IRAM_LOAD_END:08X}")
    print()

    # Read the data copy table
    table_data = read_elf_at_vaddr(DATA_TABLE_START, DATA_TABLE_END - DATA_TABLE_START)

    min_source = 0xFFFFFFFF
    max_source_end = 0

    for i in range(0, len(table_data), 16):
        if i + 16 > len(table_data):
            break
        dest, end, source, pad = struct.unpack('<IIII', table_data[i:i+16])
        size = end - dest
        if size == 0:
            continue

        source_end = source + size
        in_load = IRAM_LOAD_START <= source < IRAM_LOAD_END
        label = "OK (in LOAD)" if in_load else "MISSING (outside LOAD)"

        if not in_load:
            min_source = min(min_source, source)
            max_source_end = max(max_source_end, source_end)

        print(f"  src=0x{source:08X}..0x{source_end:08X} -> dst=0x{dest:08X} ({size:4d}B) {label}")

    print()
    if max_source_end > 0:
        gap_size = max_source_end - min_source
        print(f"IRAM gap: 0x{min_source:08X}-0x{max_source_end:08X} ({gap_size} bytes)")
        print(f"  Starts at LOAD end + 0x{min_source - IRAM_LOAD_END:X}")
        print()
        print(f"Fix: extract this range from the ROM ELF and load it:")
        print(f"  sysbus LoadBinary @platforms/rom_iram_data.bin 0x{min_source:08X}")
    else:
        print("All copy sources are within the LOAD segment")


if __name__ == "__main__":
    main()
