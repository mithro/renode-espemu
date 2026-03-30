/**
 * test_ledc_reset_values — Read reset values from ledc peripheral at 0x60019000
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

#define BASE 0x60019000u
#define REG_READ(addr) (*((volatile uint32_t *)(addr)))

void app_main(void)
{
    printf("[LEDC] === ledc reset values (base=0x60019000) ===\n");
    for (int i = 0; i < 16; i++) {
        uint32_t addr = BASE + i * 4;
        uint32_t val = REG_READ(addr);
        printf("[LEDC] REG_READ  addr=0x%08lx val=0x%08lx  offset_0x%02x\n",
               (unsigned long)addr, (unsigned long)val, i * 4);
    }
    printf("[LEDC] === Done ===\n");
    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
