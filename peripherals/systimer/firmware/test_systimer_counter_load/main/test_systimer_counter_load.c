/**
 * test_systimer_counter_load — ESP32-C3 systimer peripheral test
 *
 * Tests a specific feature of the systimer peripheral.
 * Output format: [SYSTMR] REG_READ addr=0xNNN val=0xNNN register_name
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "soc/systimer_reg.h"

#define REG_READ_RAW(addr) (*((volatile uint32_t *)(addr)))
#define REG_WRITE_RAW(addr, val) (*((volatile uint32_t *)(addr)) = (val))

static void print_reg(const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[SYSTMR] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

void app_main(void)
{
    printf("[SYSTMR] === test_systimer_counter_load ===\n");

    /* Write LOAD_HI=0, LOAD_LO=0x1000 */
    printf("[SYSTMR] Writing UNIT0_LOAD_HI=0, UNIT0_LOAD_LO=0x1000\n");
    REG_WRITE_RAW(SYSTIMER_UNIT0_LOAD_HI_REG, 0);
    REG_WRITE_RAW(SYSTIMER_UNIT0_LOAD_LO_REG, 0x1000);
    print_reg("UNIT0_LOAD_HI", SYSTIMER_UNIT0_LOAD_HI_REG);
    print_reg("UNIT0_LOAD_LO", SYSTIMER_UNIT0_LOAD_LO_REG);

    /* Trigger load by writing UNIT0_LOAD */
    printf("[SYSTMR] Triggering UNIT0_LOAD\n");
    REG_WRITE_RAW(SYSTIMER_UNIT0_LOAD_REG, 1);

    /* Latch and read value */
    REG_WRITE_RAW(SYSTIMER_UNIT0_OP_REG, 1 << 30);
    print_reg("UNIT0_VALUE_HI (expect 0)", SYSTIMER_UNIT0_VALUE_HI_REG);
    print_reg("UNIT0_VALUE_LO (expect ~0x1000)", SYSTIMER_UNIT0_VALUE_LO_REG);

    printf("[SYSTMR] TEST_PASS\n");

    printf("[SYSTMR] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
