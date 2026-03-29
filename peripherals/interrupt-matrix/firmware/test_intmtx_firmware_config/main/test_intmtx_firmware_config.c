/**
 * test_intmtx_firmware_config — ESP32-C3 interrupt-matrix peripheral test
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
    printf("[INTMTX] === test_intmtx_firmware_config ===\n");

    /* IntMatrix base: 0x600C2000 */
    uint32_t base = 0x600C2000;

    /* Read current source mappings as configured by ESP-IDF boot */
    printf("[INTMTX] --- Non-zero source mappings (ESP-IDF boot config) ---\n");
    for (int src = 0; src < 62; src++) {
        uint32_t addr = base + src * 4;
        uint32_t val = REG_READ_RAW(addr);
        if (val != 0) {
            printf("[INTMTX] REG_READ  addr=0x%08lx val=0x%08lx  SRC_%d -> line %lu\n",
                   (unsigned long)addr, (unsigned long)val, src, (unsigned long)val);
        }
    }

    /* Read CPU_INT_ENABLE */
    print_reg("CPU_INT_ENABLE", base + 0x104);

    /* Read priorities for lines 1..31 */
    printf("[INTMTX] --- Interrupt priorities ---\n");
    for (int i = 1; i <= 31; i++) {
        uint32_t addr = base + 0x114 + i * 4;
        uint32_t val = REG_READ_RAW(addr);
        if (val != 0) {
            printf("[INTMTX] REG_READ  addr=0x%08lx val=0x%08lx  CPU_INT_PRI_%d\n",
                   (unsigned long)addr, (unsigned long)val, i);
        }
    }

    printf("[INTMTX] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
