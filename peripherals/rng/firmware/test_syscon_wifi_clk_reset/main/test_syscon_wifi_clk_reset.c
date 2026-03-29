/**
 * test_syscon_wifi_clk_reset — ESP32-C3 rng peripheral test
 *
 * Tests a specific feature of the rng peripheral.
 * Output format: [SYSCON] REG_READ addr=0xNNN val=0xNNN register_name
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "soc/syscon_reg.h"

#define REG_READ_RAW(addr) (*((volatile uint32_t *)(addr)))
#define REG_WRITE_RAW(addr, val) (*((volatile uint32_t *)(addr)) = (val))

static void print_reg(const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[SYSCON] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

void app_main(void)
{
    printf("[SYSCON] === test_syscon_wifi_clk_reset ===\n");

    print_reg("SYSCON_WIFI_CLK_EN_REG", SYSCON_WIFI_CLK_EN_REG);
    print_reg("SYSCON_WIFI_RST_EN_REG", SYSCON_WIFI_RST_EN_REG);

    printf("[SYSCON] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
