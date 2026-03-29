/**
 * test_system_sysclk_conf — ESP32-C3 system peripheral test
 *
 * Tests a specific feature of the system peripheral.
 * Output format: [SYSTEM] REG_READ addr=0xNNN val=0xNNN register_name
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "soc/system_reg.h"

#define REG_READ_RAW(addr) (*((volatile uint32_t *)(addr)))
#define REG_WRITE_RAW(addr, val) (*((volatile uint32_t *)(addr)) = (val))

static void print_reg(const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[SYSTEM] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

void app_main(void)
{
    printf("[SYSTEM] === test_system_sysclk_conf ===\n");

    uint32_t val = REG_READ_RAW(SYSTEM_SYSCLK_CONF_REG);
    printf("[SYSTEM] REG_READ  addr=0x%08lx val=0x%08lx  SYSTEM_SYSCLK_CONF_REG\n",
           (unsigned long)SYSTEM_SYSCLK_CONF_REG, (unsigned long)val);

    uint32_t clk_xtal_freq = (val >> 12) & 0x7F;
    uint32_t soc_clk_sel   = (val >> 10) & 0x3;
    uint32_t pre_div_cnt   = val & 0x3FF;

    printf("[SYSTEM] DECODE   CLK_XTAL_FREQ[18:12]=%lu  SOC_CLK_SEL[11:10]=%lu  PRE_DIV_CNT[9:0]=%lu\n",
           (unsigned long)clk_xtal_freq,
           (unsigned long)soc_clk_sel,
           (unsigned long)pre_div_cnt);

    printf("[SYSTEM] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
