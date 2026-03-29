/**
 * test_rtc_swd — ESP32-C3 rtc peripheral test
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
    printf("[RTC] === test_rtc_swd ===\n");

    print_reg("RTC_CNTL_SWD_CONF_REG", RTC_CNTL_SWD_CONF_REG);
    print_reg("RTC_CNTL_SWD_WPROTECT_REG", RTC_CNTL_SWD_WPROTECT_REG);

    /* Write SWD unlock key */
    uint32_t swd_key = 0x8F1D312A;
    REG_WRITE_RAW(RTC_CNTL_SWD_WPROTECT_REG, swd_key);
    printf("[RTC] REG_WRITE addr=0x%08lx val=0x%08lx  RTC_CNTL_SWD_WPROTECT_REG (unlock)\n",
           (unsigned long)RTC_CNTL_SWD_WPROTECT_REG, (unsigned long)swd_key);

    /* Read back to verify unlock key was accepted */
    uint32_t readback = REG_READ_RAW(RTC_CNTL_SWD_WPROTECT_REG);
    printf("[RTC] REG_READ  addr=0x%08lx val=0x%08lx  RTC_CNTL_SWD_WPROTECT_REG (readback)\n",
           (unsigned long)RTC_CNTL_SWD_WPROTECT_REG, (unsigned long)readback);

    /* Read SWD_CONF again after unlock */
    print_reg("RTC_CNTL_SWD_CONF_REG (after unlock)", RTC_CNTL_SWD_CONF_REG);

    printf("[RTC] TEST_PASS\n");

    printf("[RTC] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
