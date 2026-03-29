/**
 * ESP32-C3 SYSTEM Peripheral Test Firmware
 *
 * Reads clock config and control registers, tests cross-core interrupt trigger,
 * and prints structured output for comparison between hardware, QEMU, and Renode.
 *
 * SYSTEM base: DR_REG_SYSTEM_BASE = 0x600C0000
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "soc/system_reg.h"

/* Direct register read/write macros */
#define REG_READ_RAW(addr)       (*((volatile uint32_t *)(addr)))
#define REG_WRITE_RAW(addr, val) (*((volatile uint32_t *)(addr)) = (val))

static void print_reg(const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[SYSTEM] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

void app_main(void)
{
    printf("[SYSTEM] === ESP32-C3 SYSTEM Peripheral Test ===\n");
    printf("[SYSTEM] DR_REG_SYSTEM_BASE = 0x%08lx\n", (unsigned long)DR_REG_SYSTEM_BASE);

    /* --- Clock Configuration Registers --- */
    printf("[SYSTEM] === Clock Config ===\n");
    print_reg("CPU_PERI_CLK_EN",   SYSTEM_CPU_PERI_CLK_EN_REG);
    print_reg("CPU_PERI_RST_EN",   SYSTEM_CPU_PERI_RST_EN_REG);
    print_reg("CPU_PER_CONF",      SYSTEM_CPU_PER_CONF_REG);
    print_reg("SYSCLK_CONF",       SYSTEM_SYSCLK_CONF_REG);
    print_reg("CLOCK_GATE",        SYSTEM_CLOCK_GATE_REG);

    /* --- Peripheral Clock Enable/Reset --- */
    printf("[SYSTEM] === Peripheral Clock/Reset ===\n");
    print_reg("PERIP_CLK_EN0",     SYSTEM_PERIP_CLK_EN0_REG);
    print_reg("PERIP_CLK_EN1",     SYSTEM_PERIP_CLK_EN1_REG);
    print_reg("PERIP_RST_EN0",     SYSTEM_PERIP_RST_EN0_REG);
    print_reg("PERIP_RST_EN1",     SYSTEM_PERIP_RST_EN1_REG);

    /* --- Memory/Cache Control --- */
    printf("[SYSTEM] === Memory/Cache ===\n");
    print_reg("MEM_PD_MASK",       SYSTEM_MEM_PD_MASK_REG);
    print_reg("RSA_PD_CTRL",       SYSTEM_RSA_PD_CTRL_REG);
    print_reg("CACHE_CONTROL",     SYSTEM_CACHE_CONTROL_REG);

    /* --- BT Low-Power Clock --- */
    printf("[SYSTEM] === BT LP Clock ===\n");
    print_reg("BT_LPCK_DIV_INT",   SYSTEM_BT_LPCK_DIV_INT_REG);
    print_reg("BT_LPCK_DIV_FRAC",  SYSTEM_BT_LPCK_DIV_FRAC_REG);

    /* --- Misc --- */
    printf("[SYSTEM] === Misc ===\n");
    print_reg("EDMA_CTRL",         SYSTEM_EDMA_CTRL_REG);
    print_reg("REDUNDANT_ECO_CTRL", SYSTEM_REDUNDANT_ECO_CTRL_REG);
    print_reg("DATE",              SYSTEM_DATE_REG);

    /* --- Cross-Core Interrupt Test --- */
    printf("[SYSTEM] === Cross-Core Interrupt Test ===\n");

    /* Read initial state (should be 0) */
    print_reg("CPU_INTR_FROM_CPU_0 (before)", SYSTEM_CPU_INTR_FROM_CPU_0_REG);
    print_reg("CPU_INTR_FROM_CPU_1 (before)", SYSTEM_CPU_INTR_FROM_CPU_1_REG);

    /* Trigger FROM_CPU_INTR0 by writing 1 */
    printf("[SYSTEM] Writing 1 to CPU_INTR_FROM_CPU_0 (trigger)\n");
    REG_WRITE_RAW(SYSTEM_CPU_INTR_FROM_CPU_0_REG, 1);
    print_reg("CPU_INTR_FROM_CPU_0 (after set)", SYSTEM_CPU_INTR_FROM_CPU_0_REG);

    /* Clear FROM_CPU_INTR0 by writing 0 */
    printf("[SYSTEM] Writing 0 to CPU_INTR_FROM_CPU_0 (clear)\n");
    REG_WRITE_RAW(SYSTEM_CPU_INTR_FROM_CPU_0_REG, 0);
    print_reg("CPU_INTR_FROM_CPU_0 (after clear)", SYSTEM_CPU_INTR_FROM_CPU_0_REG);

    /* --- Decode SYSCLK_CONF fields --- */
    printf("[SYSTEM] === Decoded Fields ===\n");
    uint32_t sysclk = REG_READ_RAW(SYSTEM_SYSCLK_CONF_REG);
    uint32_t pre_div_cnt = (sysclk >> SYSTEM_PRE_DIV_CNT_S) & SYSTEM_PRE_DIV_CNT_V;
    uint32_t soc_clk_sel = (sysclk >> SYSTEM_SOC_CLK_SEL_S) & SYSTEM_SOC_CLK_SEL_V;
    uint32_t clk_xtal_freq = (sysclk >> SYSTEM_CLK_XTAL_FREQ_S) & SYSTEM_CLK_XTAL_FREQ_V;
    printf("[SYSTEM] SYSCLK_CONF: PRE_DIV_CNT=%lu SOC_CLK_SEL=%lu CLK_XTAL_FREQ=%lu\n",
           (unsigned long)pre_div_cnt, (unsigned long)soc_clk_sel,
           (unsigned long)clk_xtal_freq);

    const char *clk_src_names[] = {"XTAL", "PLL", "RC_FAST", "RESERVED"};
    printf("[SYSTEM] Clock source: %s, XTAL=%luMHz\n",
           clk_src_names[soc_clk_sel & 0x3], (unsigned long)clk_xtal_freq);

    /* --- Test Results --- */
    printf("[SYSTEM] === Test Results ===\n");

    /* Check XTAL frequency is 40MHz */
    if (clk_xtal_freq == 40) {
        printf("[SYSTEM] TEST_PASS xtal_freq_40mhz\n");
    } else {
        printf("[SYSTEM] TEST_FAIL xtal_freq_40mhz expected=40 got=%lu\n",
               (unsigned long)clk_xtal_freq);
    }

    /* Check cross-core interrupt was clearable */
    uint32_t intr_after_clear = REG_READ_RAW(SYSTEM_CPU_INTR_FROM_CPU_0_REG);
    if (intr_after_clear == 0) {
        printf("[SYSTEM] TEST_PASS cpu_intr_clearable\n");
    } else {
        printf("[SYSTEM] TEST_FAIL cpu_intr_clearable expected=0 got=0x%08lx\n",
               (unsigned long)intr_after_clear);
    }

    /* Check DATE register is nonzero */
    uint32_t date = REG_READ_RAW(SYSTEM_DATE_REG);
    if (date != 0) {
        printf("[SYSTEM] TEST_PASS date_nonzero\n");
    } else {
        printf("[SYSTEM] TEST_FAIL date_nonzero expected=nonzero got=0\n");
    }

    printf("[SYSTEM] === Tests Complete ===\n");

    /* Keep running so FreeRTOS doesn't complain about task return */
    while (1) {
        vTaskDelay(1000 / portTICK_PERIOD_MS);
    }
}
