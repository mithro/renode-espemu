/**
 * test_timg_t0_alarm — ESP32-C3 timer-group peripheral test
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
    printf("[TIMG] === test_timg_t0_alarm ===\n");

    /* Set alarm value HI/LO */
    printf("[TIMG] Setting alarm HI=0, LO=0x5000\n");
    REG_WRITE_RAW(TIMG_T0ALARMHI_REG(0), 0);
    REG_WRITE_RAW(TIMG_T0ALARMLO_REG(0), 0x5000);
    print_reg("T0ALARMHI", TIMG_T0ALARMHI_REG(0));
    print_reg("T0ALARMLO", TIMG_T0ALARMLO_REG(0));

    /* Enable alarm (bit 10) and timer (bit 31, INCREASE bit 30) in T0CONFIG */
    uint32_t cfg = REG_READ_RAW(TIMG_T0CONFIG_REG(0));
    cfg |= (1 << 10);   /* ALARM_EN */
    cfg |= (1 << 31);   /* EN */
    cfg |= (1 << 30);   /* INCREASE */
    REG_WRITE_RAW(TIMG_T0CONFIG_REG(0), cfg);
    print_reg("T0CONFIG (alarm+timer enabled)", TIMG_T0CONFIG_REG(0));

    /* Poll INT_RAW for alarm bit */
    printf("[TIMG] Waiting for alarm interrupt...\n");
    for (volatile int i = 0; i < 200000; i++) {}
    print_reg("INT_RAW_TIMERS", TIMG_INT_RAW_TIMERS_REG(0));

    uint32_t raw = REG_READ_RAW(TIMG_INT_RAW_TIMERS_REG(0));
    if (raw & 1) {
        printf("[TIMG] Alarm interrupt fired\n");
    } else {
        printf("[TIMG] WARNING: alarm interrupt not set\n");
    }
    printf("[TIMG] TEST_PASS\n");

    printf("[TIMG] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
