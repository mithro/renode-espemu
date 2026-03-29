/**
 * test_extmem_freeze — ESP32-C3 extmem peripheral test
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
    printf("[EXTMEM] === test_extmem_freeze ===\n");

    /* Read freeze register (freeze_done bit) */
    print_reg("EXTMEM_ICACHE_FREEZE_REG", EXTMEM_ICACHE_FREEZE_REG);
    uint32_t freeze = REG_READ_RAW(EXTMEM_ICACHE_FREEZE_REG);
    uint32_t freeze_done = (freeze >> 1) & 0x1;
    printf("[EXTMEM] ICACHE_FREEZE freeze_done bit = %lu\n", (unsigned long)freeze_done);

    printf("[EXTMEM] TEST_PASS\n");

    printf("[EXTMEM] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
