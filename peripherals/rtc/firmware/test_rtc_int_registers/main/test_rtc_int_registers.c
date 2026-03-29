/**
 * test_rtc_int_registers — ESP32-C3 rtc peripheral test
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
    printf("[RTC] === test_rtc_int_registers ===\n");

    /* Read initial interrupt register state (expect all zeros) */
    print_reg("RTC_CNTL_INT_ENA_REG", RTC_CNTL_INT_ENA_REG);
    print_reg("RTC_CNTL_INT_RAW_REG", RTC_CNTL_INT_RAW_REG);
    print_reg("RTC_CNTL_INT_ST_REG", RTC_CNTL_INT_ST_REG);

    uint32_t ena_init = REG_READ_RAW(RTC_CNTL_INT_ENA_REG);
    uint32_t raw_init = REG_READ_RAW(RTC_CNTL_INT_RAW_REG);
    uint32_t st_init  = REG_READ_RAW(RTC_CNTL_INT_ST_REG);
    int pass = 1;

    if (ena_init != 0) {
        printf("[RTC] INT_ENA not zero at reset: 0x%08lx\n", (unsigned long)ena_init);
        pass = 0;
    }
    if (raw_init != 0) {
        printf("[RTC] INT_RAW not zero at reset: 0x%08lx\n", (unsigned long)raw_init);
        pass = 0;
    }
    if (st_init != 0) {
        printf("[RTC] INT_ST not zero at reset: 0x%08lx\n", (unsigned long)st_init);
        pass = 0;
    }

    /* Write INT_ENA = 0x1 (enable bit 0) and read back */
    REG_WRITE_RAW(RTC_CNTL_INT_ENA_REG, 0x1);
    printf("[RTC] REG_WRITE addr=0x%08lx val=0x00000001  RTC_CNTL_INT_ENA_REG\n",
           (unsigned long)RTC_CNTL_INT_ENA_REG);
    uint32_t ena_readback = REG_READ_RAW(RTC_CNTL_INT_ENA_REG);
    printf("[RTC] REG_READ  addr=0x%08lx val=0x%08lx  RTC_CNTL_INT_ENA_REG (readback)\n",
           (unsigned long)RTC_CNTL_INT_ENA_REG, (unsigned long)ena_readback);
    if (ena_readback != 0x1) {
        printf("[RTC] INT_ENA readback mismatch\n");
        pass = 0;
    }

    /* INT_ST should still be 0 (RAW=0, so masked status is 0) */
    uint32_t st_after = REG_READ_RAW(RTC_CNTL_INT_ST_REG);
    printf("[RTC] REG_READ  addr=0x%08lx val=0x%08lx  RTC_CNTL_INT_ST_REG (after ENA)\n",
           (unsigned long)RTC_CNTL_INT_ST_REG, (unsigned long)st_after);
    if (st_after != 0) {
        printf("[RTC] INT_ST unexpectedly non-zero\n");
        pass = 0;
    }

    /* Write INT_CLR = 0x1 to clear bit 0 */
    REG_WRITE_RAW(RTC_CNTL_INT_CLR_REG, 0x1);
    printf("[RTC] REG_WRITE addr=0x%08lx val=0x00000001  RTC_CNTL_INT_CLR_REG\n",
           (unsigned long)RTC_CNTL_INT_CLR_REG);

    if (pass) {
        printf("[RTC] TEST_PASS\n");
    } else {
        printf("[RTC] TEST_FAIL\n");
    }

    printf("[RTC] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
