#!/usr/bin/env python3
"""Create reset-value test firmware for all remaining stub peripherals.

Each test reads 16 registers from the peripheral's base address and
prints structured output for hardware baseline comparison.
"""

import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent

# Stub peripherals: (name, base_address, tag, size_to_read)
STUBS = [
    ("spi0", 0x60003000, "SPI0", 16),
    ("fe2", 0x60005000, "FE2", 16),
    ("fe", 0x60006000, "FE", 16),
    ("rtc-i2c", 0x6000E000, "RTCI2C", 16),
    ("nrx", 0x6001C000, "NRX", 16),
    ("bb", 0x6001E000, "BB", 16),
    ("uart1", 0x60010000, "UART1", 16),
    ("i2c", 0x60013000, "I2C", 16),
    ("uhci0", 0x60014000, "UHCI0", 16),
    ("rmt", 0x60016000, "RMT", 16),
    ("ledc", 0x60019000, "LEDC", 16),
    ("spi2", 0x60024000, "SPI2", 16),
    ("twai", 0x6002B000, "TWAI", 16),
    ("i2s", 0x6002D000, "I2S", 16),
    ("aes", 0x6003A000, "AES", 16),
    ("sha", 0x6003B000, "SHA", 16),
    ("rsa", 0x6003C000, "RSA", 16),
    ("ds", 0x6003D000, "DS", 16),
    ("hmac", 0x6003E000, "HMAC", 16),
    ("gdma", 0x6003F000, "GDMA", 16),
    ("adc", 0x60040000, "ADC", 16),
    ("usb-jtag", 0x60043000, "USB", 16),
    ("sensitive", 0x600C1000, "SENS", 16),
    ("mmu", 0x600C5000, "MMU", 16),
    ("xts-aes", 0x600CC000, "XTSAES", 16),
    ("assist-debug", 0x600CE000, "ADBG", 16),
    ("world-ctrl", 0x600D0000, "WC", 16),
]


def create_test(name: str, base: int, tag: str, num_regs: int):
    test_name = f"test_{name.replace('-', '_')}_reset_values"
    fw_dir = REPO_ROOT / "peripherals" / "stubs" / "firmware" / test_name
    main_dir = fw_dir / "main"
    main_dir.mkdir(parents=True, exist_ok=True)

    # CMakeLists.txt
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

    # main/<test_name>.c
    (main_dir / f"{test_name}.c").write_text(
        f'/**\n'
        f' * {test_name} — Read reset values from {name} peripheral at 0x{base:08X}\n'
        f' */\n'
        f'#include <stdio.h>\n'
        f'#include <stdint.h>\n'
        f'#include "freertos/FreeRTOS.h"\n'
        f'#include "freertos/task.h"\n'
        f'\n'
        f'#define BASE 0x{base:08X}u\n'
        f'#define REG_READ(addr) (*((volatile uint32_t *)(addr)))\n'
        f'\n'
        f'void app_main(void)\n'
        f'{{\n'
        f'    printf("[{tag}] === {name} reset values (base=0x{base:08X}) ===\\n");\n'
        f'    for (int i = 0; i < {num_regs}; i++) {{\n'
        f'        uint32_t addr = BASE + i * 4;\n'
        f'        uint32_t val = REG_READ(addr);\n'
        f'        printf("[{tag}] REG_READ  addr=0x%08lx val=0x%08lx  offset_0x%02x\\n",\n'
        f'               (unsigned long)addr, (unsigned long)val, i * 4);\n'
        f'    }}\n'
        f'    printf("[{tag}] === Done ===\\n");\n'
        f'    while (1) {{ vTaskDelay(1000 / portTICK_PERIOD_MS); }}\n'
        f'}}\n'
    )

    print(f"  Created {test_name}")


def main():
    print(f"Creating {len(STUBS)} stub test firmware")
    for name, base, tag, num_regs in STUBS:
        create_test(name, base, tag, num_regs)
    print(f"\nDone. {len(STUBS)} test firmware created under peripherals/stubs/firmware/")


if __name__ == "__main__":
    main()
