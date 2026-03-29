/**
 * test_intmtx_reset_values — ESP32-C3 interrupt-matrix peripheral test
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
    printf("[INTMTX] === test_intmtx_reset_values ===\n");

    /* IntMatrix base: 0x600C2000 */
    uint32_t base = 0x600C2000;
    char name[64];

    /* Read all 62 source mapping registers (base + src*4, src=0..61) */
    printf("[INTMTX] --- Source Mapping Registers (62 sources) ---\n");
    for (int src = 0; src < 62; src++) {
        uint32_t addr = base + src * 4;
        uint32_t val = REG_READ_RAW(addr);
        printf("[INTMTX] REG_READ  addr=0x%08lx val=0x%08lx  PRO_MAC_INTR_MAP + src_%d\n",
               (unsigned long)addr, (unsigned long)val, src);
    }

    /* Read CPU_INT_ENABLE (base+0x104) */
    print_reg("CPU_INT_ENABLE", base + 0x104);

    /* Read CPU_INT_TYPE (base+0x108) */
    print_reg("CPU_INT_TYPE", base + 0x108);

    /* Read CPU_INT_EIP_STATUS (base+0x110) */
    print_reg("CPU_INT_EIP_STATUS", base + 0x110);

    /* Read CPU_INT_THRESH (base+0x194) */
    print_reg("CPU_INT_THRESH", base + 0x194);

    printf("[INTMTX] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
