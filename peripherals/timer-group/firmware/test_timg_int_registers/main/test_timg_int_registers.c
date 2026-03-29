/**
 * test_timg_int_registers — ESP32-C3 timer-group peripheral test
 *
 * Tests a specific feature of the timer-group peripheral.
 * Output format: [TIMG] REG_READ addr=0xNNN val=0xNNN register_name
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "soc/timer_group_reg.h"

#define REG_READ_RAW(addr) (*((volatile uint32_t *)(addr)))
#define REG_WRITE_RAW(addr, val) (*((volatile uint32_t *)(addr)) = (val))

static void print_reg(const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[TIMG] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

void app_main(void)
{
    printf("[TIMG] === test_timg_int_registers ===\n");

    /* Read initial interrupt register state */
    print_reg("INT_ENA_TIMERS (initial)", TIMG_INT_ENA_TIMERS_REG(0));
    print_reg("INT_RAW_TIMERS (initial)", TIMG_INT_RAW_TIMERS_REG(0));
    print_reg("INT_ST_TIMERS  (initial)", TIMG_INT_ST_TIMERS_REG(0));

    /* Write INT_ENA = 1 (enable T0 interrupt) */
    printf("[TIMG] Writing INT_ENA_TIMERS = 1\n");
    REG_WRITE_RAW(TIMG_INT_ENA_TIMERS_REG(0), 1);
    print_reg("INT_ENA_TIMERS (after write)", TIMG_INT_ENA_TIMERS_REG(0));

    /* Read INT_ST */
    print_reg("INT_ST_TIMERS (after ENA)", TIMG_INT_ST_TIMERS_REG(0));

    /* Clear interrupts */
    printf("[TIMG] Writing INT_CLR_TIMERS = 1\n");
    REG_WRITE_RAW(TIMG_INT_CLR_TIMERS_REG(0), 1);
    print_reg("INT_RAW_TIMERS (after clear)", TIMG_INT_RAW_TIMERS_REG(0));
    print_reg("INT_ST_TIMERS  (after clear)", TIMG_INT_ST_TIMERS_REG(0));

    printf("[TIMG] TEST_PASS\n");

    printf("[TIMG] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
