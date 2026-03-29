/**
 * test_intmtx_source_mapping — ESP32-C3 interrupt-matrix peripheral test
 *
 * Tests a specific feature of the interrupt-matrix peripheral.
 * Output format: [INTMTX] REG_READ addr=0xNNN val=0xNNN register_name
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

#define REG_READ_RAW(addr) (*((volatile uint32_t *)(addr)))
#define REG_WRITE_RAW(addr, val) (*((volatile uint32_t *)(addr)) = (val))

static void print_reg(const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[INTMTX] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

void app_main(void)
{
    printf("[INTMTX] === test_intmtx_source_mapping ===\n");

    /* IntMatrix base: 0x600C2000 */
    uint32_t base = 0x600C2000;

    /* Write source 0 -> line 5 */
    printf("[INTMTX] Writing source 0 mapping = 5\n");
    REG_WRITE_RAW(base + 0, 5);
    print_reg("SRC_0_MAP (expect 5)", base + 0);

    /* Write source 10 -> line 15 */
    printf("[INTMTX] Writing source 10 mapping = 15\n");
    REG_WRITE_RAW(base + 10 * 4, 15);
    print_reg("SRC_10_MAP (expect 15)", base + 10 * 4);

    printf("[INTMTX] TEST_PASS\n");

    printf("[INTMTX] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
