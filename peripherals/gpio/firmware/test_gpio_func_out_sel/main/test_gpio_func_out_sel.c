/**
 * test_gpio_func_out_sel — ESP32-C3 gpio peripheral test
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
    printf("[GPIO] === test_gpio_func_out_sel ===\n");

    /* Read FUNC0..FUNC4 OUT_SEL_CFG -- default should be 0x80 */
    print_reg("GPIO_FUNC0_OUT_SEL_CFG_REG", GPIO_FUNC0_OUT_SEL_CFG_REG);
    print_reg("GPIO_FUNC1_OUT_SEL_CFG_REG", GPIO_FUNC1_OUT_SEL_CFG_REG);
    print_reg("GPIO_FUNC2_OUT_SEL_CFG_REG", GPIO_FUNC2_OUT_SEL_CFG_REG);
    print_reg("GPIO_FUNC3_OUT_SEL_CFG_REG", GPIO_FUNC3_OUT_SEL_CFG_REG);
    print_reg("GPIO_FUNC4_OUT_SEL_CFG_REG", GPIO_FUNC4_OUT_SEL_CFG_REG);

    uint32_t v0 = REG_READ_RAW(GPIO_FUNC0_OUT_SEL_CFG_REG);
    uint32_t v1 = REG_READ_RAW(GPIO_FUNC1_OUT_SEL_CFG_REG);
    uint32_t v2 = REG_READ_RAW(GPIO_FUNC2_OUT_SEL_CFG_REG);
    uint32_t v3 = REG_READ_RAW(GPIO_FUNC3_OUT_SEL_CFG_REG);
    uint32_t v4 = REG_READ_RAW(GPIO_FUNC4_OUT_SEL_CFG_REG);

    if (v0 == 0x80 && v1 == 0x80 && v2 == 0x80 && v3 == 0x80 && v4 == 0x80) {
        printf("[GPIO] TEST_PASS\n");
    } else {
        printf("[GPIO] TEST_FAIL (expected all 0x80)\n");
    }

    printf("[GPIO] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
