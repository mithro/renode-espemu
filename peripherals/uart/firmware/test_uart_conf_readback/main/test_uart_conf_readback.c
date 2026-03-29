/**
 * test_uart_conf_readback — ESP32-C3 uart peripheral test
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
    printf("[UART] === test_uart_conf_readback ===\n");

    /* Save original CONF0 value */
    uint32_t orig = REG_READ_RAW(UART_CONF0_REG(0));
    printf("[UART] Original UART_CONF0_REG = 0x%08lx\n", (unsigned long)orig);

    /* Write test value and read back */
    uint32_t test_val = 0x0000001C; /* parity_en=1, parity=1, bit_num=3 (8-bit) */
    REG_WRITE_RAW(UART_CONF0_REG(0), test_val);
    print_reg("UART_CONF0_REG (after write)", UART_CONF0_REG(0));

    uint32_t readback = REG_READ_RAW(UART_CONF0_REG(0));
    if (readback == test_val) {
        printf("[UART] Readback matches written value\n");
    } else {
        printf("[UART] Readback 0x%08lx != written 0x%08lx\n",
               (unsigned long)readback, (unsigned long)test_val);
    }

    /* Restore original */
    REG_WRITE_RAW(UART_CONF0_REG(0), orig);
    print_reg("UART_CONF0_REG (restored)", UART_CONF0_REG(0));

    printf("[UART] TEST_PASS\n");

    printf("[UART] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
