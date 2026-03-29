#!/usr/bin/env python3
"""Flash test firmware to real ESP32-C3 and capture UART baseline output.

Usage:
    uv run tools/capture_hardware_baseline.py <peripheral> [--host rpi4-esp]

Requires:
    - SSH access to the target host (rpi4-esp by default)
    - ESP32-C3 connected via USB serial on the target host
    - esptool.py installed on the target host

The firmware is copied to the target, flashed, and UART output is
captured for a fixed duration, then saved to:
    peripherals/<peripheral>/baselines/hardware.log
"""

import argparse
import subprocess
import sys
import time
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_HOST = "eth0.rpi4-esp.iot.welland.mithis.com"
DEFAULT_PORT = "/dev/ttyACM1"
CAPTURE_DURATION = 10  # seconds


def find_firmware(peripheral: str) -> tuple[Path, Path, Path]:
    """Find the bootloader, partition table, and app binaries."""
    fw_dir = REPO_ROOT / "peripherals" / peripheral / "firmware" / "build"
    if not fw_dir.is_dir():
        raise FileNotFoundError(f"Build directory not found: {fw_dir}")

    bootloader = fw_dir / "bootloader" / "bootloader.bin"
    partition = fw_dir / "partition_table" / "partition-table.bin"

    # Find the app binary (test_<name>.bin)
    app_bins = list(fw_dir.glob("test_*.bin"))
    if not app_bins:
        # Try <name>.bin
        app_bins = list(fw_dir.glob("*.bin"))
        app_bins = [b for b in app_bins if b.name not in ("bootloader.bin",)]
    if not app_bins:
        raise FileNotFoundError(f"No app binary found in {fw_dir}")

    return bootloader, partition, app_bins[0]


def flash_and_capture(
    peripheral: str, host: str, port: str, duration: int
) -> str:
    """Flash firmware to target and capture UART output."""
    bootloader, partition, app_bin = find_firmware(peripheral)

    print(f"Firmware: {app_bin.name}")
    print(f"Target: {host}:{port}")
    print(f"Capture duration: {duration}s")

    # Copy files to target
    remote_dir = f"/tmp/esp32c3_test_{peripheral}"
    print(f"\nCopying firmware to {host}:{remote_dir}...")

    subprocess.run(
        ["ssh", host, f"mkdir -p {remote_dir}"],
        check=True,
    )
    for f in (bootloader, partition, app_bin):
        subprocess.run(
            ["scp", str(f), f"{host}:{remote_dir}/{f.name}"],
            check=True,
        )

    # Flash
    print("Flashing...")
    flash_cmd = (
        f"cd {remote_dir} && ~/.venvs/esptool/bin/esptool "
        f"--chip esp32c3 -p {port} -b 460800 "
        f"--before default_reset --after hard_reset write_flash "
        f"--flash_mode dio --flash_size 4MB --flash_freq 80m "
        f"0x0 bootloader.bin 0x8000 partition-table.bin 0x10000 {app_bin.name}"
    )
    subprocess.run(["ssh", host, flash_cmd], check=True)

    # Copy serial capture helper
    capture_script = Path(__file__).parent / "serial_capture.py"
    subprocess.run(
        ["scp", str(capture_script), f"{host}:/tmp/serial_capture.py"],
        check=True,
    )

    # Capture UART output via pyserial (handles USB-JTAG-Serial properly)
    print(f"Capturing UART output for {duration}s...")
    capture_cmd = (
        f"timeout {duration + 5} "
        f"~/.venvs/esptool/bin/python3 /tmp/serial_capture.py "
        f"{port} {duration} --reset"
    )
    result = subprocess.run(
        ["ssh", host, capture_cmd],
        capture_output=True,
        text=True,
        timeout=duration + 30,
    )

    return result.stdout


def main():
    parser = argparse.ArgumentParser(description="Capture hardware baseline")
    parser.add_argument("peripheral", help="Peripheral name (e.g., efuse)")
    parser.add_argument("--host", default=DEFAULT_HOST, help="Target hostname")
    parser.add_argument("--port", default=DEFAULT_PORT, help="Serial port")
    parser.add_argument(
        "--duration", type=int, default=CAPTURE_DURATION, help="Capture seconds"
    )
    args = parser.parse_args()

    try:
        output = flash_and_capture(
            args.peripheral, args.host, args.port, args.duration
        )
    except FileNotFoundError as e:
        print(f"ERROR: {e}")
        print("Run tools/build_all_firmware.py first")
        return 1
    except subprocess.CalledProcessError as e:
        print(f"ERROR: Command failed: {e}")
        print(f"Is {args.host} accessible? Is ESP32-C3 connected?")
        return 1

    # Save baseline
    baseline_dir = (
        REPO_ROOT / "peripherals" / args.peripheral / "baselines"
    )
    baseline_dir.mkdir(parents=True, exist_ok=True)
    baseline_file = baseline_dir / "hardware.log"
    baseline_file.write_text(output)

    print(f"\nBaseline saved: {baseline_file}")
    print(f"Lines: {len(output.splitlines())}")

    # Show preview
    print("\n=== Preview ===")
    for line in output.splitlines()[:20]:
        print(f"  {line}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
