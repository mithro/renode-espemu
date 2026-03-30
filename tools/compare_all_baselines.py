#!/usr/bin/env python3
"""Compare hardware vs Renode baselines for all per-feature tests.

Extracts structured [TAG] REG_READ/TEST_PASS/TEST_FAIL lines and diffs them.

Usage:
    python3 tools/compare_all_baselines.py [--peripheral NAME] [--verbose]
"""

import argparse
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
PERIPHERALS_DIR = REPO_ROOT / "peripherals"

# Match structured output lines
STRUCTURED_RE = re.compile(r"^\[([A-Z_]+)\]\s+(.*)$")


def extract_structured(text: str) -> list[str]:
    """Extract [TAG] lines, stripping boot messages and timestamps."""
    lines = []
    for line in text.splitlines():
        line = line.strip()
        # Strip ANSI escapes
        line = re.sub(r"\x1b\[[0-9;]*m", "", line)
        # Strip Renode timestamp prefix
        line = re.sub(r"^\[host:.*?\]\s*", "", line)
        # Strip ESP-IDF boot prefix (I (NNN) tag:)
        line = re.sub(r"^[IWE] \(\d+\) \w+: ", "", line)
        m = STRUCTURED_RE.match(line)
        if m:
            lines.append(line)
    return lines


def compare_test(hw_file: Path, renode_file: Path, verbose: bool = False) -> dict:
    """Compare one test's hardware vs Renode output."""
    hw_lines = extract_structured(hw_file.read_text())
    renode_lines = extract_structured(renode_file.read_text())

    # Compare REG_READ lines (the core comparison)
    hw_reads = [l for l in hw_lines if "REG_READ" in l]
    rn_reads = [l for l in renode_lines if "REG_READ" in l]

    # Compare TEST_PASS/FAIL
    hw_pass = {l for l in hw_lines if "TEST_PASS" in l}
    hw_fail = {l for l in hw_lines if "TEST_FAIL" in l}
    rn_pass = {l for l in renode_lines if "TEST_PASS" in l}
    rn_fail = {l for l in renode_lines if "TEST_FAIL" in l}

    # Find register value differences
    reg_diffs = []
    for hw_r, rn_r in zip(hw_reads, rn_reads):
        if hw_r != rn_r:
            reg_diffs.append((hw_r, rn_r))

    # Count mismatches in length
    len_diff = len(hw_reads) - len(rn_reads)

    # Regressions: pass on hardware, fail on Renode
    regressions = hw_pass - rn_pass

    result = {
        "hw_reads": len(hw_reads),
        "rn_reads": len(rn_reads),
        "reg_diffs": reg_diffs,
        "len_diff": len_diff,
        "hw_pass": len(hw_pass),
        "hw_fail": len(hw_fail),
        "rn_pass": len(rn_pass),
        "rn_fail": len(rn_fail),
        "regressions": regressions,
        "match": len(reg_diffs) == 0 and len_diff == 0 and len(regressions) == 0,
    }

    if verbose and not result["match"]:
        print(f"    HW reads: {len(hw_reads)}, Renode reads: {len(rn_reads)}")
        if reg_diffs:
            print(f"    Register value diffs ({len(reg_diffs)}):")
            for hw_r, rn_r in reg_diffs[:5]:
                print(f"      HW:  {hw_r}")
                print(f"      RN:  {rn_r}")
            if len(reg_diffs) > 5:
                print(f"      ... and {len(reg_diffs) - 5} more")
        if regressions:
            print(f"    Regressions: {regressions}")

    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--peripheral", help="Only compare this peripheral")
    parser.add_argument("--verbose", "-v", action="store_true")
    args = parser.parse_args()

    # Discover all test pairs
    tests = []
    for periph_dir in sorted(PERIPHERALS_DIR.iterdir()):
        if not periph_dir.is_dir():
            continue
        if args.peripheral and periph_dir.name != args.peripheral:
            continue
        hw_dir = periph_dir / "baselines" / "hardware"
        rn_dir = periph_dir / "baselines" / "renode"
        if not hw_dir.is_dir() or not rn_dir.is_dir():
            continue
        for hw_file in sorted(hw_dir.glob("*.log")):
            rn_file = rn_dir / hw_file.name
            if rn_file.exists():
                tests.append((periph_dir.name, hw_file.stem, hw_file, rn_file))

    print(f"Comparing {len(tests)} test baselines\n")

    match_count = 0
    diff_count = 0
    total_reg_diffs = 0

    for periph, test_name, hw_file, rn_file in tests:
        result = compare_test(hw_file, rn_file, args.verbose)
        if result["match"]:
            match_count += 1
            status = "MATCH"
        else:
            diff_count += 1
            status = "DIFF"
            total_reg_diffs += len(result["reg_diffs"]) + abs(result["len_diff"])

        symbol = "." if result["match"] else "X"
        detail = ""
        if not result["match"]:
            parts = []
            if result["reg_diffs"]:
                parts.append(f"{len(result['reg_diffs'])} reg diffs")
            if result["len_diff"]:
                parts.append(f"len diff {result['len_diff']}")
            if result["regressions"]:
                parts.append(f"{len(result['regressions'])} regressions")
            detail = f" ({', '.join(parts)})"
        print(f"  {symbol} {test_name}{detail}")

    print(f"\n{'='*50}")
    print(f"  MATCH: {match_count}/{len(tests)}")
    print(f"  DIFF:  {diff_count}/{len(tests)}")
    print(f"  Total register differences: {total_reg_diffs}")

    return 0 if diff_count == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
