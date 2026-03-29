/**
 * test_efuse_control_regs — ESP32-C3 efuse peripheral test
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
    printf("[EFUSE] === test_efuse_control_regs ===\n");

    #define DR_REG_EFUSE_BASE 0x60008800

    print_reg("EFUSE_CLK_REG",          DR_REG_EFUSE_BASE + 0x1C8);
    print_reg("EFUSE_CONF_REG",         DR_REG_EFUSE_BASE + 0x1CC);
    print_reg("EFUSE_STATUS_REG",       DR_REG_EFUSE_BASE + 0x1D0);
    print_reg("EFUSE_CMD_REG",          DR_REG_EFUSE_BASE + 0x1D4);
    print_reg("EFUSE_DAC_CONF_REG",     DR_REG_EFUSE_BASE + 0x1D8);
    print_reg("EFUSE_RD_TIM_CONF_REG",  DR_REG_EFUSE_BASE + 0x1DC);
    print_reg("EFUSE_WR_TIM_CONF1_REG", DR_REG_EFUSE_BASE + 0x1E4);
    print_reg("EFUSE_WR_TIM_CONF2_REG", DR_REG_EFUSE_BASE + 0x1E8);
    print_reg("EFUSE_DATE_REG",         DR_REG_EFUSE_BASE + 0x1FC);

    printf("[EFUSE] TEST_PASS\n");

    printf("[EFUSE] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
