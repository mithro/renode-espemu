/**
 * test_iomux_pin_config -- ESP32-C3 iomux peripheral test
 *
 * Tests writing and reading back per-pin IO MUX configuration fields:
 *   - MCU_SEL (function select)
 *   - FUN_DRV (drive strength)
 *   - FUN_IE (input enable)
 *   - FUN_PU / FUN_PD (pull-up / pull-down)
 *   - FILTER_EN (input filter)
 *   - SLP_* (sleep mode fields)
 *
 * Uses GPIO2 (offset 0x0C, resets to 0) as the test pin.
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
#define IO_MUX_GPIO2_REG    (DR_REG_IO_MUX_BASE + 0x0C)

/* Bit field positions */
#define SLP_OE_S     0
#define SLP_SEL_S    1
#define SLP_PD_S     2
#define SLP_PU_S     3
#define SLP_IE_S     4
#define SLP_DRV_S    5
#define FUN_PD_S     7
#define FUN_PU_S     8
#define FUN_IE_S     9
#define FUN_DRV_S   10
#define MCU_SEL_S   12
#define FILTER_EN_S 15

static void print_reg(const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[IOMUX] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

static void write_and_read(const char *desc, uint32_t addr, uint32_t val)
{
    REG_WRITE_RAW(addr, val);
    printf("[IOMUX] REG_WRITE addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, desc);
    print_reg(desc, addr);
}

void app_main(void)
{
    printf("[IOMUX] === test_iomux_pin_config ===\n");

    /* 1. Read reset value of GPIO2 (expect 0x00000000) */
    print_reg("GPIO2 reset", IO_MUX_GPIO2_REG);

    /* 2. Set MCU_SEL=1 (GPIO function): bit [14:12] = 1 */
    write_and_read("GPIO2 MCU_SEL=1",
                   IO_MUX_GPIO2_REG, (1 << MCU_SEL_S));

    /* 3. Set FUN_DRV=3 (max drive): bits [11:10] = 3, keep MCU_SEL=1 */
    write_and_read("GPIO2 MCU_SEL=1 FUN_DRV=3",
                   IO_MUX_GPIO2_REG, (1 << MCU_SEL_S) | (3 << FUN_DRV_S));

    /* 4. Set FUN_IE=1, FUN_PU=1: input enable + pull-up */
    write_and_read("GPIO2 MCU_SEL=1 FUN_DRV=3 FUN_IE=1 FUN_PU=1",
                   IO_MUX_GPIO2_REG,
                   (1 << MCU_SEL_S) | (3 << FUN_DRV_S) |
                   (1 << FUN_IE_S) | (1 << FUN_PU_S));

    /* 5. Set FUN_PD=1 instead of FUN_PU */
    write_and_read("GPIO2 MCU_SEL=1 FUN_DRV=3 FUN_IE=1 FUN_PD=1",
                   IO_MUX_GPIO2_REG,
                   (1 << MCU_SEL_S) | (3 << FUN_DRV_S) |
                   (1 << FUN_IE_S) | (1 << FUN_PD_S));

    /* 6. Set FILTER_EN=1 */
    write_and_read("GPIO2 FILTER_EN=1 only",
                   IO_MUX_GPIO2_REG, (1 << FILTER_EN_S));

    /* 7. Set all sleep fields: SLP_OE, SLP_SEL, SLP_PD, SLP_PU, SLP_IE, SLP_DRV=3 */
    write_and_read("GPIO2 all SLP fields set",
                   IO_MUX_GPIO2_REG,
                   (1 << SLP_OE_S) | (1 << SLP_SEL_S) |
                   (1 << SLP_PD_S) | (1 << SLP_PU_S) |
                   (1 << SLP_IE_S) | (3 << SLP_DRV_S));

    /* 8. Set all fields to max (0xFFFF in lower 16 bits) */
    write_and_read("GPIO2 all fields max",
                   IO_MUX_GPIO2_REG, 0x0000FFFF);

    /* 9. Clear back to 0 */
    write_and_read("GPIO2 cleared to 0",
                   IO_MUX_GPIO2_REG, 0x00000000);

    printf("[IOMUX] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
