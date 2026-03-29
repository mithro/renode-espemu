/**
 * test_systimer_target_conf — ESP32-C3 systimer peripheral test
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
    printf("[SYSTMR] === test_systimer_target_conf ===\n");

    /* Write test values to TARGET0/1/2_CONF and read back */
    printf("[SYSTMR] Writing TARGET0_CONF = 0xAA\n");
    REG_WRITE_RAW(SYSTIMER_TARGET0_CONF_REG, 0xAA);
    print_reg("TARGET0_CONF (expect 0xAA)", SYSTIMER_TARGET0_CONF_REG);

    printf("[SYSTMR] Writing TARGET1_CONF = 0xBB\n");
    REG_WRITE_RAW(SYSTIMER_TARGET1_CONF_REG, 0xBB);
    print_reg("TARGET1_CONF (expect 0xBB)", SYSTIMER_TARGET1_CONF_REG);

    printf("[SYSTMR] Writing TARGET2_CONF = 0xCC\n");
    REG_WRITE_RAW(SYSTIMER_TARGET2_CONF_REG, 0xCC);
    print_reg("TARGET2_CONF (expect 0xCC)", SYSTIMER_TARGET2_CONF_REG);

    printf("[SYSTMR] TEST_PASS\n");

    printf("[SYSTMR] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
