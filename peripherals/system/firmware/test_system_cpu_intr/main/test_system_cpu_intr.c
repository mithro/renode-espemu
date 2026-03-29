/**
 * test_system_cpu_intr — ESP32-C3 system peripheral test
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
    printf("[SYSTEM] === test_system_cpu_intr ===\n");

    /* Write 1 to CPU_INTR_FROM_CPU_0 */
    REG_WRITE_RAW(SYSTEM_CPU_INTR_FROM_CPU_0_REG, 0x1);
    printf("[SYSTEM] REG_WRITE addr=0x%08lx val=0x00000001  SYSTEM_CPU_INTR_FROM_CPU_0_REG\n",
           (unsigned long)SYSTEM_CPU_INTR_FROM_CPU_0_REG);

    /* Read back -- should be 0 (auto-clear) */
    uint32_t val = REG_READ_RAW(SYSTEM_CPU_INTR_FROM_CPU_0_REG);
    printf("[SYSTEM] REG_READ  addr=0x%08lx val=0x%08lx  SYSTEM_CPU_INTR_FROM_CPU_0_REG (readback)\n",
           (unsigned long)SYSTEM_CPU_INTR_FROM_CPU_0_REG, (unsigned long)val);

    if (val == 0) {
        printf("[SYSTEM] TEST_PASS cpu_intr_auto_clear\n");
    } else {
        printf("[SYSTEM] TEST_FAIL cpu_intr_auto_clear (expected=0x00000000 got=0x%08lx)\n",
               (unsigned long)val);
    }

    printf("[SYSTEM] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
