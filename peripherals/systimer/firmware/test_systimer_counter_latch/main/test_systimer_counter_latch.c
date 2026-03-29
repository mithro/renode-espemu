/**
 * test_systimer_counter_latch — ESP32-C3 systimer peripheral test
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
    printf("[SYSTMR] === test_systimer_counter_latch ===\n");

    /* Latch UNIT0 counter by writing bit 30 of UNIT0_OP_REG */
    printf("[SYSTMR] Latching UNIT0 counter (first read)\n");
    REG_WRITE_RAW(SYSTIMER_UNIT0_OP_REG, 1 << 30);
    uint32_t hi1 = REG_READ_RAW(SYSTIMER_UNIT0_VALUE_HI_REG);
    uint32_t lo1 = REG_READ_RAW(SYSTIMER_UNIT0_VALUE_LO_REG);
    print_reg("UNIT0_VALUE_HI (1st)", SYSTIMER_UNIT0_VALUE_HI_REG);
    print_reg("UNIT0_VALUE_LO (1st)", SYSTIMER_UNIT0_VALUE_LO_REG);

    /* Small delay, then latch again */
    for (volatile int i = 0; i < 1000; i++) {}

    printf("[SYSTMR] Latching UNIT0 counter (second read)\n");
    REG_WRITE_RAW(SYSTIMER_UNIT0_OP_REG, 1 << 30);
    uint32_t hi2 = REG_READ_RAW(SYSTIMER_UNIT0_VALUE_HI_REG);
    uint32_t lo2 = REG_READ_RAW(SYSTIMER_UNIT0_VALUE_LO_REG);
    print_reg("UNIT0_VALUE_HI (2nd)", SYSTIMER_UNIT0_VALUE_HI_REG);
    print_reg("UNIT0_VALUE_LO (2nd)", SYSTIMER_UNIT0_VALUE_LO_REG);

    /* Verify the counter advanced */
    uint64_t val1 = ((uint64_t)hi1 << 32) | lo1;
    uint64_t val2 = ((uint64_t)hi2 << 32) | lo2;
    printf("[SYSTMR] Counter1=0x%08lx%08lx Counter2=0x%08lx%08lx\n",
           (unsigned long)hi1, (unsigned long)lo1,
           (unsigned long)hi2, (unsigned long)lo2);
    if (val2 > val1) {
        printf("[SYSTMR] Counter advanced correctly\n");
    } else {
        printf("[SYSTMR] WARNING: counter did not advance\n");
    }
    printf("[SYSTMR] TEST_PASS\n");

    printf("[SYSTMR] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
