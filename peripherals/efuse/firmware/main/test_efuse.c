/**
 * ESP32-C3 eFuse Test Firmware
 *
 * Reads all eFuse read-data registers and prints values in structured format
 * for comparison between hardware, QEMU, and Renode.
 *
 * eFuse base: DR_REG_EFUSE_BASE = 0x60008800
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "soc/efuse_reg.h"

/* Direct register read macro */
#define REG_READ_RAW(addr) (*((volatile uint32_t *)(addr)))

static void print_reg(const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[EFUSE] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

void app_main(void)
{
    printf("[EFUSE] === ESP32-C3 eFuse Read Test ===\n");
    printf("[EFUSE] DR_REG_EFUSE_BASE = 0x%08lx\n", (unsigned long)DR_REG_EFUSE_BASE);

    /* --- BLK0: Write Disable + Repeat Data --- */
    printf("[EFUSE] === BLK0 Read Data ===\n");
    print_reg("RD_WR_DIS",        EFUSE_RD_WR_DIS_REG);
    print_reg("RD_REPEAT_DATA0",  EFUSE_RD_REPEAT_DATA0_REG);
    print_reg("RD_REPEAT_DATA1",  EFUSE_RD_REPEAT_DATA1_REG);
    print_reg("RD_REPEAT_DATA2",  EFUSE_RD_REPEAT_DATA2_REG);
    print_reg("RD_REPEAT_DATA3",  EFUSE_RD_REPEAT_DATA3_REG);
    print_reg("RD_REPEAT_DATA4",  EFUSE_RD_REPEAT_DATA4_REG);

    /* --- BLK1: MAC + SPI Config --- */
    printf("[EFUSE] === BLK1 MAC/SPI Read Data ===\n");
    print_reg("RD_MAC_SPI_SYS_0", EFUSE_RD_MAC_SPI_SYS_0_REG);
    print_reg("RD_MAC_SPI_SYS_1", EFUSE_RD_MAC_SPI_SYS_1_REG);
    print_reg("RD_MAC_SPI_SYS_2", EFUSE_RD_MAC_SPI_SYS_2_REG);
    print_reg("RD_MAC_SPI_SYS_3", EFUSE_RD_MAC_SPI_SYS_3_REG);
    print_reg("RD_MAC_SPI_SYS_4", EFUSE_RD_MAC_SPI_SYS_4_REG);
    print_reg("RD_MAC_SPI_SYS_5", EFUSE_RD_MAC_SPI_SYS_5_REG);

    /* --- BLK2: System Part 1 --- */
    printf("[EFUSE] === BLK2 SYS_PART1 Read Data ===\n");
    print_reg("RD_SYS_PART1_DATA0", EFUSE_RD_SYS_PART1_DATA0_REG);
    print_reg("RD_SYS_PART1_DATA1", EFUSE_RD_SYS_PART1_DATA1_REG);
    print_reg("RD_SYS_PART1_DATA2", EFUSE_RD_SYS_PART1_DATA2_REG);
    print_reg("RD_SYS_PART1_DATA3", EFUSE_RD_SYS_PART1_DATA3_REG);
    print_reg("RD_SYS_PART1_DATA4", EFUSE_RD_SYS_PART1_DATA4_REG);
    print_reg("RD_SYS_PART1_DATA5", EFUSE_RD_SYS_PART1_DATA5_REG);
    print_reg("RD_SYS_PART1_DATA6", EFUSE_RD_SYS_PART1_DATA6_REG);
    print_reg("RD_SYS_PART1_DATA7", EFUSE_RD_SYS_PART1_DATA7_REG);

    /* --- Decode key fields --- */
    printf("[EFUSE] === Decoded Fields ===\n");

    /* MAC address from BLK1 */
    uint32_t mac0 = REG_READ_RAW(EFUSE_RD_MAC_SPI_SYS_0_REG);
    uint32_t mac1 = REG_READ_RAW(EFUSE_RD_MAC_SPI_SYS_1_REG);
    printf("[EFUSE] MAC_ADDR=%02lx:%02lx:%02lx:%02lx:%02lx:%02lx\n",
           (unsigned long)((mac1 >> 8) & 0xFF),
           (unsigned long)((mac1 >> 0) & 0xFF),
           (unsigned long)((mac0 >> 24) & 0xFF),
           (unsigned long)((mac0 >> 16) & 0xFF),
           (unsigned long)((mac0 >> 8) & 0xFF),
           (unsigned long)((mac0 >> 0) & 0xFF));

    /* Chip revision from BLK1 */
    uint32_t sys3 = REG_READ_RAW(EFUSE_RD_MAC_SPI_SYS_3_REG);
    uint32_t sys5 = REG_READ_RAW(EFUSE_RD_MAC_SPI_SYS_5_REG);
    uint32_t wafer_minor_lo = (sys3 >> EFUSE_WAFER_VERSION_MINOR_LO_S) & EFUSE_WAFER_VERSION_MINOR_LO_V;
    uint32_t wafer_minor_hi = (sys5 >> 23) & 0x1;  /* bit 23 of SYS5 = WAFER_VERSION_MINOR_HI */
    uint32_t wafer_minor = (wafer_minor_hi << 3) | wafer_minor_lo;
    uint32_t wafer_major = (sys5 >> 24) & EFUSE_WAFER_VERSION_MAJOR_V;
    uint32_t pkg_version = (sys3 >> EFUSE_PKG_VERSION_S) & EFUSE_PKG_VERSION_V;
    printf("[EFUSE] WAFER_VERSION_MINOR=%lu\n", (unsigned long)wafer_minor);
    printf("[EFUSE] WAFER_VERSION_MAJOR=%lu\n", (unsigned long)wafer_major);
    printf("[EFUSE] PKG_VERSION=%lu\n", (unsigned long)pkg_version);
    printf("[EFUSE] CHIP_REVISION=v%lu.%lu\n", (unsigned long)wafer_major, (unsigned long)wafer_minor);

    /* Status register */
    print_reg("STATUS", EFUSE_STATUS_REG);
    print_reg("DATE",   EFUSE_DATE_REG);

    /* Test results */
    printf("[EFUSE] === Test Results ===\n");
    if (mac0 != 0 || (mac1 & 0xFFFF) != 0) {
        printf("[EFUSE] TEST_PASS mac_nonzero\n");
    } else {
        printf("[EFUSE] TEST_FAIL mac_nonzero expected=nonzero got=0\n");
    }

    if (wafer_major > 0 || wafer_minor > 0) {
        printf("[EFUSE] TEST_PASS chip_revision_nonzero\n");
    } else {
        printf("[EFUSE] TEST_FAIL chip_revision_nonzero expected=nonzero got=v0.0\n");
    }

    printf("[EFUSE] === Tests Complete ===\n");

    /* Keep running so FreeRTOS doesn't complain about task return */
    while (1) {
        vTaskDelay(1000 / portTICK_PERIOD_MS);
    }
}
