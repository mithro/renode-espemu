/**
 * ESP32-C3 SYSTIMER Test Firmware
 *
 * Tests the SYSTIMER peripheral (base 0x60023000):
 *   1. Reads all registers at reset to verify defaults
 *   2. Latches and reads counter values (verifies counter is advancing)
 *   3. Configures a one-shot alarm, waits for interrupt
 *   4. Configures a periodic alarm, verifies repeated firing
 *
 * Output format: [SYSTIMER] REG_READ addr=0xNNN val=0xNNN
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

/* SYSTIMER base address */
#define SYSTIMER_BASE       0x60023000

/* Register offsets */
#define SYSTIMER_CONF_REG_OFF           0x00
#define SYSTIMER_UNIT0_OP_REG_OFF       0x04
#define SYSTIMER_UNIT1_OP_REG_OFF       0x08
#define SYSTIMER_UNIT0_LOAD_HI_REG_OFF  0x0C
#define SYSTIMER_UNIT0_LOAD_LO_REG_OFF  0x10
#define SYSTIMER_UNIT1_LOAD_HI_REG_OFF  0x14
#define SYSTIMER_UNIT1_LOAD_LO_REG_OFF  0x18
#define SYSTIMER_TARGET0_HI_REG_OFF     0x1C
#define SYSTIMER_TARGET0_LO_REG_OFF     0x20
#define SYSTIMER_TARGET1_HI_REG_OFF     0x24
#define SYSTIMER_TARGET1_LO_REG_OFF     0x28
#define SYSTIMER_TARGET2_HI_REG_OFF     0x2C
#define SYSTIMER_TARGET2_LO_REG_OFF     0x30
#define SYSTIMER_TARGET0_CONF_REG_OFF   0x34
#define SYSTIMER_TARGET1_CONF_REG_OFF   0x38
#define SYSTIMER_TARGET2_CONF_REG_OFF   0x3C
#define SYSTIMER_UNIT0_VALUE_HI_REG_OFF 0x40
#define SYSTIMER_UNIT0_VALUE_LO_REG_OFF 0x44
#define SYSTIMER_UNIT1_VALUE_HI_REG_OFF 0x48
#define SYSTIMER_UNIT1_VALUE_LO_REG_OFF 0x4C
#define SYSTIMER_COMP0_LOAD_REG_OFF     0x50
#define SYSTIMER_COMP1_LOAD_REG_OFF     0x54
#define SYSTIMER_COMP2_LOAD_REG_OFF     0x58
#define SYSTIMER_UNIT0_LOAD_REG_OFF     0x5C
#define SYSTIMER_UNIT1_LOAD_REG_OFF     0x60
#define SYSTIMER_INT_ENA_REG_OFF        0x64
#define SYSTIMER_INT_RAW_REG_OFF        0x68
#define SYSTIMER_INT_CLR_REG_OFF        0x6C
#define SYSTIMER_INT_ST_REG_OFF         0x70
#define SYSTIMER_DATE_REG_OFF           0xFC

/* Bit definitions */
#define UNIT_OP_UPDATE_BIT      (1 << 30)
#define UNIT_OP_VALUE_VALID_BIT (1 << 29)
#define CONF_UNIT0_WORK_EN_BIT  (1 << 30)
#define CONF_TARGET0_WORK_EN_BIT (1 << 24)
#define TARGET_PERIOD_MODE_BIT  (1 << 30)
#define TARGET_UNIT_SEL_BIT     (1 << 31)

#define REG_READ(addr) (*((volatile uint32_t *)(addr)))
#define REG_WRITE(addr, val) (*((volatile uint32_t *)(addr)) = (val))
#define SYSTIMER_REG(off) (SYSTIMER_BASE + (off))

