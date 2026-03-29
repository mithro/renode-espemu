/**
 * test_extmem_preload — ESP32-C3 extmem peripheral test
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
    printf("[EXTMEM] === test_extmem_preload ===\n");

    /* Read preload control register (done bit) */
    print_reg("EXTMEM_ICACHE_PRELOAD_CTRL_REG", EXTMEM_ICACHE_PRELOAD_CTRL_REG);

    /* Write preload address and read back */
    REG_WRITE_RAW(EXTMEM_ICACHE_PRELOAD_ADDR_REG, 0x42010000);
    print_reg("EXTMEM_ICACHE_PRELOAD_ADDR_REG", EXTMEM_ICACHE_PRELOAD_ADDR_REG);

    /* Write preload size and read back */
    REG_WRITE_RAW(EXTMEM_ICACHE_PRELOAD_SIZE_REG, 0x200);
    print_reg("EXTMEM_ICACHE_PRELOAD_SIZE_REG", EXTMEM_ICACHE_PRELOAD_SIZE_REG);

    printf("[EXTMEM] TEST_PASS\n");

    printf("[EXTMEM] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
