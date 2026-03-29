/**
 * test_systimer_unit1 — ESP32-C3 systimer peripheral test
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
    printf("[SYSTMR] === test_systimer_unit1 ===\n");

    /* Latch UNIT1 */
    printf("[SYSTMR] Latching UNIT1 counter\n");
    REG_WRITE_RAW(SYSTIMER_UNIT1_OP_REG, 1 << 30);
    print_reg("UNIT1_VALUE_HI", SYSTIMER_UNIT1_VALUE_HI_REG);
    print_reg("UNIT1_VALUE_LO", SYSTIMER_UNIT1_VALUE_LO_REG);
    uint32_t u1_hi = REG_READ_RAW(SYSTIMER_UNIT1_VALUE_HI_REG);
    uint32_t u1_lo = REG_READ_RAW(SYSTIMER_UNIT1_VALUE_LO_REG);

    /* Latch UNIT1 again */
    for (volatile int i = 0; i < 1000; i++) {}
    printf("[SYSTMR] Latching UNIT1 counter (2nd)\n");
    REG_WRITE_RAW(SYSTIMER_UNIT1_OP_REG, 1 << 30);
    print_reg("UNIT1_VALUE_HI (2nd)", SYSTIMER_UNIT1_VALUE_HI_REG);
    print_reg("UNIT1_VALUE_LO (2nd)", SYSTIMER_UNIT1_VALUE_LO_REG);

    /* Also latch UNIT0 for comparison */
    printf("[SYSTMR] Latching UNIT0 counter for comparison\n");
    REG_WRITE_RAW(SYSTIMER_UNIT0_OP_REG, 1 << 30);
    print_reg("UNIT0_VALUE_HI", SYSTIMER_UNIT0_VALUE_HI_REG);
    print_reg("UNIT0_VALUE_LO", SYSTIMER_UNIT0_VALUE_LO_REG);

    printf("[SYSTMR] TEST_PASS\n");

    printf("[SYSTMR] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
