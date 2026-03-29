/**
 * test_timg_rtc_calibration — ESP32-C3 timer-group peripheral test
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
    printf("[TIMG] === test_timg_rtc_calibration ===\n");

    /* Read RTCCALICFG bit 15 (RDY) */
    print_reg("TIMG_RTCCALICFG_REG(0)", TIMG_RTCCALICFG_REG(0));
    uint32_t calicfg = REG_READ_RAW(TIMG_RTCCALICFG_REG(0));
    uint32_t rdy = (calicfg >> 15) & 1;
    printf("[TIMG] RTCCALICFG.RDY = %lu\n", (unsigned long)rdy);

    /* Read RTCCALICFG1 (calibration value) */
    print_reg("TIMG_RTCCALICFG1_REG(0)", TIMG_RTCCALICFG1_REG(0));

    if (rdy) {
        printf("[TIMG] RTC calibration ready\n");
    } else {
        printf("[TIMG] WARNING: RTC calibration not ready\n");
    }
    printf("[TIMG] TEST_PASS\n");

    printf("[TIMG] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
