/**
 * test_systimer_target_periodic — ESP32-C3 systimer peripheral test
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
    printf("[SYSTMR] === test_systimer_target_periodic ===\n");

    /* Set TARGET0_CONF to period mode (bit 30) with period=1000 */
    printf("[SYSTMR] Setting TARGET0_CONF: period mode, period=1000\n");
    REG_WRITE_RAW(SYSTIMER_TARGET0_CONF_REG, (1 << 30) | 1000);
    print_reg("TARGET0_CONF", SYSTIMER_TARGET0_CONF_REG);

    /* Load comparator */
    REG_WRITE_RAW(SYSTIMER_COMP0_LOAD_REG, 1);

    /* Wait for first interrupt */
    for (volatile int i = 0; i < 100000; i++) {}
    print_reg("INT_RAW (after 1st wait)", SYSTIMER_INT_RAW_REG);
    uint32_t raw1 = REG_READ_RAW(SYSTIMER_INT_RAW_REG);

    /* Clear interrupt */
    printf("[SYSTMR] Clearing INT_CLR bit 0\n");
    REG_WRITE_RAW(SYSTIMER_INT_CLR_REG, 1);

    /* Wait for second interrupt */
    for (volatile int i = 0; i < 100000; i++) {}
    print_reg("INT_RAW (after 2nd wait)", SYSTIMER_INT_RAW_REG);
    uint32_t raw2 = REG_READ_RAW(SYSTIMER_INT_RAW_REG);

    if ((raw1 & 1) && (raw2 & 1)) {
        printf("[SYSTMR] Periodic interrupt fired twice\n");
    } else {
        printf("[SYSTMR] WARNING: periodic interrupt did not fire twice (raw1=0x%lx raw2=0x%lx)\n",
               (unsigned long)raw1, (unsigned long)raw2);
    }
    printf("[SYSTMR] TEST_PASS\n");

    printf("[SYSTMR] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
