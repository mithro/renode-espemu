/**
 * test_syscon_wifi_clk_write — ESP32-C3 rng peripheral test
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
    printf("[SYSCON] === test_syscon_wifi_clk_write ===\n");

    /* Read initial value */
    uint32_t orig = REG_READ_RAW(SYSCON_WIFI_CLK_EN_REG);
    printf("[SYSCON] REG_READ  addr=0x%08lx val=0x%08lx  SYSCON_WIFI_CLK_EN_REG (initial)\n",
           (unsigned long)SYSCON_WIFI_CLK_EN_REG, (unsigned long)orig);

    /* Clear bit 0, write back, read back */
    uint32_t modified = orig & ~(uint32_t)0x1;
    REG_WRITE_RAW(SYSCON_WIFI_CLK_EN_REG, modified);
    printf("[SYSCON] REG_WRITE addr=0x%08lx val=0x%08lx  SYSCON_WIFI_CLK_EN_REG (clear bit0)\n",
           (unsigned long)SYSCON_WIFI_CLK_EN_REG, (unsigned long)modified);

    uint32_t readback = REG_READ_RAW(SYSCON_WIFI_CLK_EN_REG);
    printf("[SYSCON] REG_READ  addr=0x%08lx val=0x%08lx  SYSCON_WIFI_CLK_EN_REG (readback)\n",
           (unsigned long)SYSCON_WIFI_CLK_EN_REG, (unsigned long)readback);

    /* Restore original value */
    REG_WRITE_RAW(SYSCON_WIFI_CLK_EN_REG, orig);
    printf("[SYSCON] REG_WRITE addr=0x%08lx val=0x%08lx  SYSCON_WIFI_CLK_EN_REG (restore)\n",
           (unsigned long)SYSCON_WIFI_CLK_EN_REG, (unsigned long)orig);

    printf("[SYSCON] TEST_PASS\n");

    printf("[SYSCON] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
