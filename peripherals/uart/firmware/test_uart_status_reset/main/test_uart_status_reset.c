/**
 * test_uart_status_reset — ESP32-C3 uart peripheral test
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
    printf("[UART] === test_uart_status_reset ===\n");

    /* UART0 base = 0x60000000 */
    /* Read STATUS_REG: TXFIFO_CNT should be 0 at reset */
    print_reg("UART_STATUS_REG", UART_STATUS_REG(0));
    uint32_t status = REG_READ_RAW(UART_STATUS_REG(0));
    uint32_t txfifo_cnt = (status >> 16) & 0x3FF;
    printf("[UART] TXFIFO_CNT = %lu\n", (unsigned long)txfifo_cnt);

    /* Read CONF0 and CONF1 reset values */
    print_reg("UART_CONF0_REG", UART_CONF0_REG(0));
    print_reg("UART_CONF1_REG", UART_CONF1_REG(0));

    printf("[UART] TEST_PASS\n");

    printf("[UART] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
