/**
 * test_uhci0_reset_values — Read reset values from uhci0 peripheral at 0x60014000
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

#define BASE 0x60014000u
#define REG_READ(addr) (*((volatile uint32_t *)(addr)))

void app_main(void)
{
    printf("[UHCI0] === uhci0 reset values (base=0x60014000) ===\n");
    for (int i = 0; i < 16; i++) {
        uint32_t addr = BASE + i * 4;
        uint32_t val = REG_READ(addr);
        printf("[UHCI0] REG_READ  addr=0x%08lx val=0x%08lx  offset_0x%02x\n",
               (unsigned long)addr, (unsigned long)val, i * 4);
    }
    printf("[UHCI0] === Done ===\n");
    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
