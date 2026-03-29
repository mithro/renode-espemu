/**
 * test_intmtx_threshold — ESP32-C3 interrupt-matrix peripheral test
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
    printf("[INTMTX] === test_intmtx_threshold ===\n");

    /* IntMatrix base: 0x600C2000, CPU_INT_THRESH at base+0x194 */
    uint32_t base = 0x600C2000;
    uint32_t thresh_addr = base + 0x194;

    /* Read current CPU_INT_THRESH */
    uint32_t saved = REG_READ_RAW(thresh_addr);
    print_reg("CPU_INT_THRESH (initial)", thresh_addr);

    /* Write 3, read back */
    printf("[INTMTX] Writing CPU_INT_THRESH = 3\n");
    REG_WRITE_RAW(thresh_addr, 3);
    print_reg("CPU_INT_THRESH (expect 3)", thresh_addr);

    /* Restore original value */
    printf("[INTMTX] Restoring CPU_INT_THRESH\n");
    REG_WRITE_RAW(thresh_addr, saved);
    print_reg("CPU_INT_THRESH (restored)", thresh_addr);

    printf("[INTMTX] TEST_PASS\n");

    printf("[INTMTX] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
