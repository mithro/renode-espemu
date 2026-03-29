/**
 * test_syscon_rng_reads — ESP32-C3 rng peripheral test
 *
 * Tests a specific feature of the rng peripheral.
 * Output format: [SYSCON] REG_READ addr=0xNNN val=0xNNN register_name
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "soc/syscon_reg.h"

#define REG_READ_RAW(addr) (*((volatile uint32_t *)(addr)))
#define REG_WRITE_RAW(addr, val) (*((volatile uint32_t *)(addr)) = (val))

static void print_reg(const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[SYSCON] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

void app_main(void)
{
    printf("[SYSCON] === test_syscon_rng_reads ===\n");

    #define RNG_DATA_REG 0x600260B0
    #define NUM_READS 20

    uint32_t values[NUM_READS];
    for (int i = 0; i < NUM_READS; i++) {
        values[i] = REG_READ_RAW(RNG_DATA_REG);
        printf("[SYSCON] REG_READ  addr=0x%08lx val=0x%08lx  SYSCON_RND_DATA_REG [%d]\n",
               (unsigned long)RNG_DATA_REG, (unsigned long)values[i], i);
    }

    /* Count distinct values */
    int distinct = 0;
    for (int i = 0; i < NUM_READS; i++) {
        int unique = 1;
        for (int j = 0; j < i; j++) {
            if (values[i] == values[j]) {
                unique = 0;
                break;
            }
        }
        distinct += unique;
    }

    printf("[SYSCON] Distinct values: %d / %d\n", distinct, NUM_READS);

    if (distinct >= 15) {
        printf("[SYSCON] TEST_PASS\n");
    } else {
        printf("[SYSCON] TEST_FAIL (expected >= 15 distinct, got %d)\n", distinct);
    }

    printf("[SYSCON] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
