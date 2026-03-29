/**
 * test_gpio_in_loopback — ESP32-C3 gpio peripheral test
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
    printf("[GPIO] === test_gpio_in_loopback ===\n");

    /* Set GPIO 2 output value */
    REG_WRITE_RAW(GPIO_OUT_W1TS_REG, (1 << 2));
    printf("[GPIO] REG_WRITE addr=0x%08lx val=0x00000004  GPIO_OUT_W1TS_REG (set bit 2)\n",
           (unsigned long)GPIO_OUT_W1TS_REG);

    /* Enable GPIO 2 as output */
    REG_WRITE_RAW(GPIO_ENABLE_W1TS_REG, (1 << 2));
    printf("[GPIO] REG_WRITE addr=0x%08lx val=0x00000004  GPIO_ENABLE_W1TS_REG (set bit 2)\n",
           (unsigned long)GPIO_ENABLE_W1TS_REG);

    /* Read GPIO_IN_REG and check bit 2 */
    uint32_t in_val = REG_READ_RAW(GPIO_IN_REG);
    printf("[GPIO] REG_READ  addr=0x%08lx val=0x%08lx  GPIO_IN_REG\n",
           (unsigned long)GPIO_IN_REG, (unsigned long)in_val);

    if (in_val & (1 << 2)) {
        printf("[GPIO] TEST_PASS\n");
    } else {
        printf("[GPIO] TEST_FAIL (bit 2 not set in GPIO_IN_REG)\n");
    }

    printf("[GPIO] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
