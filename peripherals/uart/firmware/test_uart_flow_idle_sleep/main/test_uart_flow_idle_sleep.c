/**
 * test_uart_flow_idle_sleep — ESP32-C3 uart peripheral test
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
    printf("[UART] === test_uart_flow_idle_sleep ===\n");

    /* Read FLOW_CONF reset value */
    print_reg("UART_FLOW_CONF_REG", UART_FLOW_CONF_REG(0));

    /* Read IDLE_CONF reset value */
    print_reg("UART_IDLE_CONF_REG", UART_IDLE_CONF_REG(0));

    /* Read SLEEP_CONF reset value */
    print_reg("UART_SLEEP_CONF_REG", UART_SLEEP_CONF_REG(0));

    printf("[UART] TEST_PASS\n");

    printf("[UART] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
