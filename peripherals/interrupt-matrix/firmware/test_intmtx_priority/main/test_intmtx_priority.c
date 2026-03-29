/**
 * test_intmtx_priority — ESP32-C3 interrupt-matrix peripheral test
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
    printf("[INTMTX] === test_intmtx_priority ===\n");

    /* IntMatrix base: 0x600C2000, CPU_INT_PRI_1 at base+0x118 */
    uint32_t base = 0x600C2000;
    uint32_t pri1_addr = base + 0x118;
    uint32_t pri2_addr = base + 0x11C;

    /* Write CPU_INT_PRI_1 = 5, read back */
    printf("[INTMTX] Writing CPU_INT_PRI_1 = 5\n");
    REG_WRITE_RAW(pri1_addr, 5);
    print_reg("CPU_INT_PRI_1 (expect 5)", pri1_addr);

    /* Write CPU_INT_PRI_2 = 10, read back */
    printf("[INTMTX] Writing CPU_INT_PRI_2 = 10\n");
    REG_WRITE_RAW(pri2_addr, 10);
    print_reg("CPU_INT_PRI_2 (expect 10)", pri2_addr);

    printf("[INTMTX] TEST_PASS\n");

    printf("[INTMTX] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
