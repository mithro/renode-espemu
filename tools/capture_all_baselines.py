#!/usr/bin/env python3
"""Capture hardware baselines for all per-feature test firmware.

Flashes each firmware to the real ESP32-C3 on rpi4-esp and captures
UART output to peripherals/<name>/baselines/hardware/<test_name>.log

Usage:
    python3 tools/capture_all_baselines.py [--host HOST] [--port PORT]
"""

import argparse
import subprocess
import sys
import time
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
PERIPHERALS_DIR = REPO_ROOT / "peripherals"
DEFAULT_HOST = "eth0.rpi4-esp.iot.welland.mithis.com"
DEFAULT_PORT = "/dev/ttyACM1"
CAPTURE_DURATION = 12
SERIAL_CAPTURE = REPO_ROOT / "tools" / "serial_capture.py"


def discover_built_firmware():
    """Find all built firmware with ELF files."""
    firmware = []
    for periph_dir in sorted(PERIPHERALS_DIR.iterdir()):
        if not periph_dir.is_dir():
            continue
        fw_base = periph_dir / "firmware"
        if not fw_base.is_dir():
            continue
        for sub in sorted(fw_base.iterdir()):
            if sub.is_dir() and (sub / "build").is_dir():
                elfs = list((sub / "build").glob("*.elf"))
                bins = list((sub / "build").glob("*.bin"))
                # Filter to app bins (not bootloader)
                app_bins = [b for b in bins if not b.name.startswith("bootloader")]
                app_bins = [b for b in app_bins if not b.name.startswith("partition")]
                if elfs and app_bins:
                    firmware.append((periph_dir.name, sub.name, sub))
    return firmware


def capture_one(periph: str, test_name: str, fw_dir: Path,
                host: str, port: str, duration: int, force: bool = False) -> bool:
    """Flash and capture one firmware. Returns True on success."""
    build_dir = fw_dir / "build"
    bootloader = build_dir / "bootloader" / "bootloader.bin"
    partition = build_dir / "partition_table" / "partition-table.bin"

    # Find app binary
    app_bins = [b for b in build_dir.glob("*.bin")
                if not b.name.startswith(("bootloader", "partition"))]
    if not app_bins:
        print(f"  SKIP {test_name}: no app binary")
        return False

    app_bin = app_bins[0]

    # Skip if baseline already exists (unless --force)
    baseline_dir = PERIPHERALS_DIR / periph / "baselines" / "hardware"
    baseline_file = baseline_dir / f"{test_name}.log"
    if not force and baseline_file.exists() and baseline_file.stat().st_size > 0:
        print(f"  SKIP {test_name}: exists ({baseline_file.stat().st_size} bytes)")
        return True

    remote_dir = f"/tmp/esp32c3_{test_name}"

    try:
        # Copy all files in one scp call
        subprocess.run(["ssh", host, f"mkdir -p {remote_dir}"],
                       check=True, capture_output=True, timeout=60)
        subprocess.run(
            ["scp", str(bootloader), str(partition), str(app_bin),
             f"{host}:{remote_dir}/"],
            check=True, capture_output=True, timeout=120)

        # Flash
        flash_cmd = (
            f"cd {remote_dir} && ~/.venvs/esptool/bin/esptool "
            f"--chip esp32c3 -p {port} -b 460800 "
            f"--before default_reset --after hard_reset write_flash "
            f"--flash_mode dio --flash_size 4MB --flash_freq 80m "
            f"0x0 bootloader.bin 0x8000 partition-table.bin 0x10000 {app_bin.name}"
        )
        subprocess.run(["ssh", host, flash_cmd],
                       check=True, capture_output=True, timeout=60)

        # Copy serial capture helper
        subprocess.run(["scp", str(SERIAL_CAPTURE), f"{host}:/tmp/serial_capture.py"],
                       check=True, capture_output=True, timeout=60)

        # Capture UART
        capture_cmd = (
            f"timeout {duration + 5} "
            f"~/.venvs/esptool/bin/python3 /tmp/serial_capture.py "
            f"{port} {duration} --reset"
        )
        result = subprocess.run(["ssh", host, capture_cmd],
                                capture_output=True, text=True,
                                timeout=duration + 30)
        output = result.stdout

        # Save baseline
        baseline_dir = PERIPHERALS_DIR / periph / "baselines" / "hardware"
        baseline_dir.mkdir(parents=True, exist_ok=True)
        baseline_file = baseline_dir / f"{test_name}.log"
        baseline_file.write_text(output)

        lines = len(output.splitlines())
        print(f"  OK   {test_name}: {lines} lines")
        return True

    except (subprocess.CalledProcessError, subprocess.TimeoutExpired) as e:
        print(f"  FAIL {test_name}: {e}")
        return False


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default=DEFAULT_HOST)
    parser.add_argument("--port", default=DEFAULT_PORT)
    parser.add_argument("--duration", type=int, default=CAPTURE_DURATION)
    parser.add_argument("--peripheral", help="Only capture this peripheral")
    parser.add_argument("--force", action="store_true", help="Re-capture existing baselines")
    args = parser.parse_args()

    firmware = discover_built_firmware()
    if args.peripheral:
        firmware = [(p, t, d) for p, t, d in firmware if p == args.peripheral]

    print(f"Capturing {len(firmware)} firmware baselines")
    print(f"Host: {args.host}:{args.port}")
    print()

    results = {}
    for periph, test_name, fw_dir in firmware:
        results[test_name] = capture_one(
            periph, test_name, fw_dir, args.host, args.port, args.duration,
            force=args.force)
        time.sleep(1)  # Brief pause between flashes

    print()
    passed = sum(1 for v in results.values() if v)
    print(f"{passed}/{len(results)} baselines captured")
    return 0 if all(results.values()) else 1


if __name__ == "__main__":
    sys.exit(main())
