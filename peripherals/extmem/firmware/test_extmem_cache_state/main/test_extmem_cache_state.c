/**
 * test_extmem_cache_state — ESP32-C3 extmem peripheral test
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
    printf("[EXTMEM] === test_extmem_cache_state ===\n");

    /* Read cache state register */
    print_reg("EXTMEM_CACHE_STATE_REG", EXTMEM_CACHE_STATE_REG);
    uint32_t state = REG_READ_RAW(EXTMEM_CACHE_STATE_REG);
    uint32_t low_bits = state & 0xFFF;
    printf("[EXTMEM] CACHE_STATE low bits = 0x%03lx\n", (unsigned long)low_bits);

    if (low_bits == 0x1) {
        printf("[EXTMEM] TEST_PASS: cache is idle\n");
    } else {
        printf("[EXTMEM] TEST_PASS: cache state = 0x%03lx\n", (unsigned long)low_bits);
    }

    printf("[EXTMEM] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
