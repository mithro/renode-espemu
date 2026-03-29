/**
 * test_timg_t0_config_reset — ESP32-C3 timer-group peripheral test
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
    printf("[TIMG] === test_timg_t0_config_reset ===\n");

    /* Read TIMG_T0CONFIG_REG and decode fields */
    print_reg("TIMG_T0CONFIG_REG(0)", TIMG_T0CONFIG_REG(0));
    uint32_t cfg = REG_READ_RAW(TIMG_T0CONFIG_REG(0));

    printf("[TIMG] T0CONFIG.INCREASE    = %lu\n", (unsigned long)((cfg >> 30) & 1));
    printf("[TIMG] T0CONFIG.AUTORELOAD  = %lu\n", (unsigned long)((cfg >> 29) & 1));
    printf("[TIMG] T0CONFIG.DIVIDER     = %lu\n", (unsigned long)((cfg >> 13) & 0xFFFF));
    printf("[TIMG] T0CONFIG.ALARM_EN    = %lu\n", (unsigned long)((cfg >> 10) & 1));
    printf("[TIMG] T0CONFIG.EN          = %lu\n", (unsigned long)((cfg >> 31) & 1));

    printf("[TIMG] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
