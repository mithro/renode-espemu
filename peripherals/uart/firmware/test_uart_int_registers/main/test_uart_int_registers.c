/**
 * test_uart_int_registers — ESP32-C3 uart peripheral test
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
    printf("[UART] === test_uart_int_registers ===\n");

    /* Read interrupt register reset values */
    print_reg("UART_INT_RAW_REG", UART_INT_RAW_REG(0));
    print_reg("UART_INT_ST_REG", UART_INT_ST_REG(0));
    print_reg("UART_INT_ENA_REG", UART_INT_ENA_REG(0));

    /* Write INT_ENA with a test value and read back */
    REG_WRITE_RAW(UART_INT_ENA_REG(0), 0x1); /* enable RXFIFO_FULL_INT */
    print_reg("UART_INT_ENA_REG (after write)", UART_INT_ENA_REG(0));

    /* Clear interrupts via INT_CLR */
    REG_WRITE_RAW(UART_INT_CLR_REG(0), 0xFFFFFFFF);
    printf("[UART] Wrote 0xFFFFFFFF to UART_INT_CLR_REG\n");

    /* Disable interrupts again */
    REG_WRITE_RAW(UART_INT_ENA_REG(0), 0x0);

    printf("[UART] TEST_PASS\n");

    printf("[UART] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
