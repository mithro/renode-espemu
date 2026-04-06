#!/usr/bin/env python3
"""Run all Robot Framework tests in Renode.

Usage:
    uv run tools/run_renode_tests.py

Environment variables:
    RENODE_ESPEMU_BASE  - repo root (auto-detected if not set)
    ROM_ELF             - path to esp32c3_rev3_rom.elf
"""

import os
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
RENODE_TEST_VENV = Path.home() / ".venvs" / "renode-test"

# Default ROM ELF location
DEFAULT_ROM_ELF = Path.home() / "esp" / "esp-rom-elfs" / "esp32c3_rev3_rom.elf"

# All Robot test files
ROBOT_TESTS = [
    REPO_ROOT / "hello_world" / "test.robot",
    REPO_ROOT / "peripherals" / "efuse" / "test.robot",
    REPO_ROOT / "peripherals" / "rng" / "test.robot",
    REPO_ROOT / "peripherals" / "gpio" / "test.robot",
    REPO_ROOT / "peripherals" / "system" / "test.robot",
    REPO_ROOT / "peripherals" / "systimer" / "test.robot",
    REPO_ROOT / "peripherals" / "timer-group" / "test.robot",
]


def run_all_tests() -> int:
    """Run all Robot tests via renode-test. Returns exit code."""
    env = os.environ.copy()
    env["PATH"] = f"{RENODE_TEST_VENV / 'bin'}:{env['PATH']}"

    # Auto-detect paths
    base = os.environ.get("RENODE_ESPEMU_BASE", str(REPO_ROOT))
    rom_elf = os.environ.get("ROM_ELF", str(DEFAULT_ROM_ELF))

    if not Path(rom_elf).exists():
        print(f"ERROR: ROM ELF not found: {rom_elf}")
        print("Set ROM_ELF environment variable to the correct path")
        return 1

    test_args = [str(t) for t in ROBOT_TESTS if t.exists()]
    if not test_args:
        print("ERROR: No Robot test files found")
        return 1

    print(f"Running {len(test_args)} test suites...")
    print(f"  Base: {base}")
    print(f"  ROM ELF: {rom_elf}")
    print()

    result = subprocess.run(
        ["renode-test",
         "--variable", f"BASE:{base}",
         "--variable", f"ROM_ELF:{rom_elf}",
         ] + test_args,
        cwd=str(REPO_ROOT),
        env=env,
    )

    return result.returncode


def main():
    return run_all_tests()


if __name__ == "__main__":
    sys.exit(main())
