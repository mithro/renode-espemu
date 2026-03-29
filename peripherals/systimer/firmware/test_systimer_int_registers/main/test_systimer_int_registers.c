/**
 * test_systimer_int_registers — ESP32-C3 systimer peripheral test
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
    printf("[SYSTMR] === test_systimer_int_registers ===\n");

    /* Read initial interrupt register state */
    print_reg("INT_ENA (initial)", SYSTIMER_INT_ENA_REG);
    print_reg("INT_RAW (initial)", SYSTIMER_INT_RAW_REG);
    print_reg("INT_ST  (initial)", SYSTIMER_INT_ST_REG);

    /* Write INT_ENA = 1 (enable TARGET0 interrupt) */
    printf("[SYSTMR] Writing INT_ENA = 1\n");
    REG_WRITE_RAW(SYSTIMER_INT_ENA_REG, 1);
    print_reg("INT_ENA (after write)", SYSTIMER_INT_ENA_REG);

    /* Read INT_ST */
    print_reg("INT_ST  (after ENA)", SYSTIMER_INT_ST_REG);

    /* Clear all interrupts by writing INT_CLR = 7 */
    printf("[SYSTMR] Writing INT_CLR = 7\n");
    REG_WRITE_RAW(SYSTIMER_INT_CLR_REG, 7);
    print_reg("INT_RAW (after clear)", SYSTIMER_INT_RAW_REG);
    print_reg("INT_ST  (after clear)", SYSTIMER_INT_ST_REG);

    printf("[SYSTMR] TEST_PASS\n");

    printf("[SYSTMR] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
