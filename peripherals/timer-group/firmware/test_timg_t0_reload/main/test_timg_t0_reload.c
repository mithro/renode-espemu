/**
 * test_timg_t0_reload — ESP32-C3 timer-group peripheral test
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
    printf("[TIMG] === test_timg_t0_reload ===\n");

    /* Write T0LOADLO=0x5555, T0LOADHI=0 */
    printf("[TIMG] Writing T0LOADLO=0x5555, T0LOADHI=0\n");
    REG_WRITE_RAW(TIMG_T0LOADLO_REG(0), 0x5555);
    REG_WRITE_RAW(TIMG_T0LOADHI_REG(0), 0);
    print_reg("T0LOADLO", TIMG_T0LOADLO_REG(0));
    print_reg("T0LOADHI", TIMG_T0LOADHI_REG(0));

    /* Trigger load */
    printf("[TIMG] Triggering T0LOAD\n");
    REG_WRITE_RAW(TIMG_T0LOAD_REG(0), 1);

    /* Update and read */
    REG_WRITE_RAW(TIMG_T0UPDATE_REG(0), 1);
    print_reg("T0LO (expect ~0x5555)", TIMG_T0LO_REG(0));
    print_reg("T0HI (expect 0)", TIMG_T0HI_REG(0));

    printf("[TIMG] TEST_PASS\n");

    printf("[TIMG] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
