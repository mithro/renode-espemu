/**
 * test_intmtx_pending_clear — ESP32-C3 interrupt-matrix peripheral test
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
    printf("[INTMTX] === test_intmtx_pending_clear ===\n");

    /* IntMatrix base: 0x600C2000 */
    uint32_t base = 0x600C2000;
    uint32_t eip_addr = base + 0x110;    /* CPU_INT_EIP_STATUS */
    uint32_t clear_addr = base + 0x10C;  /* CPU_INT_CLEAR */

    /* Read CPU_INT_EIP_STATUS */
    print_reg("CPU_INT_EIP_STATUS (before clear)", eip_addr);

    /* Write CPU_INT_CLEAR = 0xFF to clear pending bits */
    printf("[INTMTX] Writing CPU_INT_CLEAR = 0xFF\n");
    REG_WRITE_RAW(clear_addr, 0xFF);

    /* Read EIP_STATUS again -- should be cleared */
    print_reg("CPU_INT_EIP_STATUS (after clear)", eip_addr);

    printf("[INTMTX] TEST_PASS\n");

    printf("[INTMTX] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
