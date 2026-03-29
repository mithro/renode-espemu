/**
 * test_efuse_blk1_spi_config — ESP32-C3 efuse peripheral test
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
    printf("[EFUSE] === test_efuse_blk1_spi_config ===\n");

    print_reg("EFUSE_RD_MAC_SPI_SYS_0_REG", EFUSE_RD_MAC_SPI_SYS_0_REG);
    print_reg("EFUSE_RD_MAC_SPI_SYS_1_REG", EFUSE_RD_MAC_SPI_SYS_1_REG);
    print_reg("EFUSE_RD_MAC_SPI_SYS_2_REG", EFUSE_RD_MAC_SPI_SYS_2_REG);
    print_reg("EFUSE_RD_MAC_SPI_SYS_3_REG", EFUSE_RD_MAC_SPI_SYS_3_REG);
    print_reg("EFUSE_RD_MAC_SPI_SYS_4_REG", EFUSE_RD_MAC_SPI_SYS_4_REG);
    print_reg("EFUSE_RD_MAC_SPI_SYS_5_REG", EFUSE_RD_MAC_SPI_SYS_5_REG);

    printf("[EFUSE] TEST_PASS\n");

    printf("[EFUSE] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
