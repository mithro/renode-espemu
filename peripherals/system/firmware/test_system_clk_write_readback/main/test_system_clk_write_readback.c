/**
 * test_system_clk_write_readback — ESP32-C3 system peripheral test
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
    printf("[SYSTEM] === test_system_clk_write_readback ===\n");

    /* Read original value */
    uint32_t orig = REG_READ_RAW(SYSTEM_PERIP_CLK_EN0_REG);
    printf("[SYSTEM] REG_READ  addr=0x%08lx val=0x%08lx  SYSTEM_PERIP_CLK_EN0_REG (original)\n",
           (unsigned long)SYSTEM_PERIP_CLK_EN0_REG, (unsigned long)orig);

    /* Toggle bit 0 */
    uint32_t toggled = orig ^ 0x1;
    REG_WRITE_RAW(SYSTEM_PERIP_CLK_EN0_REG, toggled);
    printf("[SYSTEM] REG_WRITE addr=0x%08lx val=0x%08lx  SYSTEM_PERIP_CLK_EN0_REG (toggled bit0)\n",
           (unsigned long)SYSTEM_PERIP_CLK_EN0_REG, (unsigned long)toggled);

    /* Read back */
    uint32_t readback = REG_READ_RAW(SYSTEM_PERIP_CLK_EN0_REG);
    printf("[SYSTEM] REG_READ  addr=0x%08lx val=0x%08lx  SYSTEM_PERIP_CLK_EN0_REG (readback)\n",
           (unsigned long)SYSTEM_PERIP_CLK_EN0_REG, (unsigned long)readback);

    /* Restore original */
    REG_WRITE_RAW(SYSTEM_PERIP_CLK_EN0_REG, orig);
    printf("[SYSTEM] REG_WRITE addr=0x%08lx val=0x%08lx  SYSTEM_PERIP_CLK_EN0_REG (restored)\n",
           (unsigned long)SYSTEM_PERIP_CLK_EN0_REG, (unsigned long)orig);

    if (readback == toggled) {
        printf("[SYSTEM] TEST_PASS clk_write_readback\n");
    } else {
        printf("[SYSTEM] TEST_FAIL clk_write_readback (expected=0x%08lx got=0x%08lx)\n",
               (unsigned long)toggled, (unsigned long)readback);
    }

    printf("[SYSTEM] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
