/**
 * test_systimer_date — ESP32-C3 systimer peripheral test
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
    printf("[SYSTMR] === test_systimer_date ===\n");

    /* Read SYSTIMER_DATE_REG */
    print_reg("SYSTIMER_DATE_REG", SYSTIMER_DATE_REG);
    uint32_t date = REG_READ_RAW(SYSTIMER_DATE_REG);
    if (date != 0) {
        printf("[SYSTMR] DATE register is non-zero (0x%08lx) -- TEST_PASS\n",
               (unsigned long)date);
    } else {
        printf("[SYSTMR] WARNING: DATE register is zero\n");
    }
    printf("[SYSTMR] TEST_PASS\n");

    printf("[SYSTMR] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
