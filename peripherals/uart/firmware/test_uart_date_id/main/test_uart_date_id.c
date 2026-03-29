/**
 * test_uart_date_id — ESP32-C3 uart peripheral test
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
    printf("[UART] === test_uart_date_id ===\n");

    /* Read DATE register */
    print_reg("UART_DATE_REG", UART_DATE_REG(0));
    uint32_t date_val = REG_READ_RAW(UART_DATE_REG(0));

    /* Read ID register */
    print_reg("UART_ID_REG", UART_ID_REG(0));
    uint32_t id_val = REG_READ_RAW(UART_ID_REG(0));

    if (date_val != 0 && id_val != 0) {
        printf("[UART] TEST_PASS\n");
    } else {
        printf("[UART] TEST_FAIL: DATE=0x%08lx ID=0x%08lx\n",
               (unsigned long)date_val, (unsigned long)id_val);
    }

    printf("[UART] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
