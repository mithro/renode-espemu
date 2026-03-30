/**
 * test_iomux_pin_reset_values -- ESP32-C3 iomux peripheral test
 *
 * Reads all 22 per-pin IO MUX registers and the PIN_CTRL register
 * immediately after boot to verify reset values.
 *
 * Expected reset values:
 *   PIN_CTRL:               0x00000000
 *   GPIO0-7,9-11,18-19,21: 0x00000000
 *   GPIO8:                  0x00000100 (FUN_PU=1)
 *   GPIO12-17:              0x00000A00 (FUN_DRV=2, FUN_IE=1)
 *   GPIO20:                 0x00000300 (FUN_IE=1, FUN_PU=1)
 *
 * Output format: [IOMUX] REG_READ addr=0xNNN val=0xNNN register_name
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

#define REG_READ_RAW(addr) (*((volatile uint32_t *)(addr)))
#define REG_WRITE_RAW(addr, val) (*((volatile uint32_t *)(addr)) = (val))

/* IO MUX base address for ESP32-C3 */
#define DR_REG_IO_MUX_BASE 0x60009000

#define IO_MUX_PIN_CTRL_REG     (DR_REG_IO_MUX_BASE + 0x00)
#define IO_MUX_GPIO_REG(n)      (DR_REG_IO_MUX_BASE + 0x04 + (n) * 4)

static void print_reg(const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[IOMUX] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

void app_main(void)
{
    printf("[IOMUX] === test_iomux_pin_reset_values ===\n");

    print_reg("PIN_CTRL", IO_MUX_PIN_CTRL_REG);

    /* Read all 22 per-pin registers to check reset values */
    char name[32];
    for (int i = 0; i <= 21; i++) {
        snprintf(name, sizeof(name), "IO_MUX_GPIO%d_REG", i);
        print_reg(name, IO_MUX_GPIO_REG(i));
    }

    printf("[IOMUX] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
