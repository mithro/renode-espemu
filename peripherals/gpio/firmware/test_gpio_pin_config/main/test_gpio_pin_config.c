/**
 * test_gpio_pin_config — ESP32-C3 gpio peripheral test
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
    printf("[GPIO] === test_gpio_pin_config ===\n");

    /* Write INT_TYPE=edge (0x10) to PIN0, PIN5, PIN21 and read back */
    REG_WRITE_RAW(GPIO_PIN0_REG, 0x10);
    printf("[GPIO] REG_WRITE addr=0x%08lx val=0x00000010  GPIO_PIN0_REG\n",
           (unsigned long)GPIO_PIN0_REG);
    print_reg("GPIO_PIN0_REG (readback)", GPIO_PIN0_REG);

    REG_WRITE_RAW(GPIO_PIN5_REG, 0x10);
    printf("[GPIO] REG_WRITE addr=0x%08lx val=0x00000010  GPIO_PIN5_REG\n",
           (unsigned long)GPIO_PIN5_REG);
    print_reg("GPIO_PIN5_REG (readback)", GPIO_PIN5_REG);

    REG_WRITE_RAW(GPIO_PIN21_REG, 0x10);
    printf("[GPIO] REG_WRITE addr=0x%08lx val=0x00000010  GPIO_PIN21_REG\n",
           (unsigned long)GPIO_PIN21_REG);
    print_reg("GPIO_PIN21_REG (readback)", GPIO_PIN21_REG);

    printf("[GPIO] TEST_PASS\n");

    printf("[GPIO] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
