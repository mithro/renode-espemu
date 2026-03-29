/**
 * test_rtc_wdt_config — ESP32-C3 rtc peripheral test
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
    printf("[RTC] === test_rtc_wdt_config ===\n");

    /* Read WDT configuration registers */
    print_reg("RTC_CNTL_WDTCONFIG0_REG", RTC_CNTL_WDTCONFIG0_REG);
    print_reg("RTC_CNTL_WDTCONFIG1_REG", RTC_CNTL_WDTCONFIG1_REG);
    print_reg("RTC_CNTL_WDTCONFIG2_REG", RTC_CNTL_WDTCONFIG2_REG);
    print_reg("RTC_CNTL_WDTCONFIG3_REG", RTC_CNTL_WDTCONFIG3_REG);
    print_reg("RTC_CNTL_WDTCONFIG4_REG", RTC_CNTL_WDTCONFIG4_REG);
    print_reg("RTC_CNTL_WDTWPROTECT_REG", RTC_CNTL_WDTWPROTECT_REG);

    /* Unlock WDT write protect with magic key */
    uint32_t wdt_key = 0x50D83AA1;
    REG_WRITE_RAW(RTC_CNTL_WDTWPROTECT_REG, wdt_key);
    printf("[RTC] REG_WRITE addr=0x%08lx val=0x%08lx  RTC_CNTL_WDTWPROTECT_REG (unlock)\n",
           (unsigned long)RTC_CNTL_WDTWPROTECT_REG, (unsigned long)wdt_key);

    /* Write a test value to WDTCONFIG0 */
    uint32_t test_val = 0x00010000;  /* a benign config value */
    REG_WRITE_RAW(RTC_CNTL_WDTCONFIG0_REG, test_val);
    printf("[RTC] REG_WRITE addr=0x%08lx val=0x%08lx  RTC_CNTL_WDTCONFIG0_REG\n",
           (unsigned long)RTC_CNTL_WDTCONFIG0_REG, (unsigned long)test_val);

    /* Read back and verify */
    uint32_t readback = REG_READ_RAW(RTC_CNTL_WDTCONFIG0_REG);
    printf("[RTC] REG_READ  addr=0x%08lx val=0x%08lx  RTC_CNTL_WDTCONFIG0_REG (readback)\n",
           (unsigned long)RTC_CNTL_WDTCONFIG0_REG, (unsigned long)readback);

    if (readback == test_val) {
        printf("[RTC] WDTCONFIG0 write persisted after unlock\n");
        printf("[RTC] TEST_PASS\n");
    } else {
        printf("[RTC] WDTCONFIG0 readback mismatch: expected=0x%08lx got=0x%08lx\n",
               (unsigned long)test_val, (unsigned long)readback);
        printf("[RTC] TEST_FAIL\n");
    }

    printf("[RTC] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
