/**
 * ESP32-C3 Interrupt Matrix Test Firmware
 *
 * Reads interrupt matrix configuration registers and prints structured output.
 * Tests source mapping, enable, priority, and threshold registers.
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

#define DR_REG_INTERRUPT_CORE0_BASE 0x600C2000
#define REG_READ(addr) (*((volatile uint32_t *)(addr)))
#define REG_WRITE(addr, val) (*((volatile uint32_t *)(addr)) = (val))

/* Register offsets */
#define INTMATRIX_SRC_MAP(n)    (DR_REG_INTERRUPT_CORE0_BASE + (n) * 4)
#define CPU_INT_ENABLE          (DR_REG_INTERRUPT_CORE0_BASE + 0x104)
#define CPU_INT_TYPE            (DR_REG_INTERRUPT_CORE0_BASE + 0x108)
#define CPU_INT_EIP_STATUS      (DR_REG_INTERRUPT_CORE0_BASE + 0x110)
#define CPU_INT_PRI(n)          (DR_REG_INTERRUPT_CORE0_BASE + 0x114 + (n) * 4)
#define CPU_INT_THRESH          (DR_REG_INTERRUPT_CORE0_BASE + 0x194)

void app_main(void)
{
    printf("[INTMATRIX] === ESP32-C3 Interrupt Matrix Test ===\n");

    /* Read current interrupt enable state */
    uint32_t enable = REG_READ(CPU_INT_ENABLE);
    printf("[INTMATRIX] REG_READ  addr=0x%08lx val=0x%08lx  CPU_INT_ENABLE\n",
           (unsigned long)CPU_INT_ENABLE, (unsigned long)enable);

    /* Read interrupt type */
    uint32_t inttype = REG_READ(CPU_INT_TYPE);
    printf("[INTMATRIX] REG_READ  addr=0x%08lx val=0x%08lx  CPU_INT_TYPE\n",
           (unsigned long)CPU_INT_TYPE, (unsigned long)inttype);

    /* Read pending status */
    uint32_t pending = REG_READ(CPU_INT_EIP_STATUS);
    printf("[INTMATRIX] REG_READ  addr=0x%08lx val=0x%08lx  CPU_INT_EIP_STATUS\n",
           (unsigned long)CPU_INT_EIP_STATUS, (unsigned long)pending);

    /* Read threshold */
    uint32_t thresh = REG_READ(CPU_INT_THRESH);
    printf("[INTMATRIX] REG_READ  addr=0x%08lx val=0x%08lx  CPU_INT_THRESH\n",
           (unsigned long)CPU_INT_THRESH, (unsigned long)thresh);

    /* Read all non-zero source mappings */
    printf("[INTMATRIX] === Source Mappings (non-zero) ===\n");
    int mapped_count = 0;
    for (int src = 0; src < 64; src++) {
        uint32_t val = REG_READ(INTMATRIX_SRC_MAP(src));
        if (val != 0) {
            printf("[INTMATRIX] REG_READ  addr=0x%08lx val=0x%08lx  SRC_%d->LINE_%lu\n",
                   (unsigned long)INTMATRIX_SRC_MAP(src), (unsigned long)val,
                   src, (unsigned long)(val & 0x1F));
            mapped_count++;
        }
    }

    /* Read priorities for enabled lines */
    printf("[INTMATRIX] === Priorities (enabled lines) ===\n");
    for (int line = 0; line < 32; line++) {
        if (enable & (1u << line)) {
            uint32_t pri = REG_READ(CPU_INT_PRI(line));
            printf("[INTMATRIX] REG_READ  addr=0x%08lx val=0x%08lx  PRI_%d\n",
                   (unsigned long)CPU_INT_PRI(line), (unsigned long)pri, line);
        }
    }

    /* Test results */
    printf("[INTMATRIX] === Test Results ===\n");
    if (mapped_count > 0) {
        printf("[INTMATRIX] TEST_PASS sources_mapped (count=%d)\n", mapped_count);
    } else {
        printf("[INTMATRIX] TEST_FAIL sources_mapped expected=>0 got=0\n");
    }

    if (enable != 0) {
        printf("[INTMATRIX] TEST_PASS interrupts_enabled\n");
    } else {
        printf("[INTMATRIX] TEST_FAIL interrupts_enabled expected=nonzero got=0\n");
    }

    printf("[INTMATRIX] === Tests Complete ===\n");

    while (1) {
        vTaskDelay(1000 / portTICK_PERIOD_MS);
    }
}
