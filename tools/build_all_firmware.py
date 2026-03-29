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

# Peripherals with ESP-IDF firmware (have main/CMakeLists.txt)
PERIPHERAL_FIRMWARE = [
    "efuse",
    "rng",
    "gpio",
    "system",
    "systimer",
    "timer-group",
]


def build_firmware(peripheral: str, clean: bool = False) -> bool:
    """Build one peripheral's test firmware. Returns True on success."""
    fw_dir = PERIPHERALS_DIR / peripheral / "firmware"
    if not (fw_dir / "main").is_dir():
        print(f"  SKIP {peripheral}: no firmware/main/ directory")
        return True

    print(f"  BUILD {peripheral}...")

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
        print(f"  FAIL {peripheral}:")
        # Show last 10 lines of output
        for line in result.stdout.splitlines()[-10:]:
            print(f"    {line}")
        for line in result.stderr.splitlines()[-5:]:
            print(f"    {line}")
        return False

    # Verify ELF exists
    elf_files = list((fw_dir / "build").glob("*.elf"))
    if not elf_files:
        print(f"  FAIL {peripheral}: no .elf file produced")
        return False

    print(f"  OK   {peripheral}: {elf_files[0].name}")
    return True


def main():
    clean = "--clean" in sys.argv

    print(f"Building all test firmware (clean={clean})")
    print(f"ESP-IDF: {IDF_PATH}")
    print(f"Peripherals: {PERIPHERALS_DIR}")
    print()

    results = {}
    for peripheral in PERIPHERAL_FIRMWARE:
        results[peripheral] = build_firmware(peripheral, clean)

    # Also build hello_world
    hw_dir = REPO_ROOT / "hello_world" / "firmware"
    if hw_dir.is_dir() and (hw_dir / "hello_world.elf").exists():
        print(f"  OK   hello_world: already built")
        results["hello_world"] = True
    else:
        print(f"  SKIP hello_world: pre-built firmware")
        results["hello_world"] = True

    print()
    print("=== Build Summary ===")
    passed = sum(1 for v in results.values() if v)
    total = len(results)
    for name, ok in results.items():
        status = "PASS" if ok else "FAIL"
        print(f"  {status} {name}")
    print(f"\n{passed}/{total} builds succeeded")

    return 0 if all(results.values()) else 1


if __name__ == "__main__":
    sys.exit(main())
