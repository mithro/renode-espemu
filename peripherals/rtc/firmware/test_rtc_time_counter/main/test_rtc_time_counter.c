/**
 * test_rtc_time_counter — ESP32-C3 rtc peripheral test
 *
 * Tests a specific feature of the rtc peripheral.
 * Output format: [RTC] REG_READ addr=0xNNN val=0xNNN register_name
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "soc/rtc_cntl_reg.h"

#define REG_READ_RAW(addr) (*((volatile uint32_t *)(addr)))
#define REG_WRITE_RAW(addr, val) (*((volatile uint32_t *)(addr)) = (val))

static void print_reg(const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[RTC] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

void app_main(void)
{
    printf("[RTC] === test_rtc_time_counter ===\n");

    /* First latch: set bit 31 of TIME_UPDATE to latch the RTC counter */
    REG_WRITE_RAW(RTC_CNTL_TIME_UPDATE_REG, REG_READ_RAW(RTC_CNTL_TIME_UPDATE_REG) | (1U << 31));
    print_reg("RTC_CNTL_TIME_UPDATE_REG", RTC_CNTL_TIME_UPDATE_REG);

    uint32_t low0_a  = REG_READ_RAW(RTC_CNTL_TIME_LOW0_REG);
    uint32_t high0_a = REG_READ_RAW(RTC_CNTL_TIME_HIGH0_REG);
    printf("[RTC] REG_READ  addr=0x%08lx val=0x%08lx  RTC_CNTL_TIME_LOW0_REG\n",
           (unsigned long)RTC_CNTL_TIME_LOW0_REG, (unsigned long)low0_a);
    printf("[RTC] REG_READ  addr=0x%08lx val=0x%08lx  RTC_CNTL_TIME_HIGH0_REG\n",
           (unsigned long)RTC_CNTL_TIME_HIGH0_REG, (unsigned long)high0_a);
    uint64_t time_a = ((uint64_t)high0_a << 32) | low0_a;
    printf("[RTC] time_a = 0x%08lx%08lx\n",
           (unsigned long)(time_a >> 32), (unsigned long)(time_a & 0xFFFFFFFF));

    /* Small delay to let counter advance */
    vTaskDelay(10 / portTICK_PERIOD_MS);

    /* Second latch */
    REG_WRITE_RAW(RTC_CNTL_TIME_UPDATE_REG, REG_READ_RAW(RTC_CNTL_TIME_UPDATE_REG) | (1U << 31));
    print_reg("RTC_CNTL_TIME_UPDATE_REG", RTC_CNTL_TIME_UPDATE_REG);

    uint32_t low0_b  = REG_READ_RAW(RTC_CNTL_TIME_LOW0_REG);
    uint32_t high0_b = REG_READ_RAW(RTC_CNTL_TIME_HIGH0_REG);
    printf("[RTC] REG_READ  addr=0x%08lx val=0x%08lx  RTC_CNTL_TIME_LOW0_REG\n",
           (unsigned long)RTC_CNTL_TIME_LOW0_REG, (unsigned long)low0_b);
    printf("[RTC] REG_READ  addr=0x%08lx val=0x%08lx  RTC_CNTL_TIME_HIGH0_REG\n",
           (unsigned long)RTC_CNTL_TIME_HIGH0_REG, (unsigned long)high0_b);
    uint64_t time_b = ((uint64_t)high0_b << 32) | low0_b;
    printf("[RTC] time_b = 0x%08lx%08lx\n",
           (unsigned long)(time_b >> 32), (unsigned long)(time_b & 0xFFFFFFFF));

    if (time_b > time_a) {
        printf("[RTC] RTC time counter is advancing (time_b > time_a)\n");
        printf("[RTC] TEST_PASS\n");
    } else {
        printf("[RTC] RTC time counter did NOT advance\n");
        printf("[RTC] TEST_FAIL\n");
    }

    printf("[RTC] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
