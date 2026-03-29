/**
 * test_rtc_power_regs — ESP32-C3 rtc peripheral test
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
    printf("[RTC] === test_rtc_power_regs ===\n");

    #define DR_REG_RTCCNTL_BASE 0x60008000

    print_reg("RTC_CNTL_BIAS_CONF_REG", RTC_CNTL_BIAS_CONF_REG);
    print_reg("RTC_CNTL_PWC_REG", RTC_CNTL_PWC_REG);
    print_reg("RTC_CNTL_DIG_PWC_REG", RTC_CNTL_DIG_PWC_REG);
    print_reg("RTC_CNTL_DIG_ISO_REG", RTC_CNTL_DIG_ISO_REG);

    /* Read additional power registers via base+offset where defines may not exist */
    print_reg("RTC_CNTL_REG (base+0x0084)", DR_REG_RTCCNTL_BASE + 0x0084);
    print_reg("RTC_CNTL_REGULATOR0 (base+0x00CC)", DR_REG_RTCCNTL_BASE + 0x00CC);
    print_reg("RTC_CNTL_REGULATOR1 (base+0x00D0)", DR_REG_RTCCNTL_BASE + 0x00D0);

    printf("[RTC] TEST_PASS\n");

    printf("[RTC] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
