/**
 * test_sha_reset_values — Read reset values from sha peripheral at 0x6003B000
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

#define BASE 0x6003B000u
#define REG_READ(addr) (*((volatile uint32_t *)(addr)))

void app_main(void)
{
    printf("[SHA] === sha reset values (base=0x6003B000) ===\n");
    for (int i = 0; i < 16; i++) {
        uint32_t addr = BASE + i * 4;
        uint32_t val = REG_READ(addr);
        printf("[SHA] REG_READ  addr=0x%08lx val=0x%08lx  offset_0x%02x\n",
               (unsigned long)addr, (unsigned long)val, i * 4);
    }
    printf("[SHA] === Done ===\n");
    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
