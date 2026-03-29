/**
 * test_efuse_chip_revision — ESP32-C3 efuse peripheral test
 *
 * Tests a specific feature of the efuse peripheral.
 * Output format: [EFUSE] REG_READ addr=0xNNN val=0xNNN register_name
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "soc/efuse_reg.h"

#define REG_READ_RAW(addr) (*((volatile uint32_t *)(addr)))
#define REG_WRITE_RAW(addr, val) (*((volatile uint32_t *)(addr)) = (val))

static void print_reg(const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[EFUSE] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

void app_main(void)
{
    printf("[EFUSE] === test_efuse_chip_revision ===\n");

    print_reg("EFUSE_RD_MAC_SPI_SYS_3_REG", EFUSE_RD_MAC_SPI_SYS_3_REG);
    print_reg("EFUSE_RD_MAC_SPI_SYS_5_REG", EFUSE_RD_MAC_SPI_SYS_5_REG);

    uint32_t sys3 = REG_READ_RAW(EFUSE_RD_MAC_SPI_SYS_3_REG);
    uint32_t sys5 = REG_READ_RAW(EFUSE_RD_MAC_SPI_SYS_5_REG);

    uint32_t wafer_ver_minor_lo = (sys3 >> 18) & 0x7;  /* bits 20:18 */
    uint32_t pkg_version        = (sys3 >> 21) & 0x7;  /* bits 23:21 */
    uint32_t wafer_ver_major    = (sys5 >> 24) & 0x3;  /* bits 25:24 */

    printf("[EFUSE] WAFER_VERSION_MINOR_LO = %lu\n", (unsigned long)wafer_ver_minor_lo);
    printf("[EFUSE] PKG_VERSION            = %lu\n", (unsigned long)pkg_version);
    printf("[EFUSE] WAFER_VERSION_MAJOR    = %lu\n", (unsigned long)wafer_ver_major);
    printf("[EFUSE] Chip revision: %lu.%lu\n",
           (unsigned long)wafer_ver_major, (unsigned long)wafer_ver_minor_lo);

    if (wafer_ver_major != 0 || wafer_ver_minor_lo != 0 || pkg_version != 0) {
        printf("[EFUSE] TEST_PASS\n");
    } else {
        printf("[EFUSE] TEST_FAIL: chip revision fields are all zero\n");
    }

    printf("[EFUSE] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
