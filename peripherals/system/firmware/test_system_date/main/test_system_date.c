/**
 * test_system_date — ESP32-C3 system peripheral test
 *
 * Tests a specific feature of the system peripheral.
 * Output format: [SYSTEM] REG_READ addr=0xNNN val=0xNNN register_name
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "soc/system_reg.h"

#define REG_READ_RAW(addr) (*((volatile uint32_t *)(addr)))
#define REG_WRITE_RAW(addr, val) (*((volatile uint32_t *)(addr)) = (val))

static void print_reg(const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[SYSTEM] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

void app_main(void)
{
    printf("[SYSTEM] === test_system_date ===\n");

    uint32_t val = REG_READ_RAW(SYSTEM_DATE_REG);
    printf("[SYSTEM] REG_READ  addr=0x%08lx val=0x%08lx  SYSTEM_DATE_REG\n",
           (unsigned long)SYSTEM_DATE_REG, (unsigned long)val);

    if (val != 0) {
        printf("[SYSTEM] TEST_PASS system_date_nonzero\n");
    } else {
        printf("[SYSTEM] TEST_FAIL system_date_nonzero (val=0x00000000)\n");
    }

    printf("[SYSTEM] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
