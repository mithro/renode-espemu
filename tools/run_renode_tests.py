#!/usr/bin/env python3
"""Run all Robot Framework tests in Renode and capture structured output.

Usage:
    uv run tools/run_renode_tests.py [--capture]

With --capture, also saves the full UART output from each test to
peripherals/<name>/baselines/renode.log for comparison with hardware.
"""

import os
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
RENODE_TEST_VENV = Path.home() / ".venvs" / "renode-test"

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

    test_args = [str(t) for t in ROBOT_TESTS if t.exists()]
    if not test_args:
        print("ERROR: No Robot test files found")
        return 1

    print(f"Running {len(test_args)} test suites...")
    print()

    result = subprocess.run(
        ["renode-test"] + test_args,
        cwd=str(REPO_ROOT),
        env=env,
    )

    return result.returncode


def main():
    return run_all_tests()


if __name__ == "__main__":
    sys.exit(main())
