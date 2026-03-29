#!/usr/bin/env python3
"""Create a new ESP-IDF test firmware scaffold for a peripheral feature.

Usage:
    python3 tools/create_test_firmware.py <peripheral> <test_name>

Example:
    python3 tools/create_test_firmware.py efuse test_efuse_blk0_reset_values

Creates:
    peripherals/<peripheral>/firmware/<test_name>/
    ├── CMakeLists.txt
    ├── sdkconfig.defaults
    └── main/
        ├── CMakeLists.txt
        └── <test_name>.c  (template with structured output)
"""

import argparse
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent


def create_firmware(peripheral: str, test_name: str, tag: str):
    fw_dir = REPO_ROOT / "peripherals" / peripheral / "firmware" / test_name
    main_dir = fw_dir / "main"
    main_dir.mkdir(parents=True, exist_ok=True)

    # Top-level CMakeLists.txt
    (fw_dir / "CMakeLists.txt").write_text(
        f'cmake_minimum_required(VERSION 3.16)\n'
        f'include($ENV{{IDF_PATH}}/tools/cmake/project.cmake)\n'
        f'project({test_name})\n'
    )

    # sdkconfig.defaults
    (fw_dir / "sdkconfig.defaults").write_text(
        'CONFIG_IDF_TARGET="esp32c3"\n'
        'CONFIG_ESPTOOLPY_FLASHSIZE_4MB=y\n'
    )

    # main/CMakeLists.txt
    (main_dir / "CMakeLists.txt").write_text(
        f'idf_component_register(SRCS "{test_name}.c"\n'
        f'                       INCLUDE_DIRS ".")\n'
    )

    # main/<test_name>.c (template)
    (main_dir / f"{test_name}.c").write_text(
        f'/**\n'
        f' * {test_name} — ESP32-C3 {peripheral} peripheral test\n'
        f' *\n'
        f' * Tests a specific feature of the {peripheral} peripheral.\n'
        f' * Output format: [{tag}] REG_READ addr=0xNNN val=0xNNN register_name\n'
        f' */\n'
        f'#include <stdio.h>\n'
        f'#include <stdint.h>\n'
        f'#include "freertos/FreeRTOS.h"\n'
        f'#include "freertos/task.h"\n'
        f'\n'
        f'#define REG_READ_RAW(addr) (*((volatile uint32_t *)(addr)))\n'
        f'#define REG_WRITE_RAW(addr, val) (*((volatile uint32_t *)(addr)) = (val))\n'
        f'\n'
        f'static void print_reg(const char *name, uint32_t addr)\n'
        f'{{\n'
        f'    uint32_t val = REG_READ_RAW(addr);\n'
        f'    printf("[{tag}] REG_READ  addr=0x%08lx val=0x%08lx  %s\\n",\n'
        f'           (unsigned long)addr, (unsigned long)val, name);\n'
        f'}}\n'
        f'\n'
        f'void app_main(void)\n'
        f'{{\n'
        f'    printf("[{tag}] === {test_name} ===\\n");\n'
        f'\n'
        f'    // TODO: Add register reads/writes here\n'
        f'\n'
        f'    printf("[{tag}] === Done ===\\n");\n'
        f'\n'
        f'    while (1) {{ vTaskDelay(1000 / portTICK_PERIOD_MS); }}\n'
        f'}}\n'
    )

    print(f"Created {fw_dir}")


# Tag mapping
TAGS = {
    "efuse": "EFUSE",
    "rtc": "RTC",
    "interrupt-matrix": "INTMTX",
    "systimer": "SYSTMR",
    "timer-group": "TIMG",
    "system": "SYSTEM",
    "rng": "SYSCON",
    "gpio": "GPIO",
    "extmem": "EXTMEM",
    "uart": "UART",
}


def main():
    parser = argparse.ArgumentParser(description="Create test firmware scaffold")
    parser.add_argument("peripheral", help="Peripheral name")
    parser.add_argument("test_name", help="Test name (e.g., test_efuse_blk0_reset_values)")
    parser.add_argument("--tag", help="Output tag (default: auto from peripheral)")
    args = parser.parse_args()

    tag = args.tag or TAGS.get(args.peripheral, args.peripheral.upper())
    create_firmware(args.peripheral, args.test_name, tag)


if __name__ == "__main__":
    main()
