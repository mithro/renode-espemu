#!/usr/bin/env python3
"""Measure ESP32-C3 boot progress in Renode.

Runs the hello_world firmware in Renode, captures UART output and
peripheral access logs, and reports a boot progress score (0-5).

Score levels:
  0 - CPU hangs immediately (no output at all)
  1 - CPU runs but hits unhandled peripherals early (ROM stage)
  2 - Boot ROM starts executing (eFuse/RTC reads visible)
  3 - Bootloader starts (clock config, cache/MMU, SPI flash)
  4 - Application starts (FreeRTOS, timers, "boot:" messages)
  5 - "Hello world!" appears in UART output

Usage:
  python3 tools/measure_boot_progress.py [--timeout SECS] [--firmware PATH]
"""

import argparse
import os
import re
import subprocess
import sys
import tempfile


def run_renode(repl_path, firmware_path, timeout_secs, log_path):
    """Run firmware in Renode, capture output. Returns (uart_output, log_output)."""
    # Build the Renode command script
    resc_commands = f"""
using sysbus
mach create "esp32c3"
machine LoadPlatformDescription @{repl_path}
logFile @{log_path}
logLevel -1
sysbus LoadELF @{firmware_path}
showAnalyzer uart0
start
sleep {timeout_secs}
quit
"""
    # Write temp resc file
    resc_path = os.path.join(os.path.dirname(log_path), "boot_test.resc")
    with open(resc_path, "w") as f:
        # resc files can't start with comments, write commands directly
        for line in resc_commands.strip().split("\n"):
            line = line.strip()
            if line:
                f.write(line + "\n")

    try:
        result = subprocess.run(
            ["renode", "--console", "--disable-xwt", "-e", f"include @{resc_path}"],
            capture_output=True,
            timeout=timeout_secs + 30,
            text=True,
        )
        uart_output = result.stdout
    except subprocess.TimeoutExpired:
        uart_output = ""

    # Read log file
    log_output = ""
    if os.path.exists(log_path):
        with open(log_path) as f:
            log_output = f.read()

    # Clean up temp files
    if os.path.exists(resc_path):
        os.unlink(resc_path)

    return uart_output, log_output


def score_boot(uart_output, log_output):
    """Score boot progress 0-5 based on output."""
    score = 0
    details = []

    # Score 1: Any peripheral access logged (CPU is running)
    if "ReadDoubleWord" in log_output or "WriteDoubleWord" in log_output:
        score = 1
        details.append("CPU executing, peripheral accesses detected")

    # Score 2: eFuse or RTC reads visible (ROM is running)
    if "0x60008" in log_output:  # eFuse or RTC address range
        score = 2
        details.append("ROM stage: eFuse/RTC register accesses")

    # Score 3: Cache/MMU or SPI flash activity (bootloader loading)
    if "0x600c4" in log_output or "0x60002" in log_output:
        score = 3
        details.append("Bootloader stage: cache/SPI flash activity")

    # Score 4: "boot:" messages in UART (application starting)
    if "boot:" in uart_output or "boot.esp32c3:" in uart_output:
        score = 4
        details.append("Application starting: boot messages in UART")

    # Score 5: Hello world!
    if "Hello world!" in uart_output:
        score = 5
        details.append("SUCCESS: Hello world! in UART output")

    # Find first unhandled access
    first_unhandled = None
    unhandled_pattern = re.compile(
        r"(Read|Write)DoubleWord (?:from|to) non existing peripheral at (0x[0-9A-Fa-f]+)"
    )
    for match in unhandled_pattern.finditer(log_output):
        first_unhandled = match.group(2)
        break

    return score, details, first_unhandled


def main():
    parser = argparse.ArgumentParser(description="Measure ESP32-C3 boot progress in Renode")
    parser.add_argument("--timeout", type=int, default=10, help="Emulation time in seconds")
    parser.add_argument(
        "--firmware",
        default="hello_world/firmware/hello_world.elf",
        help="Path to firmware ELF or binary",
    )
    parser.add_argument(
        "--platform",
        default="platforms/cpus/esp32c3.repl",
        help="Path to .repl platform file",
    )
    args = parser.parse_args()

    if not os.path.exists(args.firmware):
        print(f"ERROR: Firmware not found: {args.firmware}")
        sys.exit(1)

    if not os.path.exists(args.platform):
        print(f"ERROR: Platform file not found: {args.platform}")
        sys.exit(1)

    # Use a temp directory for logs
    log_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "tmp")
    os.makedirs(log_dir, exist_ok=True)
    log_path = os.path.join(log_dir, "renode_boot_test.log")

    print(f"Running firmware in Renode for {args.timeout}s...")
    uart_output, log_output = run_renode(args.platform, args.firmware, args.timeout, log_path)

    score, details, first_unhandled = score_boot(uart_output, log_output)

    print(f"\n=== Boot Progress Score: {score}/5 ===")
    for detail in details:
        print(f"  {detail}")

    if first_unhandled:
        print(f"\nFirst unhandled register access: {first_unhandled}")

    # Count unhandled accesses
    unhandled_count = log_output.count("non existing peripheral")
    if unhandled_count:
        print(f"Total unhandled accesses: {unhandled_count}")

    # Summary line for machine parsing
    print(f"\nBOOT_SCORE={score}")
    if first_unhandled:
        print(f"FIRST_UNHANDLED={first_unhandled}")

    return score


if __name__ == "__main__":
    sys.exit(0 if main() == 5 else 1)
