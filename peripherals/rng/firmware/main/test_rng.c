/**
 * ESP32-C3 RNG Test Firmware
 *
 * Reads WDEV_RND_REG multiple times and validates basic randomness.
 * WDEV_RND_REG = 0x600260B0
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "soc/wdev_reg.h"

#define REG_READ_RAW(addr) (*((volatile uint32_t *)(addr)))

#define NUM_READS 10

void app_main(void)
{
    printf("[RNG] === ESP32-C3 RNG Read Test ===\n");
    printf("[RNG] WDEV_RND_REG = 0x%08lx\n", (unsigned long)WDEV_RND_REG);

    uint32_t values[NUM_READS];
    int distinct = 0;
    int nonzero = 0;

    printf("[RNG] === Reading %d values ===\n", NUM_READS);
    for (int i = 0; i < NUM_READS; i++) {
        values[i] = REG_READ_RAW(WDEV_RND_REG);
        printf("[RNG] REG_READ  addr=0x%08lx val=0x%08lx  read_%d\n",
               (unsigned long)WDEV_RND_REG, (unsigned long)values[i], i);
        if (values[i] != 0) nonzero++;
    }

    /* Count distinct values */
    for (int i = 0; i < NUM_READS; i++) {
        int is_first = 1;
        for (int j = 0; j < i; j++) {
            if (values[j] == values[i]) {
                is_first = 0;
                break;
            }
        }
        if (is_first) distinct++;
    }

    printf("[RNG] === Test Results ===\n");
    printf("[RNG] nonzero=%d distinct=%d\n", nonzero, distinct);

    if (nonzero >= NUM_READS - 1) {
        printf("[RNG] TEST_PASS values_nonzero\n");
    } else {
        printf("[RNG] TEST_FAIL values_nonzero expected=>=%d got=%d\n",
               NUM_READS - 1, nonzero);
    }

    if (distinct >= 3) {
        printf("[RNG] TEST_PASS values_distinct\n");
    } else {
        printf("[RNG] TEST_FAIL values_distinct expected=>=3 got=%d\n", distinct);
    }

    printf("[RNG] === Tests Complete ===\n");

    while (1) {
        vTaskDelay(1000 / portTICK_PERIOD_MS);
    }
}
