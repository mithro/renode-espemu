/**
 * test_rtc_store_registers — ESP32-C3 rtc peripheral test
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
    printf("[RTC] === test_rtc_store_registers ===\n");

    /* STOREn register addresses (base + offset) */
    const uint32_t store_regs[] = {
        RTC_CNTL_STORE0_REG, RTC_CNTL_STORE1_REG,
        RTC_CNTL_STORE2_REG, RTC_CNTL_STORE3_REG,
        RTC_CNTL_STORE4_REG, RTC_CNTL_STORE5_REG,
        RTC_CNTL_STORE6_REG, RTC_CNTL_STORE7_REG,
    };
    const char *store_names[] = {
        "RTC_CNTL_STORE0_REG", "RTC_CNTL_STORE1_REG",
        "RTC_CNTL_STORE2_REG", "RTC_CNTL_STORE3_REG",
        "RTC_CNTL_STORE4_REG", "RTC_CNTL_STORE5_REG",
        "RTC_CNTL_STORE6_REG", "RTC_CNTL_STORE7_REG",
    };

    /* Write unique values */
    int all_match = 1;
    for (int i = 0; i < 8; i++) {
        uint32_t write_val = 0xDEAD0000 + i;
        REG_WRITE_RAW(store_regs[i], write_val);
        printf("[RTC] REG_WRITE addr=0x%08lx val=0x%08lx  %s\n",
               (unsigned long)store_regs[i], (unsigned long)write_val, store_names[i]);
    }

    /* Read back and verify */
    for (int i = 0; i < 8; i++) {
        uint32_t expected = 0xDEAD0000 + i;
        uint32_t actual = REG_READ_RAW(store_regs[i]);
        printf("[RTC] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
               (unsigned long)store_regs[i], (unsigned long)actual, store_names[i]);
        if (actual != expected) {
            printf("[RTC] MISMATCH %s: expected=0x%08lx got=0x%08lx\n",
                   store_names[i], (unsigned long)expected, (unsigned long)actual);
            all_match = 0;
        }
    }

    /* Check STORE4 for XTAL_FREQ encoding */
    uint32_t store4_val = REG_READ_RAW(RTC_CNTL_STORE4_REG);
    printf("[RTC] STORE4 (XTAL_FREQ encoding check) val=0x%08lx\n",
           (unsigned long)store4_val);

    if (all_match) {
        printf("[RTC] All STORE registers read back correctly\n");
        printf("[RTC] TEST_PASS\n");
    } else {
        printf("[RTC] TEST_FAIL\n");
    }

    printf("[RTC] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
