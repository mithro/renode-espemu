/**
 * test_rtc_brownout — ESP32-C3 rtc peripheral test
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
    printf("[RTC] === test_rtc_brownout ===\n");

    print_reg("RTC_CNTL_BROWN_OUT_REG", RTC_CNTL_BROWN_OUT_REG);

    uint32_t brown_out = REG_READ_RAW(RTC_CNTL_BROWN_OUT_REG);
    uint32_t enable_bit = (brown_out >> 30) & 1;
    uint32_t detect_bit = (brown_out >> 31) & 1;
    printf("[RTC] Brownout enable (bit 30) = %lu\n", (unsigned long)enable_bit);
    printf("[RTC] Brownout detect (bit 31) = %lu\n", (unsigned long)detect_bit);
    printf("[RTC] TEST_PASS\n");

    printf("[RTC] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
