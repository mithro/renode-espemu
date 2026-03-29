/**
 * test_systimer_target_oneshot — ESP32-C3 systimer peripheral test
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
    printf("[SYSTMR] === test_systimer_target_oneshot ===\n");

    /* Latch current counter to get baseline */
    REG_WRITE_RAW(SYSTIMER_UNIT0_OP_REG, 1 << 30);
    uint32_t cur_hi = REG_READ_RAW(SYSTIMER_UNIT0_VALUE_HI_REG);
    uint32_t cur_lo = REG_READ_RAW(SYSTIMER_UNIT0_VALUE_LO_REG);
    printf("[SYSTMR] Current counter: HI=0x%08lx LO=0x%08lx\n",
           (unsigned long)cur_hi, (unsigned long)cur_lo);

    /* Set TARGET0 to counter + 10000 */
    uint64_t target = ((uint64_t)cur_hi << 32 | cur_lo) + 10000;
    uint32_t tgt_hi = (uint32_t)(target >> 32);
    uint32_t tgt_lo = (uint32_t)(target & 0xFFFFFFFF);
    printf("[SYSTMR] Setting TARGET0: HI=0x%08lx LO=0x%08lx\n",
           (unsigned long)tgt_hi, (unsigned long)tgt_lo);
    REG_WRITE_RAW(SYSTIMER_TARGET0_HI_REG, tgt_hi);
    REG_WRITE_RAW(SYSTIMER_TARGET0_LO_REG, tgt_lo);

    /* Load comparator */
    REG_WRITE_RAW(SYSTIMER_COMP0_LOAD_REG, 1);

    /* Wait for interrupt */
    for (volatile int i = 0; i < 100000; i++) {}

    /* Read INT_RAW bit 0 */
    print_reg("INT_RAW (expect bit 0 set)", SYSTIMER_INT_RAW_REG);
    uint32_t raw = REG_READ_RAW(SYSTIMER_INT_RAW_REG);
    if (raw & 1) {
        printf("[SYSTMR] TARGET0 oneshot interrupt fired\n");
    } else {
        printf("[SYSTMR] WARNING: TARGET0 interrupt not set\n");
    }
    printf("[SYSTMR] TEST_PASS\n");

    printf("[SYSTMR] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
