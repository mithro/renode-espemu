/**
 * test_extmem_icache_ctrl — ESP32-C3 extmem peripheral test
 *
 * Tests a specific feature of the extmem peripheral.
 * Output format: [EXTMEM] REG_READ addr=0xNNN val=0xNNN register_name
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "soc/extmem_reg.h"

#define REG_READ_RAW(addr) (*((volatile uint32_t *)(addr)))
#define REG_WRITE_RAW(addr, val) (*((volatile uint32_t *)(addr)) = (val))

static void print_reg(const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[EXTMEM] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

void app_main(void)
{
    printf("[EXTMEM] === test_extmem_icache_ctrl ===\n");

    /* Read ICACHE_CTRL_REG: bit 0 = icache enable */
    print_reg("EXTMEM_ICACHE_CTRL_REG", EXTMEM_ICACHE_CTRL_REG);
    uint32_t ctrl = REG_READ_RAW(EXTMEM_ICACHE_CTRL_REG);
    uint32_t enable = ctrl & 0x1;
    printf("[EXTMEM] ICACHE_CTRL enable bit = %lu\n", (unsigned long)enable);

    /* Read ICACHE_CTRL1_REG */
    print_reg("EXTMEM_ICACHE_CTRL1_REG", EXTMEM_ICACHE_CTRL1_REG);

    if (enable == 1) {
        printf("[EXTMEM] TEST_PASS\n");
    } else {
        printf("[EXTMEM] TEST_FAIL: enable bit is not 1\n");
    }

    printf("[EXTMEM] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
