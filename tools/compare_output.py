#!/usr/bin/env python3
"""Compare UART output between Renode and hardware baseline.

Compares structured output lines (lines starting with [PERIPH] or standard
boot messages) between two log files and reports differences.

Usage:
  python3 tools/compare_output.py --renode renode.log --hardware hardware.log
  python3 tools/compare_output.py --renode renode.log --hardware hardware.log --output diff.txt
"""

import argparse
import difflib
import sys


def normalize_line(line):
    """Normalize a line for comparison (strip timestamps, whitespace)."""
    line = line.strip()
    # Strip ESP-IDF log timestamp prefix: "I (123) tag: "
    # Keep the content after the tag
    if line and line[0] in "IWEDV" and line[1:3] == " (":
        paren_end = line.find(")")
        if paren_end > 0:
            # Keep from the tag onwards
            colon = line.find(":", paren_end)
            if colon > 0:
                line = line[colon + 1:].strip()
    return line


def filter_meaningful_lines(lines):
    """Filter to lines that are meaningful for comparison."""
    result = []
    for line in lines:
        line = line.strip()
        if not line:
            continue
        # Keep structured test output
        if line.startswith("["):
            result.append(line)
            continue
        # Keep boot messages
        if any(kw in line for kw in ["boot:", "Hello world", "ESP-ROM:", "rst:", "entry "]):
            result.append(normalize_line(line))
            continue
        # Keep ESP-IDF log lines
        if line and line[0] in "IWEDV" and line[1:3] == " (":
            result.append(normalize_line(line))
            continue
    return result


def compare(renode_path, hardware_path, output_path=None):
    """Compare two log files. Returns True if they match."""
    with open(renode_path) as f:
        renode_lines = filter_meaningful_lines(f.readlines())
    with open(hardware_path) as f:
        hw_lines = filter_meaningful_lines(f.readlines())

    diff = list(difflib.unified_diff(
        hw_lines, renode_lines,
        fromfile="hardware", tofile="renode",
        lineterm=""
    ))

    if output_path:
        with open(output_path, "w") as f:
            f.write("\n".join(diff) + "\n" if diff else "")

    if not diff:
        print("MATCH: Renode output matches hardware baseline")
        return True
    else:
        print(f"DIFF: {len(diff)} lines differ between Renode and hardware")
        print(f"  Hardware lines: {len(hw_lines)}")
        print(f"  Renode lines: {len(renode_lines)}")
        print()
        # Show first 20 diff lines
        for line in diff[:20]:
            print(f"  {line}")
        if len(diff) > 20:
            print(f"  ... ({len(diff) - 20} more lines)")
        return False


def main():
    parser = argparse.ArgumentParser(description="Compare UART output")
    parser.add_argument("--renode", required=True, help="Renode output log")
    parser.add_argument("--hardware", required=True, help="Hardware baseline log")
    parser.add_argument("--output", help="Write diff to file")
    args = parser.parse_args()

    match = compare(args.renode, args.hardware, args.output)
    sys.exit(0 if match else 1)


if __name__ == "__main__":
    main()
