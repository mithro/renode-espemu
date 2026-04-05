#!/usr/bin/env python3
"""Extract complete ROM IRAM binary including CRT0 data-copy sources.

The ROM ELF's IRAM LOAD segment ends at 0x40059590, but the CRT0 data
initializers are at 0x40059590-0x40059AC4. These are in ELF sections
but not LOAD segments. On real hardware they're in mask ROM (read-only).

This extracts ALL section data in the 0x40000000+ range from the ROM ELF,
creating a flat binary that covers code + CRT0 data sources. This should
be loaded as read-only memory to match real hardware behavior.
"""

import struct
import subprocess
import sys
from pathlib import Path

READELF = str(Path.home() / ".espressif/tools/riscv32-esp-elf/esp-14.2.0_20241119/riscv32-esp-elf/bin/riscv32-esp-elf-readelf")
ROM_ELF = Path.home() / "esp" / "esp-rom-elfs" / "esp32c3_rev3_rom.elf"
REPO_ROOT = Path(__file__).resolve().parent.parent
OUTPUT = REPO_ROOT / "platforms" / "rom_iram_data.bin"

IRAM_BASE = 0x40059590  # Start of gap (end of LOAD segment)


def parse_sections(elf_path):
    """Parse all ELF section headers."""
    result = subprocess.run(
        [READELF, "-S", "-W", str(elf_path)],
        capture_output=True, text=True
    )
    sections = []
    lines = result.stdout.splitlines()
    for line in lines:
        # Section lines look like:
        #   [ 7] .data.interface.cache PROGBITS 3fcdffd4 062b90 000004 00  W  0  0  4
        line = line.strip()
        if not line.startswith('['):
            continue
        # Remove [NN] prefix
        bracket_end = line.index(']')
        rest = line[bracket_end+1:].strip()
        parts = rest.split()
        if len(parts) < 6:
            continue
        name = parts[0]
        stype = parts[1]
        if stype not in ('PROGBITS', 'NOBITS'):
            continue
        try:
            addr = int(parts[2], 16)
            offset = int(parts[3], 16)
            size = int(parts[4], 16)
        except (ValueError, IndexError):
            continue
        sections.append({
            'name': name,
            'type': stype,
            'addr': addr,
            'offset': offset,
            'size': size,
        })
    return sections


def main():
    sections = parse_sections(ROM_ELF)
    elf_bytes = ROM_ELF.read_bytes()

    # Find all sections that the CRT0 copy table needs (DRAM data sections)
    dram_sections = [s for s in sections
                     if s['type'] == 'PROGBITS'
                     and 0x3FCD0000 <= s['addr'] < 0x3FCE0000
                     and s['size'] > 0]

    print(f"DRAM PROGBITS sections in ROM ELF: {len(dram_sections)}")
    for s in sorted(dram_sections, key=lambda x: x['addr']):
        data_preview = elf_bytes[s['offset']:s['offset']+4].hex() if s['size'] >= 4 else ""
        print(f"  {s['name']:40s} addr=0x{s['addr']:08X} off=0x{s['offset']:06X} size={s['size']:4d}  data={data_preview}")

    # Now find the IRAM sections that contain the CRT0 data sources
    # These are stored in the ELF with DRAM VAddr but the CRT0 reads from IRAM PhysAddr
    # The PhysAddr mapping is defined by the LOAD segments (readelf -l)

    # Actually, the simplest approach: use objcopy to create a binary of everything
    # above the LOAD segment end. Let's just directly extract data from the ELF
    # using the copy table mapping.

    # Read the copy table from the .text section (which IS loaded)
    text_sections = [s for s in sections if s['name'] == '.text']
    if not text_sections:
        print("ERROR: .text section not found")
        return 1

    # The copy table is at 0x40059200 in IRAM. Find its file offset.
    # .text addr=0x40038DEC, so offset of 0x40059200 from .text start:
    text_sec = text_sections[0]
    table_file_offset = text_sec['offset'] + (0x40059200 - text_sec['addr'])
    table_size = 0x40059400 - 0x40059200

    print(f"\nCopy table at file offset 0x{table_file_offset:06X}")

    table_data = elf_bytes[table_file_offset:table_file_offset + table_size]

    # Build mapping: IRAM source addr -> DRAM dest addr
    entries = []
    for i in range(0, len(table_data), 16):
        if i + 16 > len(table_data):
            break
        dest, end, source, pad = struct.unpack('<IIII', table_data[i:i+16])
        size = end - dest
        if size > 0 and source >= IRAM_BASE:
            entries.append((dest, size, source))

    # For each entry, find the DRAM section data in the ELF file
    max_iram_end = max(e[2] + e[1] for e in entries)
    total_size = max_iram_end - IRAM_BASE
    buf = bytearray(total_size)

    print(f"\nBuilding IRAM binary: 0x{IRAM_BASE:08X}-0x{max_iram_end:08X} ({total_size} bytes)")

    loaded = 0
    for dest_dram, size, source_iram in entries:
        # Find section containing dest_dram
        for s in dram_sections:
            if s['addr'] <= dest_dram < s['addr'] + s['size']:
                offset_in_sec = dest_dram - s['addr']
                file_pos = s['offset'] + offset_in_sec
                actual_size = min(size, s['size'] - offset_in_sec)
                data = elf_bytes[file_pos:file_pos + actual_size]

                iram_offset = source_iram - IRAM_BASE
                buf[iram_offset:iram_offset + len(data)] = data
                loaded += 1
                print(f"  0x{source_iram:08X} <- {s['name']:35s}+0x{offset_in_sec:X} ({len(data):4d}B)")
                break
        else:
            print(f"  0x{source_iram:08X} MISS -> 0x{dest_dram:08X} ({size}B)")

    OUTPUT.write_bytes(bytes(buf))
    print(f"\nWrote {OUTPUT} ({total_size} bytes, {loaded}/{len(entries)} entries)")
    print(f"Load with: sysbus LoadBinary @platforms/rom_iram_data.bin 0x{IRAM_BASE:08X}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
