/**
 * ESP32-C3 Timer Group Test Firmware
 *
 * Tests Timer Group registers including Timer 0 counter, RTC calibration,
 * alarm functionality, and interrupt registers. Prints structured output
 * for comparison between hardware, QEMU, and Renode.
 *
 * TIMG0 base: 0x6001F000
 * TIMG1 base: 0x60020000
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "soc/timer_group_reg.h"

/* Direct register read/write macros */
#define REG_READ_RAW(addr)        (*((volatile uint32_t *)(addr)))
#define REG_WRITE_RAW(addr, val)  (*((volatile uint32_t *)(addr)) = (val))

static void print_reg(const char *group, const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[TIMER] REG_READ  addr=0x%08lx val=0x%08lx  %s.%s\n",
           (unsigned long)addr, (unsigned long)val, group, name);
}

static void test_timer_group(const char *group, int idx)
{
    printf("[TIMER] === %s Register Dump ===\n", group);

    /* Timer 0 configuration */
    print_reg(group, "T0CONFIG",    TIMG_T0CONFIG_REG(idx));
    print_reg(group, "T0LO",        TIMG_T0LO_REG(idx));
    print_reg(group, "T0HI",        TIMG_T0HI_REG(idx));
    print_reg(group, "T0ALARMLO",   TIMG_T0ALARMLO_REG(idx));
    print_reg(group, "T0ALARMHI",   TIMG_T0ALARMHI_REG(idx));
    print_reg(group, "T0LOADLO",    TIMG_T0LOADLO_REG(idx));
    print_reg(group, "T0LOADHI",    TIMG_T0LOADHI_REG(idx));

    /* WDT registers */
    print_reg(group, "WDTCONFIG0",  TIMG_WDTCONFIG0_REG(idx));
    print_reg(group, "WDTWPROTECT", TIMG_WDTWPROTECT_REG(idx));

    /* RTC calibration */
    print_reg(group, "RTCCALICFG",  TIMG_RTCCALICFG_REG(idx));
    print_reg(group, "RTCCALICFG1", TIMG_RTCCALICFG1_REG(idx));
    print_reg(group, "RTCCALICFG2", TIMG_RTCCALICFG2_REG(idx));

    /* Interrupt registers */
    print_reg(group, "INT_ENA",     TIMG_INT_ENA_TIMERS_REG(idx));
    print_reg(group, "INT_RAW",     TIMG_INT_RAW_TIMERS_REG(idx));
    print_reg(group, "INT_ST",      TIMG_INT_ST_TIMERS_REG(idx));

    /* Clock control */
    print_reg(group, "REGCLK",      TIMG_REGCLK_REG(idx));
}

static void test_rtc_calibration(const char *group, int idx)
{
    printf("[TIMER] === %s RTC Calibration Test ===\n", group);

    /* Read RTCCALICFG to check RTC_CALI_RDY (bit 15) */
    uint32_t calicfg = REG_READ_RAW(TIMG_RTCCALICFG_REG(idx));
    uint32_t rdy = (calicfg >> 15) & 0x1;
    printf("[TIMER] %s.RTC_CALI_RDY=%lu\n", group, (unsigned long)rdy);

    if (rdy) {
        printf("[TIMER] TEST_PASS %s_rtc_cali_rdy\n", group);
    } else {
        printf("[TIMER] TEST_FAIL %s_rtc_cali_rdy expected=1 got=0\n", group);
    }

    /* Read calibration value from RTCCALICFG1 */
    uint32_t calicfg1 = REG_READ_RAW(TIMG_RTCCALICFG1_REG(idx));
    uint32_t cali_value = (calicfg1 >> 7) & 0x01FFFFFF;
    printf("[TIMER] %s.RTC_CALI_VALUE=0x%lx\n", group, (unsigned long)cali_value);

    if (cali_value > 0) {
        printf("[TIMER] TEST_PASS %s_rtc_cali_value_nonzero\n", group);
    } else {
        printf("[TIMER] TEST_FAIL %s_rtc_cali_value_nonzero expected=nonzero got=0\n", group);
    }

    /* Check no timeout */
    uint32_t calicfg2 = REG_READ_RAW(TIMG_RTCCALICFG2_REG(idx));
    uint32_t timeout = calicfg2 & 0x1;
    printf("[TIMER] %s.RTC_CALI_TIMEOUT=%lu\n", group, (unsigned long)timeout);

    if (!timeout) {
        printf("[TIMER] TEST_PASS %s_rtc_cali_no_timeout\n", group);
    } else {
        printf("[TIMER] TEST_FAIL %s_rtc_cali_no_timeout expected=0 got=1\n", group);
    }
}

