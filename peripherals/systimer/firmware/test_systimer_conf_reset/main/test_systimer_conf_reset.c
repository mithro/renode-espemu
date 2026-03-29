/**
 * test_systimer_conf_reset — ESP32-C3 systimer peripheral test
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
    printf("[SYSTMR] === test_systimer_conf_reset ===\n");

    /* Base: 0x60023000 */
    uint32_t conf = REG_READ_RAW(SYSTIMER_CONF_REG);
    print_reg("SYSTIMER_CONF_REG", SYSTIMER_CONF_REG);

    /* Decode individual bit fields */
    printf("[SYSTMR] CONF.CLK_EN           = %lu\n", (unsigned long)((conf >> 31) & 1));
    printf("[SYSTMR] CONF.UNIT0_WORK_EN    = %lu\n", (unsigned long)((conf >> 30) & 1));
    printf("[SYSTMR] CONF.UNIT1_WORK_EN    = %lu\n", (unsigned long)((conf >> 29) & 1));
    printf("[SYSTMR] CONF.TARGET0_WORK_EN  = %lu\n", (unsigned long)((conf >> 24) & 1));
    printf("[SYSTMR] CONF.TARGET1_WORK_EN  = %lu\n", (unsigned long)((conf >> 23) & 1));
    printf("[SYSTMR] CONF.TARGET2_WORK_EN  = %lu\n", (unsigned long)((conf >> 22) & 1));

    printf("[SYSTMR] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
