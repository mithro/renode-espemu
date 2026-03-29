/**
 * test_gpio_clock_date — ESP32-C3 gpio peripheral test
 *
 * Tests a specific feature of the gpio peripheral.
 * Output format: [GPIO] REG_READ addr=0xNNN val=0xNNN register_name
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "soc/gpio_reg.h"

#define REG_READ_RAW(addr) (*((volatile uint32_t *)(addr)))
#define REG_WRITE_RAW(addr, val) (*((volatile uint32_t *)(addr)) = (val))

static void print_reg(const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[GPIO] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

void app_main(void)
{
    printf("[GPIO] === test_gpio_clock_date ===\n");

    print_reg("GPIO_CLOCK_GATE_REG", GPIO_CLOCK_GATE_REG);

    uint32_t date_val = REG_READ_RAW(GPIO_DATE_REG);
    printf("[GPIO] REG_READ  addr=0x%08lx val=0x%08lx  GPIO_DATE_REG\n",
           (unsigned long)GPIO_DATE_REG, (unsigned long)date_val);

    if (date_val != 0) {
        printf("[GPIO] TEST_PASS\n");
    } else {
        printf("[GPIO] TEST_FAIL (GPIO_DATE_REG is zero)\n");
    }

    printf("[GPIO] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
