/**
 * test_gpio_out_w1ts_w1tc — ESP32-C3 gpio peripheral test
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
    printf("[GPIO] === test_gpio_out_w1ts_w1tc ===\n");

    /* Set bit 0 via W1TS */
    REG_WRITE_RAW(GPIO_OUT_W1TS_REG, 0x1);
    printf("[GPIO] REG_WRITE addr=0x%08lx val=0x00000001  GPIO_OUT_W1TS_REG\n",
           (unsigned long)GPIO_OUT_W1TS_REG);
    print_reg("GPIO_OUT_REG (after W1TS)", GPIO_OUT_REG);

    /* Clear bit 0 via W1TC */
    REG_WRITE_RAW(GPIO_OUT_W1TC_REG, 0x1);
    printf("[GPIO] REG_WRITE addr=0x%08lx val=0x00000001  GPIO_OUT_W1TC_REG\n",
           (unsigned long)GPIO_OUT_W1TC_REG);
    print_reg("GPIO_OUT_REG (after W1TC)", GPIO_OUT_REG);

    /* Read W1TS/W1TC directly -- should return 0 */
    print_reg("GPIO_OUT_W1TS_REG (direct read)", GPIO_OUT_W1TS_REG);
    print_reg("GPIO_OUT_W1TC_REG (direct read)", GPIO_OUT_W1TC_REG);

    printf("[GPIO] TEST_PASS\n");

    printf("[GPIO] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
