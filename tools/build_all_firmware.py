#!/usr/bin/env python3
"""Build all ESP-IDF test firmware for ESP32-C3 peripherals.

Usage:
    uv run tools/build_all_firmware.py [--clean]

Requires ESP-IDF to be installed at ~/esp/esp-idf.
"""

import os
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
PERIPHERALS_DIR = REPO_ROOT / "peripherals"
IDF_PATH = Path.home() / "esp" / "esp-idf"
EXPORT_SCRIPT = IDF_PATH / "export.sh"

# Auto-discover all ESP-IDF firmware directories
# Supports both single firmware (firmware/main/) and multi (firmware/test_*/main/)
def discover_firmware():
    """Find all ESP-IDF firmware directories under peripherals/."""
    firmware = []
    for periph_dir in sorted(PERIPHERALS_DIR.iterdir()):
        if not periph_dir.is_dir():
            continue
        fw_base = periph_dir / "firmware"
        if not fw_base.is_dir():
            continue
        # Check for multi-firmware layout: firmware/test_*/main/
        for sub in sorted(fw_base.iterdir()):
            if sub.is_dir() and (sub / "main").is_dir() and (sub / "CMakeLists.txt").exists():
                firmware.append(sub)
        # Check for single-firmware layout: firmware/main/
        if (fw_base / "main").is_dir() and (fw_base / "CMakeLists.txt").exists():
            # Only add if no sub-firmware found (avoid building old monolithic + new split)
            if not any(f.parent == fw_base for f in firmware):
                firmware.append(fw_base)
    return firmware


def build_firmware(fw_dir: Path, clean: bool = False) -> bool:
    """Build one firmware directory. Returns True on success."""
    name = fw_dir.name if fw_dir.name != "firmware" else fw_dir.parent.name

    print(f"  BUILD {name}...")

    # Build command: use login shell to get correct Python, source ESP-IDF
    cmd = f"source {EXPORT_SCRIPT} && cd {fw_dir}"
    if clean or not (fw_dir / "build").is_dir():
        cmd += " && idf.py set-target esp32c3"
    cmd += " && idf.py build"

    result = subprocess.run(
        ["bash", "-l", "-c", cmd],
        capture_output=True,
        text=True,
        timeout=300,
    )

    if result.returncode != 0:
        print(f"  FAIL {name}:")
        for line in result.stdout.splitlines()[-5:]:
            print(f"    {line}")
        return False

    # Verify ELF exists
    elf_files = list((fw_dir / "build").glob("*.elf"))
    if not elf_files:
        print(f"  FAIL {name}: no .elf file produced")
        return False

    print(f"  OK   {name}: {elf_files[0].name}")
    return True


def main():
    clean = "--clean" in sys.argv

    print(f"Building all test firmware (clean={clean})")
    print(f"ESP-IDF: {IDF_PATH}")
    print(f"Peripherals: {PERIPHERALS_DIR}")
    print()

    all_firmware = discover_firmware()
    print(f"Discovered {len(all_firmware)} firmware directories\n")

    results = {}
    for fw_dir in all_firmware:
        name = fw_dir.name if fw_dir.name != "firmware" else fw_dir.parent.name
        results[name] = build_firmware(fw_dir, clean)

    print()
    print("=== Build Summary ===")
    passed = sum(1 for v in results.values() if v)
    total = len(results)
    for name, ok in sorted(results.items()):
        status = "PASS" if ok else "FAIL"
        print(f"  {status} {name}")
    print(f"\n{passed}/{total} builds succeeded")

    return 0 if all(results.values()) else 1


if __name__ == "__main__":
    sys.exit(main())
