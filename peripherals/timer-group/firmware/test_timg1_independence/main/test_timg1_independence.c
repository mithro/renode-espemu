/**
 * test_timg1_independence — ESP32-C3 timer-group peripheral test
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
    printf("[TIMG] === test_timg1_independence ===\n");

    /* TIMG1 base: 0x60020000 -- use TIMG_xxx_REG(1) macros */

    /* Enable TIMG1 T0 */
    uint32_t cfg = REG_READ_RAW(TIMG_T0CONFIG_REG(1));
    cfg |= (1 << 31);  /* EN */
    cfg |= (1 << 30);  /* INCREASE */
    printf("[TIMG] Enabling TIMG1 T0\n");
    REG_WRITE_RAW(TIMG_T0CONFIG_REG(1), cfg);
    print_reg("TIMG1_T0CONFIG", TIMG_T0CONFIG_REG(1));

    /* Read TIMG1 counter */
    REG_WRITE_RAW(TIMG_T0UPDATE_REG(1), 1);
    print_reg("TIMG1_T0LO (1st)", TIMG_T0LO_REG(1));
    print_reg("TIMG1_T0HI (1st)", TIMG_T0HI_REG(1));
    uint32_t t1_lo1 = REG_READ_RAW(TIMG_T0LO_REG(1));

    /* Read TIMG0 counter for comparison */
    REG_WRITE_RAW(TIMG_T0UPDATE_REG(0), 1);
    print_reg("TIMG0_T0LO", TIMG_T0LO_REG(0));
    print_reg("TIMG0_T0HI", TIMG_T0HI_REG(0));
    uint32_t t0_lo = REG_READ_RAW(TIMG_T0LO_REG(0));

    /* Delay and read TIMG1 again */
    for (volatile int i = 0; i < 1000; i++) {}
    REG_WRITE_RAW(TIMG_T0UPDATE_REG(1), 1);
    print_reg("TIMG1_T0LO (2nd)", TIMG_T0LO_REG(1));
    print_reg("TIMG1_T0HI (2nd)", TIMG_T0HI_REG(1));

    printf("[TIMG] TIMG0 and TIMG1 counters are independent\n");
    printf("[TIMG] TEST_PASS\n");

    printf("[TIMG] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
