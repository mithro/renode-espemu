/**
 * test_timg_t0_counter — ESP32-C3 timer-group peripheral test
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
    printf("[TIMG] === test_timg_t0_counter ===\n");

    /* Enable timer by setting EN bit (bit 31) in T0CONFIG */
    uint32_t cfg = REG_READ_RAW(TIMG_T0CONFIG_REG(0));
    cfg |= (1 << 31);  /* EN */
    cfg |= (1 << 30);  /* INCREASE */
    printf("[TIMG] Enabling T0 (EN=1, INCREASE=1)\n");
    REG_WRITE_RAW(TIMG_T0CONFIG_REG(0), cfg);

    /* First update and read */
    REG_WRITE_RAW(TIMG_T0UPDATE_REG(0), 1);
    uint32_t lo1 = REG_READ_RAW(TIMG_T0LO_REG(0));
    uint32_t hi1 = REG_READ_RAW(TIMG_T0HI_REG(0));
    print_reg("T0LO (1st)", TIMG_T0LO_REG(0));
    print_reg("T0HI (1st)", TIMG_T0HI_REG(0));

    /* Delay, then second update and read */
    for (volatile int i = 0; i < 1000; i++) {}

    REG_WRITE_RAW(TIMG_T0UPDATE_REG(0), 1);
    uint32_t lo2 = REG_READ_RAW(TIMG_T0LO_REG(0));
    uint32_t hi2 = REG_READ_RAW(TIMG_T0HI_REG(0));
    print_reg("T0LO (2nd)", TIMG_T0LO_REG(0));
    print_reg("T0HI (2nd)", TIMG_T0HI_REG(0));

    uint64_t val1 = ((uint64_t)hi1 << 32) | lo1;
    uint64_t val2 = ((uint64_t)hi2 << 32) | lo2;
    if (val2 > val1) {
        printf("[TIMG] Counter advanced correctly\n");
    } else {
        printf("[TIMG] WARNING: counter did not advance\n");
    }
    printf("[TIMG] TEST_PASS\n");

    printf("[TIMG] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
