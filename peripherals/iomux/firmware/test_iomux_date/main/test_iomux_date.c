/**
 * test_iomux_date -- ESP32-C3 iomux peripheral test
 *
 * Tests the IO_MUX_DATE_REG version register:
 *   - Read reset value (expect 0x02006050)
 *   - Write a new value and read back
 *   - Restore original value
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
#define DR_REG_IO_MUX_BASE  0x60009000
#define IO_MUX_DATE_REG     (DR_REG_IO_MUX_BASE + 0xFC)
#define IO_MUX_DATE_DEFAULT 0x02006050

static void print_reg(const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[IOMUX] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

void app_main(void)
{
    printf("[IOMUX] === test_iomux_date ===\n");

    /* 1. Read reset value (expect 0x02006050) */
    print_reg("IO_MUX_DATE reset", IO_MUX_DATE_REG);

    /* 2. Write a test value and read back */
    REG_WRITE_RAW(IO_MUX_DATE_REG, 0x0DEADBEE);
    printf("[IOMUX] REG_WRITE addr=0x%08lx val=0x%08lx  IO_MUX_DATE write test\n",
           (unsigned long)IO_MUX_DATE_REG, (unsigned long)0x0DEADBEE);
    print_reg("IO_MUX_DATE after write", IO_MUX_DATE_REG);

    /* 3. Restore original value */
    REG_WRITE_RAW(IO_MUX_DATE_REG, IO_MUX_DATE_DEFAULT);
    printf("[IOMUX] REG_WRITE addr=0x%08lx val=0x%08lx  IO_MUX_DATE restore\n",
           (unsigned long)IO_MUX_DATE_REG, (unsigned long)IO_MUX_DATE_DEFAULT);
    print_reg("IO_MUX_DATE restored", IO_MUX_DATE_REG);

    printf("[IOMUX] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