static void test_timer_counter(const char *group, int idx)
{
    printf("[TIMER] === %s Timer Counter Test ===\n", group);

    /* Latch counter by writing to T0UPDATE */
    REG_WRITE_RAW(TIMG_T0UPDATE_REG(idx), 0x80000000);

    /* Read latched value */
    uint32_t lo1 = REG_READ_RAW(TIMG_T0LO_REG(idx));
    uint32_t hi1 = REG_READ_RAW(TIMG_T0HI_REG(idx));
    printf("[TIMER] %s.T0_COUNTER_1=0x%08lx%08lx\n", group,
           (unsigned long)hi1, (unsigned long)lo1);

    /* Latch again to verify counter can change */
    REG_WRITE_RAW(TIMG_T0UPDATE_REG(idx), 0x80000000);
    uint32_t lo2 = REG_READ_RAW(TIMG_T0LO_REG(idx));
    uint32_t hi2 = REG_READ_RAW(TIMG_T0HI_REG(idx));
    printf("[TIMER] %s.T0_COUNTER_2=0x%08lx%08lx\n", group,
           (unsigned long)hi2, (unsigned long)lo2);

    /* Test reload: set load value, trigger reload, latch, read */
    REG_WRITE_RAW(TIMG_T0LOADLO_REG(idx), 0x12345678);
    REG_WRITE_RAW(TIMG_T0LOADHI_REG(idx), 0x000000AB);
    REG_WRITE_RAW(TIMG_T0LOAD_REG(idx), 1); /* trigger reload */
    REG_WRITE_RAW(TIMG_T0UPDATE_REG(idx), 0x80000000); /* latch */
    uint32_t lo3 = REG_READ_RAW(TIMG_T0LO_REG(idx));
    uint32_t hi3 = REG_READ_RAW(TIMG_T0HI_REG(idx));
    printf("[TIMER] %s.T0_AFTER_RELOAD=0x%08lx%08lx\n", group,
           (unsigned long)hi3, (unsigned long)lo3);

    if (lo3 == 0x12345678 || (lo3 >= 0x12345678 && lo3 <= 0x12346000)) {
        printf("[TIMER] TEST_PASS %s_timer_reload\n", group);
    } else {
        printf("[TIMER] TEST_FAIL %s_timer_reload expected=~0xAB12345678 got=0x%lx%08lx\n",
               group, (unsigned long)hi3, (unsigned long)lo3);
    }
}

static void test_timer_alarm(const char *group, int idx)
{
    printf("[TIMER] === %s Timer Alarm Test ===\n", group);

    /* Clear any pending interrupts */
    REG_WRITE_RAW(TIMG_INT_CLR_TIMERS_REG(idx), 0x3);

    /* Load counter to 0 */
    REG_WRITE_RAW(TIMG_T0LOADLO_REG(idx), 0);
    REG_WRITE_RAW(TIMG_T0LOADHI_REG(idx), 0);
    REG_WRITE_RAW(TIMG_T0LOAD_REG(idx), 1);

    /* Set alarm to a low value */
    REG_WRITE_RAW(TIMG_T0ALARMLO_REG(idx), 100);
    REG_WRITE_RAW(TIMG_T0ALARMHI_REG(idx), 0);

    /* Enable alarm and timer (increase mode) */
    uint32_t config = (1u << 31) |  /* T0_EN */
                      (1u << 30) |  /* T0_INCREASE */
                      (1u << 10) |  /* T0_ALARM_EN */
                      (1u << 13);   /* DIVIDER=1 */
    REG_WRITE_RAW(TIMG_T0CONFIG_REG(idx), config);

    /* Latch counter a few times to advance it (in emulation) */
    for (int i = 0; i < 5; i++) {
        REG_WRITE_RAW(TIMG_T0UPDATE_REG(idx), 0x80000000);
    }

    /* Check if alarm interrupt fired */
    uint32_t int_raw = REG_READ_RAW(TIMG_INT_RAW_TIMERS_REG(idx));
    uint32_t t0_int = int_raw & 0x1;
    printf("[TIMER] %s.INT_RAW_AFTER_ALARM=0x%08lx (T0_INT=%lu)\n",
           group, (unsigned long)int_raw, (unsigned long)t0_int);

    /* Read alarm config to check auto-clear of ALARM_EN */
    uint32_t config_after = REG_READ_RAW(TIMG_T0CONFIG_REG(idx));
    uint32_t alarm_en_after = (config_after >> 10) & 0x1;
    printf("[TIMER] %s.T0_ALARM_EN_AFTER=%lu\n", group, (unsigned long)alarm_en_after);

    if (t0_int) {
        printf("[TIMER] TEST_PASS %s_alarm_interrupt\n", group);
    } else {
        printf("[TIMER] TEST_FAIL %s_alarm_interrupt expected=1 got=0\n", group);
    }

    /* Clear interrupt and verify */
    REG_WRITE_RAW(TIMG_INT_CLR_TIMERS_REG(idx), 0x1);
    int_raw = REG_READ_RAW(TIMG_INT_RAW_TIMERS_REG(idx));
    printf("[TIMER] %s.INT_RAW_AFTER_CLR=0x%08lx\n", group, (unsigned long)int_raw);

    if ((int_raw & 0x1) == 0) {
        printf("[TIMER] TEST_PASS %s_interrupt_clear\n", group);
    } else {
        printf("[TIMER] TEST_FAIL %s_interrupt_clear expected=0 got=1\n", group);
    }
}

void app_main(void)
{
    printf("[TIMER] === ESP32-C3 Timer Group Test ===\n");

    /* Test TIMG0 */
    test_timer_group("TIMG0", 0);
    test_rtc_calibration("TIMG0", 0);
    test_timer_counter("TIMG0", 0);
    test_timer_alarm("TIMG0", 0);

    /* Test TIMG1 */
    test_timer_group("TIMG1", 1);
    test_rtc_calibration("TIMG1", 1);
    test_timer_counter("TIMG1", 1);
    test_timer_alarm("TIMG1", 1);

    printf("[TIMER] === All Tests Complete ===\n");

    /* Keep running so FreeRTOS doesn't complain about task return */
    while (1) {
        vTaskDelay(1000 / portTICK_PERIOD_MS);
    }
}
