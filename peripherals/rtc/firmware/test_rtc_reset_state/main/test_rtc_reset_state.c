/**
 * test_rtc_reset_state — ESP32-C3 rtc peripheral test
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
    printf("[RTC] === test_rtc_reset_state ===\n");

    print_reg("RTC_CNTL_RESET_STATE_REG", RTC_CNTL_RESET_STATE_REG);

    uint32_t reset_state = REG_READ_RAW(RTC_CNTL_RESET_STATE_REG);
    uint32_t reset_cause = reset_state & 0x3F;  /* bits [5:0] */
    printf("[RTC] reset_cause (bits[5:0]) = 0x%lx\n", (unsigned long)reset_cause);

    if (reset_cause == 1) {
        printf("[RTC] Reset cause is POWERON_RESET (1)\n");
        printf("[RTC] TEST_PASS\n");
    } else {
        printf("[RTC] Expected reset_cause=1 (POWERON), got %lu\n",
               (unsigned long)reset_cause);
        printf("[RTC] TEST_FAIL\n");
    }

    printf("[RTC] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
