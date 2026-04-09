/**
 * test_efuse_int_regs — ESP32-C3 efuse peripheral test
 *
 * Tests a specific feature of the efuse peripheral.
 * Output format: [EFUSE] REG_READ addr=0xNNN val=0xNNN register_name
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "soc/efuse_reg.h"

#define REG_READ_RAW(addr) (*((volatile uint32_t *)(addr)))
#define REG_WRITE_RAW(addr, val) (*((volatile uint32_t *)(addr)) = (val))

static void print_reg(const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[EFUSE] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

void app_main(void)
{
    printf("[EFUSE] === test_efuse_int_regs ===\n");

    /* Use correct offsets from ESP-IDF efuse_reg.h (soc/esp32c3) */
    #define EFUSE_INT_RAW_ADDR EFUSE_INT_RAW_REG   /* 0x60008800 + 0x1D8 */
    #define EFUSE_INT_ST_ADDR  EFUSE_INT_ST_REG    /* 0x60008800 + 0x1DC */
    #define EFUSE_INT_ENA_ADDR EFUSE_INT_ENA_REG   /* 0x60008800 + 0x1E0 */
    #define EFUSE_INT_CLR_ADDR EFUSE_INT_CLR_REG   /* 0x60008800 + 0x1E4 */

    /* Step 1: Read all interrupt registers at reset -- expect 0 */
    print_reg("EFUSE_INT_RAW (reset)", EFUSE_INT_RAW_ADDR);
    print_reg("EFUSE_INT_ST  (reset)", EFUSE_INT_ST_ADDR);
    print_reg("EFUSE_INT_ENA (reset)", EFUSE_INT_ENA_ADDR);

    uint32_t raw0 = REG_READ_RAW(EFUSE_INT_RAW_ADDR);
    uint32_t st0  = REG_READ_RAW(EFUSE_INT_ST_ADDR);
    uint32_t ena0 = REG_READ_RAW(EFUSE_INT_ENA_ADDR);

    if (raw0 == 0 && st0 == 0 && ena0 == 0) {
        printf("[EFUSE] TEST_PASS: all interrupt regs are 0 at reset\n");
    } else {
        printf("[EFUSE] TEST_FAIL: interrupt regs not 0 at reset\n");
    }

    /* Step 2: Enable interrupt, read back */
    REG_WRITE_RAW(EFUSE_INT_ENA_ADDR, 0x1);
    print_reg("EFUSE_INT_ENA (after write 0x1)", EFUSE_INT_ENA_ADDR);

    uint32_t ena1 = REG_READ_RAW(EFUSE_INT_ENA_ADDR);
    if (ena1 == 0x1) {
        printf("[EFUSE] TEST_PASS: INT_ENA readback correct\n");
    } else {
        printf("[EFUSE] TEST_FAIL: INT_ENA readback=0x%08lx expected 0x1\n",
               (unsigned long)ena1);
    }

    /* Step 3: INT_ST should still be 0 since RAW=0 */
    print_reg("EFUSE_INT_ST  (after ENA=1)", EFUSE_INT_ST_ADDR);

    uint32_t st1 = REG_READ_RAW(EFUSE_INT_ST_ADDR);
    if (st1 == 0) {
        printf("[EFUSE] TEST_PASS: INT_ST still 0 (RAW=0)\n");
    } else {
        printf("[EFUSE] TEST_FAIL: INT_ST=0x%08lx expected 0\n",
               (unsigned long)st1);
    }

    /* Step 4: Write INT_CLR */
    REG_WRITE_RAW(EFUSE_INT_CLR_ADDR, 0x1);
    printf("[EFUSE] REG_WRITE addr=0x%08lx val=0x00000001  EFUSE_INT_CLR\n",
           (unsigned long)EFUSE_INT_CLR_ADDR);
    printf("[EFUSE] TEST_PASS: INT_CLR write completed\n");

    printf("[EFUSE] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
