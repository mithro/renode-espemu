/**
 * test_rtc_options_state — ESP32-C3 rtc peripheral test
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
    printf("[RTC] === test_rtc_options_state ===\n");

    print_reg("RTC_CNTL_OPTIONS0_REG", RTC_CNTL_OPTIONS0_REG);
    print_reg("RTC_CNTL_STATE0_REG", RTC_CNTL_STATE0_REG);
    print_reg("RTC_CNTL_TIMER1_REG", RTC_CNTL_TIMER1_REG);
    print_reg("RTC_CNTL_TIMER2_REG", RTC_CNTL_TIMER2_REG);
    print_reg("RTC_CNTL_TIMER3_REG", RTC_CNTL_TIMER3_REG);
    print_reg("RTC_CNTL_TIMER4_REG", RTC_CNTL_TIMER4_REG);
    print_reg("RTC_CNTL_TIMER5_REG", RTC_CNTL_TIMER5_REG);
    print_reg("RTC_CNTL_TIMER6_REG", RTC_CNTL_TIMER6_REG);
    print_reg("RTC_CNTL_ANA_CONF_REG", RTC_CNTL_ANA_CONF_REG);

    printf("[RTC] TEST_PASS\n");

    printf("[RTC] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
