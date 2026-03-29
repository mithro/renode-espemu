/**
 * test_system_rst_en_reset — ESP32-C3 system peripheral test
 *
 * Tests a specific feature of the system peripheral.
 * Output format: [SYSTEM] REG_READ addr=0xNNN val=0xNNN register_name
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "soc/system_reg.h"

#define REG_READ_RAW(addr) (*((volatile uint32_t *)(addr)))
#define REG_WRITE_RAW(addr, val) (*((volatile uint32_t *)(addr)) = (val))

static void print_reg(const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[SYSTEM] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

void app_main(void)
{
    printf("[SYSTEM] === test_system_rst_en_reset ===\n");

    print_reg("SYSTEM_PERIP_RST_EN0_REG",  SYSTEM_PERIP_RST_EN0_REG);
    print_reg("SYSTEM_PERIP_RST_EN1_REG",  SYSTEM_PERIP_RST_EN1_REG);
    print_reg("SYSTEM_CPU_PERI_RST_EN_REG", SYSTEM_CPU_PERI_RST_EN_REG);

    printf("[SYSTEM] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