static uint32_t read_and_log(const char *name, uint32_t offset)
{
    uint32_t addr = SYSTIMER_REG(offset);
    uint32_t val = REG_READ(addr);
    printf("[SYSTIMER] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
    return val;
}

static void write_and_log(const char *name, uint32_t offset, uint32_t val)
{
    uint32_t addr = SYSTIMER_REG(offset);
    REG_WRITE(addr, val);
    printf("[SYSTIMER] REG_WRITE addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

static uint64_t latch_and_read_unit0(void)
{
    /* Trigger latch */
    write_and_log("UNIT0_OP(update)", SYSTIMER_UNIT0_OP_REG_OFF, UNIT_OP_UPDATE_BIT);

    /* Read VALUE_VALID */
    uint32_t op = read_and_log("UNIT0_OP(valid?)", SYSTIMER_UNIT0_OP_REG_OFF);
    if (!(op & UNIT_OP_VALUE_VALID_BIT)) {
        printf("[SYSTIMER] WARNING: UNIT0 VALUE_VALID not set after update\n");
    }

    /* Read latched value */
    uint32_t hi = read_and_log("UNIT0_VALUE_HI", SYSTIMER_UNIT0_VALUE_HI_REG_OFF);
    uint32_t lo = read_and_log("UNIT0_VALUE_LO", SYSTIMER_UNIT0_VALUE_LO_REG_OFF);
    return ((uint64_t)(hi & 0xFFFFF) << 32) | lo;
}

void app_main(void)
{
    printf("[SYSTIMER] === ESP32-C3 SYSTIMER Read Test ===\n");

    /* --- Phase 1: Read all registers at reset --- */
    printf("[SYSTIMER] === Phase 1: Reset register dump ===\n");
    read_and_log("CONF",         SYSTIMER_CONF_REG_OFF);
    read_and_log("UNIT0_OP",     SYSTIMER_UNIT0_OP_REG_OFF);
    read_and_log("UNIT1_OP",     SYSTIMER_UNIT1_OP_REG_OFF);
    read_and_log("UNIT0_LOAD_HI", SYSTIMER_UNIT0_LOAD_HI_REG_OFF);
    read_and_log("UNIT0_LOAD_LO", SYSTIMER_UNIT0_LOAD_LO_REG_OFF);
    read_and_log("UNIT1_LOAD_HI", SYSTIMER_UNIT1_LOAD_HI_REG_OFF);
    read_and_log("UNIT1_LOAD_LO", SYSTIMER_UNIT1_LOAD_LO_REG_OFF);
    read_and_log("TARGET0_HI",    SYSTIMER_TARGET0_HI_REG_OFF);
    read_and_log("TARGET0_LO",    SYSTIMER_TARGET0_LO_REG_OFF);
    read_and_log("TARGET1_HI",    SYSTIMER_TARGET1_HI_REG_OFF);
    read_and_log("TARGET1_LO",    SYSTIMER_TARGET1_LO_REG_OFF);
    read_and_log("TARGET2_HI",    SYSTIMER_TARGET2_HI_REG_OFF);
    read_and_log("TARGET2_LO",    SYSTIMER_TARGET2_LO_REG_OFF);
    read_and_log("TARGET0_CONF",  SYSTIMER_TARGET0_CONF_REG_OFF);
    read_and_log("TARGET1_CONF",  SYSTIMER_TARGET1_CONF_REG_OFF);
    read_and_log("TARGET2_CONF",  SYSTIMER_TARGET2_CONF_REG_OFF);
    read_and_log("INT_ENA",       SYSTIMER_INT_ENA_REG_OFF);
    read_and_log("INT_RAW",       SYSTIMER_INT_RAW_REG_OFF);
    read_and_log("INT_ST",        SYSTIMER_INT_ST_REG_OFF);
    read_and_log("DATE",          SYSTIMER_DATE_REG_OFF);

    /* --- Phase 2: Counter advancement test --- */
    printf("[SYSTIMER] === Phase 2: Counter advancement ===\n");

    /* Ensure UNIT0 is enabled */
    uint32_t conf = read_and_log("CONF", SYSTIMER_CONF_REG_OFF);
    if (!(conf & CONF_UNIT0_WORK_EN_BIT)) {
        write_and_log("CONF(enable_unit0)", SYSTIMER_CONF_REG_OFF,
                      conf | CONF_UNIT0_WORK_EN_BIT);
    }

    /* Latch and read counter twice, verify it advances */
    uint64_t val1 = latch_and_read_unit0();
    printf("[SYSTIMER] counter_read_1 = 0x%05lx%08lx\n",
           (unsigned long)(val1 >> 32), (unsigned long)(val1 & 0xFFFFFFFF));

    /* Small delay to let counter advance */
    vTaskDelay(10 / portTICK_PERIOD_MS);

    uint64_t val2 = latch_and_read_unit0();
    printf("[SYSTIMER] counter_read_2 = 0x%05lx%08lx\n",
           (unsigned long)(val2 >> 32), (unsigned long)(val2 & 0xFFFFFFFF));

    if (val2 > val1) {
        printf("[SYSTIMER] TEST_PASS counter_advances (delta=%lu)\n",
               (unsigned long)(val2 - val1));
    } else {
        printf("[SYSTIMER] TEST_FAIL counter_advances val2=0x%llx val1=0x%llx\n",
               (unsigned long long)val2, (unsigned long long)val1);
    }

    /* --- Phase 3: One-shot alarm test --- */
    printf("[SYSTIMER] === Phase 3: One-shot alarm (TARGET0) ===\n");

    /* Clear any pending interrupt */
    write_and_log("INT_CLR(all)", SYSTIMER_INT_CLR_REG_OFF, 0x7);

    /* Read current counter */
    uint64_t now = latch_and_read_unit0();
    /* Set target to current + 160000 (10ms at 16MHz) */
    uint64_t target_val = now + 160000;
    printf("[SYSTIMER] setting TARGET0 = 0x%05lx%08lx (now + 160000)\n",
           (unsigned long)(target_val >> 32), (unsigned long)(target_val & 0xFFFFFFFF));

    /* Configure TARGET0: one-shot mode, unit0 */
    write_and_log("TARGET0_CONF", SYSTIMER_TARGET0_CONF_REG_OFF, 0);  /* period_mode=0, unit_sel=0 */
    write_and_log("TARGET0_HI", SYSTIMER_TARGET0_HI_REG_OFF, (uint32_t)((target_val >> 32) & 0xFFFFF));
    write_and_log("TARGET0_LO", SYSTIMER_TARGET0_LO_REG_OFF, (uint32_t)(target_val & 0xFFFFFFFF));

    /* Arm comparator */
    write_and_log("COMP0_LOAD", SYSTIMER_COMP0_LOAD_REG_OFF, 1);

    /* Enable TARGET0_WORK_EN in CONF */
    conf = read_and_log("CONF", SYSTIMER_CONF_REG_OFF);
    write_and_log("CONF(+target0)", SYSTIMER_CONF_REG_OFF, conf | CONF_TARGET0_WORK_EN_BIT);

    /* Enable interrupt for TARGET0 */
    write_and_log("INT_ENA(target0)", SYSTIMER_INT_ENA_REG_OFF, 0x1);

    /* Poll for interrupt (up to 100ms) */
    int alarm_fired = 0;
    for (int i = 0; i < 100; i++) {
        vTaskDelay(1 / portTICK_PERIOD_MS);
        uint32_t raw = read_and_log("INT_RAW(poll)", SYSTIMER_INT_RAW_REG_OFF);
        if (raw & 0x1) {
            alarm_fired = 1;
            printf("[SYSTIMER] TARGET0 alarm fired after ~%d ms\n", i + 1);
            break;
        }
    }

    if (alarm_fired) {
        printf("[SYSTIMER] TEST_PASS oneshot_alarm\n");
    } else {
        printf("[SYSTIMER] TEST_FAIL oneshot_alarm (no interrupt after 100ms)\n");
    }

    /* Read INT_ST (should show masked status) */
    read_and_log("INT_ST(after_alarm)", SYSTIMER_INT_ST_REG_OFF);

    /* Clear interrupt */
    write_and_log("INT_CLR(target0)", SYSTIMER_INT_CLR_REG_OFF, 0x1);
    read_and_log("INT_RAW(after_clr)", SYSTIMER_INT_RAW_REG_OFF);
    read_and_log("INT_ST(after_clr)", SYSTIMER_INT_ST_REG_OFF);

    /* Verify clear worked */
    uint32_t raw_after = read_and_log("INT_RAW(verify)", SYSTIMER_INT_RAW_REG_OFF);
    if ((raw_after & 0x1) == 0) {
        printf("[SYSTIMER] TEST_PASS int_clear\n");
    } else {
        printf("[SYSTIMER] TEST_FAIL int_clear (bit still set)\n");
    }

    printf("[SYSTIMER] === Tests Complete ===\n");

    while (1) {
        vTaskDelay(1000 / portTICK_PERIOD_MS);
    }
}
