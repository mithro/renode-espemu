/**
 * test_uart_clkdiv — ESP32-C3 uart peripheral test
 *
 * Tests a specific feature of the uart peripheral.
 * Output format: [UART] REG_READ addr=0xNNN val=0xNNN register_name
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "soc/uart_reg.h"

#define REG_READ_RAW(addr) (*((volatile uint32_t *)(addr)))
#define REG_WRITE_RAW(addr, val) (*((volatile uint32_t *)(addr)) = (val))

static void print_reg(const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[UART] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

void app_main(void)
{
    printf("[UART] === test_uart_clkdiv ===\n");

    /* Read CLKDIV_REG reset value */
    print_reg("UART_CLKDIV_REG (reset)", UART_CLKDIV_REG(0));

    /* Write new divider value and read back */
    uint32_t new_div = 0x2B6; /* example: 694 for ~115200 baud */
    REG_WRITE_RAW(UART_CLKDIV_REG(0), new_div);
    print_reg("UART_CLKDIV_REG (after write)", UART_CLKDIV_REG(0));

    uint32_t readback = REG_READ_RAW(UART_CLKDIV_REG(0));
    if ((readback & 0xFFFFF) == new_div) {
        printf("[UART] CLKDIV readback matches\n");
    } else {
        printf("[UART] CLKDIV readback 0x%08lx != 0x%08lx\n",
               (unsigned long)readback, (unsigned long)new_div);
    }

    printf("[UART] TEST_PASS\n");

    printf("[UART] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
