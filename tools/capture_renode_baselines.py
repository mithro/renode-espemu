#!/usr/bin/env python3
"""Capture Renode baselines for all per-feature test firmware.

Runs each firmware in Renode with the standard ESP32-C3 boot setup
and captures UART output to peripherals/<name>/baselines/renode/<test>.log

Usage:
    python3 tools/capture_renode_baselines.py [--peripheral NAME]
"""

import argparse
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
PERIPHERALS_DIR = REPO_ROOT / "peripherals"
SETUP_RESC = REPO_ROOT / "tests" / "esp32c3_setup.resc"
CLIC_SETUP_RESC = REPO_ROOT / "tests" / "clic_setup.resc"


def discover_built_firmware():
    """Find all built firmware with ELF+BIN files."""
    firmware = []
    for periph_dir in sorted(PERIPHERALS_DIR.iterdir()):
        if not periph_dir.is_dir():
            continue
        fw_base = periph_dir / "firmware"
        if not fw_base.is_dir():
            continue
        for sub in sorted(fw_base.iterdir()):
            if not sub.is_dir() or not (sub / "build").is_dir():
                continue
            elfs = list((sub / "build").glob("*.elf"))
            if elfs:
                firmware.append((periph_dir.name, sub.name, elfs[0]))
    return firmware


def find_bin(elf_path: Path) -> Path:
    """Find the .bin file matching the ELF."""
    build_dir = elf_path.parent
    bins = [b for b in build_dir.glob("*.bin")
            if not b.name.startswith(("bootloader", "partition"))]
    return bins[0] if bins else None


def capture_one(periph: str, test_name: str, elf_path: Path) -> bool:
    """Run one firmware in Renode and capture UART output."""
    bin_path = find_bin(elf_path)
    if not bin_path:
        print(f"  SKIP {test_name}: no .bin file")
        return False

    # Build a Renode script that loads the firmware and runs it
    resc = f'''
$firmware_elf="{elf_path}"
$firmware_bin="{bin_path}"
include @{SETUP_RESC}

showAnalyzer uart0

# Phase 1: Boot with CLIC present but unconfigured
start
sleep 1
pause

# Phase 2: Configure CLIC interrupt delivery
include @{CLIC_SETUP_RESC}

# Phase 3: CLIC delivers interrupts naturally
start
sleep 4
pause
quit
'''

    # Write temp .resc file
    tmp_resc = REPO_ROOT / "tmp" / f"renode_capture_{test_name}.resc"
    tmp_resc.parent.mkdir(parents=True, exist_ok=True)
    tmp_resc.write_text(resc)

    try:
        result = subprocess.run(
            ["renode", "--disable-xwt", "--console",
             "-e", f"include @{tmp_resc}"],
            capture_output=True,
            text=True,
            timeout=60,
        )

        # Extract UART lines from Renode output
        uart_lines = []
        for line in result.stdout.splitlines():
            if "uart0:" in line and "[host:" in line:
                # Extract the actual UART text after the timestamp
                parts = line.split("]", 2)
                if len(parts) >= 3:
                    uart_text = parts[2].strip()
                    uart_lines.append(uart_text)

        # Save baseline
        baseline_dir = PERIPHERALS_DIR / periph / "baselines" / "renode"
        baseline_dir.mkdir(parents=True, exist_ok=True)
        baseline_file = baseline_dir / f"{test_name}.log"
        baseline_file.write_text("\n".join(uart_lines) + "\n" if uart_lines else "")

        print(f"  OK   {test_name}: {len(uart_lines)} lines")
        return True

    except subprocess.TimeoutExpired:
        print(f"  FAIL {test_name}: timeout")
        return False
    except Exception as e:
        print(f"  FAIL {test_name}: {e}")
        return False
    finally:
        tmp_resc.unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--peripheral", help="Only capture this peripheral")
    args = parser.parse_args()

    firmware = discover_built_firmware()
    if args.peripheral:
        firmware = [(p, t, e) for p, t, e in firmware if p == args.peripheral]

    print(f"Capturing {len(firmware)} Renode baselines")
    print()

    results = {}
    for periph, test_name, elf_path in firmware:
        results[test_name] = capture_one(periph, test_name, elf_path)

    print()
    passed = sum(1 for v in results.values() if v)
    print(f"{passed}/{len(results)} Renode baselines captured")
    return 0 if all(results.values()) else 1


if __name__ == "__main__":
    sys.exit(main())
