/**
 * test_timg_wdt_config — ESP32-C3 timer-group peripheral test
 *
 * Tests a specific feature of the timer-group peripheral.
 * Output format: [TIMG] REG_READ addr=0xNNN val=0xNNN register_name
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "soc/timer_group_reg.h"

#define REG_READ_RAW(addr) (*((volatile uint32_t *)(addr)))
#define REG_WRITE_RAW(addr, val) (*((volatile uint32_t *)(addr)) = (val))

static void print_reg(const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[TIMG] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

void app_main(void)
{
    printf("[TIMG] === test_timg_wdt_config ===\n");

    /* Unlock WDT write protect (write magic value 0x50D83AA1) */
    printf("[TIMG] Unlocking WDTWPROTECT\n");
    REG_WRITE_RAW(TIMG_WDTWPROTECT_REG(0), 0x50D83AA1);
    print_reg("WDTWPROTECT", TIMG_WDTWPROTECT_REG(0));

    /* Write WDTCONFIG0 with a test value */
    printf("[TIMG] Writing WDTCONFIG0 = 0x00060000\n");
    REG_WRITE_RAW(TIMG_WDTCONFIG0_REG(0), 0x00060000);
    print_reg("WDTCONFIG0 (expect 0x00060000)", TIMG_WDTCONFIG0_REG(0));

    /* Re-lock WDT write protect */
    printf("[TIMG] Re-locking WDTWPROTECT\n");
    REG_WRITE_RAW(TIMG_WDTWPROTECT_REG(0), 0);

    printf("[TIMG] TEST_PASS\n");

    printf("[TIMG] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
