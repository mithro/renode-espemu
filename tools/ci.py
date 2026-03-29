#!/usr/bin/env python3
"""ESP32-C3 Renode emulation CI pipeline.

Builds all firmware, runs all Renode Robot tests, optionally captures
hardware baselines and compares output.

Usage:
    uv run tools/ci.py                  # build + test (Renode only)
    uv run tools/ci.py --hardware       # also flash to rpi4-esp + compare
    uv run tools/ci.py --clean          # clean build first
    uv run tools/ci.py --skip-build     # only run tests (firmware already built)

Exit code 0 = all steps pass, 1 = failures.
"""

import argparse
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent


def step(name: str):
    """Print a step header."""
    print(f"\n{'='*60}")
    print(f"  {name}")
    print(f"{'='*60}\n")


def run(cmd: list[str], **kwargs) -> int:
    """Run a command, return exit code."""
    result = subprocess.run(cmd, **kwargs)
    return result.returncode


def build_all(clean: bool = False) -> bool:
    step("Step 1: Build all test firmware")
    args = [sys.executable, str(REPO_ROOT / "tools" / "build_all_firmware.py")]
    if clean:
        args.append("--clean")
    return run(args) == 0


def run_renode_tests() -> bool:
    step("Step 2: Run all Renode Robot Framework tests")
    return run([sys.executable, str(REPO_ROOT / "tools" / "run_renode_tests.py")]) == 0


def capture_hardware(peripherals: list[str], host: str) -> bool:
    step("Step 3: Capture hardware baselines")
    all_ok = True
    for p in peripherals:
        print(f"\n--- {p} ---")
        rc = run([
            sys.executable,
            str(REPO_ROOT / "tools" / "capture_hardware_baseline.py"),
            p, "--host", host,
        ])
        if rc != 0:
            print(f"  FAIL: {p} hardware capture failed")
            all_ok = False
    return all_ok


def compare_all(peripherals: list[str]) -> bool:
    step("Step 4: Compare hardware vs Renode output")
    all_ok = True
    for p in peripherals:
        hw_log = REPO_ROOT / "peripherals" / p / "baselines" / "hardware.log"
        renode_log = REPO_ROOT / "peripherals" / p / "baselines" / "renode.log"
        diff_file = REPO_ROOT / "peripherals" / p / "baselines" / "comparison.diff"

        if not hw_log.exists():
            print(f"  SKIP {p}: no hardware baseline")
            continue
        if not renode_log.exists():
            print(f"  SKIP {p}: no Renode baseline")
            continue

        rc = run([
            sys.executable,
            str(REPO_ROOT / "tools" / "compare_output.py"),
            "--hardware", str(hw_log),
            "--renode", str(renode_log),
            "--output", str(diff_file),
        ])
        if rc != 0:
            all_ok = False
    return all_ok


def main():
    parser = argparse.ArgumentParser(description="ESP32-C3 CI pipeline")
    parser.add_argument("--clean", action="store_true", help="Clean build")
    parser.add_argument("--skip-build", action="store_true", help="Skip build step")
    parser.add_argument("--hardware", action="store_true", help="Include hardware capture + compare")
    parser.add_argument("--host", default="rpi4-esp", help="Hardware target host")
    args = parser.parse_args()

    peripherals = ["efuse", "rng", "gpio", "system", "systimer", "timer-group"]
    results = {}

    if not args.skip_build:
        results["build"] = build_all(args.clean)
        if not results["build"]:
            print("\nBuild failed, stopping.")
            return 1

    results["renode_tests"] = run_renode_tests()

    if args.hardware:
        results["hardware_capture"] = capture_hardware(peripherals, args.host)
        results["comparison"] = compare_all(peripherals)

    step("Summary")
    all_pass = True
    for name, ok in results.items():
        status = "PASS" if ok else "FAIL"
        print(f"  {status}  {name}")
        if not ok:
            all_pass = False

    if all_pass:
        print("\nAll steps passed!")
    else:
        print("\nSome steps failed.")

    return 0 if all_pass else 1


if __name__ == "__main__":
    sys.exit(main())
