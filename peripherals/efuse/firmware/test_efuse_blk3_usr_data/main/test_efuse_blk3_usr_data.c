/**
 * test_efuse_blk3_usr_data — ESP32-C3 efuse peripheral test
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
    printf("[EFUSE] === test_efuse_blk3_usr_data ===\n");

    #define DR_REG_EFUSE_BASE 0x60008800
    #define EFUSE_RD_USR_DATA_BASE (DR_REG_EFUSE_BASE + 0x7C)

    for (int i = 0; i < 8; i++) {
        uint32_t addr = EFUSE_RD_USR_DATA_BASE + i * 4;
        char name[40];
        snprintf(name, sizeof(name), "EFUSE_RD_USR_DATA%d_REG", i);
        print_reg(name, addr);
    }

    printf("[EFUSE] TEST_PASS\n");

    printf("[EFUSE] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
