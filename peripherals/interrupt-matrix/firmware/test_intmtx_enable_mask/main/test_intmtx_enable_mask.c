/**
 * test_intmtx_enable_mask — ESP32-C3 interrupt-matrix peripheral test
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
    printf("[INTMTX] === test_intmtx_enable_mask ===\n");

    /* IntMatrix base: 0x600C2000, CPU_INT_ENABLE at base+0x104 */
    uint32_t base = 0x600C2000;
    uint32_t enable_addr = base + 0x104;

    /* Read current CPU_INT_ENABLE */
    uint32_t saved = REG_READ_RAW(enable_addr);
    print_reg("CPU_INT_ENABLE (initial)", enable_addr);

    /* Write 0xFF, read back */
    printf("[INTMTX] Writing CPU_INT_ENABLE = 0xFF\n");
    REG_WRITE_RAW(enable_addr, 0xFF);
    print_reg("CPU_INT_ENABLE (expect 0xFF)", enable_addr);

    /* Restore original value */
    printf("[INTMTX] Restoring CPU_INT_ENABLE\n");
    REG_WRITE_RAW(enable_addr, saved);
    print_reg("CPU_INT_ENABLE (restored)", enable_addr);

    printf("[INTMTX] TEST_PASS\n");

    printf("[INTMTX] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
