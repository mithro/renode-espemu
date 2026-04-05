#!/usr/bin/env python3
"""Extract ROM ELF DRAM data sections into a binary for Renode loading.

The ROM ELF has data sections in the 0x3FCD range that are NOT covered by
any LOAD program header. LoadELF skips them, so they must be loaded separately.

This creates a binary that can be loaded with:
    sysbus LoadBinary @platforms/rom_dram_data.bin 0x3FCDF060
"""

import subprocess
import struct
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
ROM_ELF = Path.home() / "esp" / "esp-rom-elfs" / "esp32c3_rev3_rom.elf"
OUTPUT = REPO_ROOT / "platforms" / "rom_dram_data.bin"

OBJCOPY = str(Path.home() / ".espressif/tools/riscv32-esp-elf/esp-14.2.0_20241119/riscv32-esp-elf/bin/riscv32-esp-elf-objcopy")
READELF = str(Path.home() / ".espressif/tools/riscv32-esp-elf/esp-14.2.0_20241119/riscv32-esp-elf/bin/riscv32-esp-elf-readelf")
OBJDUMP = str(Path.home() / ".espressif/tools/riscv32-esp-elf/esp-14.2.0_20241119/riscv32-esp-elf/bin/riscv32-esp-elf-objdump")

# ROM DRAM data sections not covered by LOAD segments
# Extracted from: readelf -S rom.elf | grep "3fcd.*PROGBITS"
# Only sections with non-zero size
DRAM_SECTIONS = [
    # (name, addr, size) — from readelf -S -W output
    (".data_btdm",                          0x3FCDF09C, 0x0C),
    (".data_uart",                          0x3FCDF060, 0x04),
    (".data_spi_flash",                     0x3FCDF5BC, 0x20),
    (".data_cache",                         0x3FCDF630, 0x08),
    (".data_ets_delay",                     0x3FCDF64C, 0x04),
    (".data.interface.bt",                  0x3FCDF96C, 0xBC),
    (".data.interface.cache",               0x3FCDFFD4, 0x04),
    (".data.interface.cache_config",        0x3FCDFFE4, 0x04),
    (".data.interface.phy",                 0x3FCDFFEC, 0x04),
    (".data.interface.spiflash_legacy",     0x3FCDFFF0, 0x08),
    (".data.interface.pp",                  0x3FCDFFFC, 0x04),
]


def extract_section_data(elf_path, section_name):
    """Extract raw bytes for a named section using objdump -s."""
    result = subprocess.run(
        [OBJDUMP, "-s", "-j", section_name, str(elf_path)],
        capture_output=True, text=True
    )
    if result.returncode != 0:
        return None

    data = bytearray()
    for line in result.stdout.splitlines():
        line = line.strip()
        # Lines look like: " 3fcdffd4 34e4f13f   4..?"
        if not line or not line[0].isalnum():
            continue
        parts = line.split()
        if len(parts) < 2:
            continue
        try:
            int(parts[0], 16)  # verify it's a hex address
        except ValueError:
            continue
        # Extract hex data words (up to 4 words after the address)
        for word in parts[1:5]:
            if len(word) == 8:
                try:
                    data.extend(bytes.fromhex(word))
                except ValueError:
                    break
            else:
                break
    return bytes(data)


def main():
    if not ROM_ELF.exists():
        print(f"ERROR: ROM ELF not found: {ROM_ELF}")
        return 1

    # Find the overall range to cover
    min_addr = min(addr for _, addr, _ in DRAM_SECTIONS)
    max_addr = max(addr + size for _, addr, size in DRAM_SECTIONS)
    total_size = max_addr - min_addr
    base_addr = min_addr

    print(f"ROM DRAM data range: 0x{base_addr:08X} to 0x{max_addr:08X} ({total_size} bytes)")

    # Create a zero-filled buffer
    buf = bytearray(total_size)

    # Extract each section and place it in the buffer
    loaded = 0
    for name, addr, expected_size in DRAM_SECTIONS:
        data = extract_section_data(ROM_ELF, name)
        if data is None:
            print(f"  SKIP {name}: not found in ELF")
            continue
        offset = addr - base_addr
        actual_size = len(data)
        buf[offset:offset + actual_size] = data
        print(f"  OK   {name}: 0x{addr:08X} ({actual_size} bytes)")
        loaded += 1

    # Write the binary
    OUTPUT.write_bytes(bytes(buf))
    print(f"\nWrote {OUTPUT} ({total_size} bytes, {loaded} sections)")
    print(f"Load with: sysbus LoadBinary @platforms/rom_dram_data.bin 0x{base_addr:08X}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
