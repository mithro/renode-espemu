/**
 * test_efuse_key_blocks — ESP32-C3 efuse peripheral test
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
    printf("[EFUSE] === test_efuse_key_blocks ===\n");

    #define DR_REG_EFUSE_BASE 0x60008800
    #define EFUSE_RD_KEY0_DATA0_ADDR (DR_REG_EFUSE_BASE + 0x9C)
    #define EFUSE_KEY_BLOCK_STRIDE 0x20  /* 8 regs * 4 bytes = 0x20 per block */

    for (int block = 0; block < 6; block++) {
        printf("[EFUSE] --- KEY%d ---\n", block);
        for (int reg = 0; reg < 8; reg++) {
            uint32_t addr = EFUSE_RD_KEY0_DATA0_ADDR + block * EFUSE_KEY_BLOCK_STRIDE + reg * 4;
            char name[48];
            snprintf(name, sizeof(name), "EFUSE_RD_KEY%d_DATA%d_REG", block, reg);
            print_reg(name, addr);
        }
    }

    printf("[EFUSE] TEST_PASS\n");

    printf("[EFUSE] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
