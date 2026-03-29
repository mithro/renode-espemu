/**
 * ESP32-C3 GPIO Test Firmware
 *
 * Reads and writes GPIO registers to verify the Renode peripheral model.
 * Tests output, enable, W1TS/W1TC semantics, per-pin config, and function mux.
 *
 * GPIO base: DR_REG_GPIO_BASE = 0x60004000
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "soc/gpio_reg.h"

/* Direct register read/write macros */
#define REG_READ_RAW(addr)       (*((volatile uint32_t *)(addr)))
#define REG_WRITE_RAW(addr, val) (*((volatile uint32_t *)(addr)) = (val))

static int test_pass_count = 0;
static int test_fail_count = 0;

static void check(const char *name, uint32_t addr, uint32_t expected, uint32_t mask)
{
    uint32_t val = REG_READ_RAW(addr) & mask;
    uint32_t exp = expected & mask;
    if (val == exp) {
        printf("[GPIO] TEST_PASS %s val=0x%08lx\n", name, (unsigned long)val);
        test_pass_count++;
    } else {
        printf("[GPIO] TEST_FAIL %s expected=0x%08lx got=0x%08lx\n",
               name, (unsigned long)exp, (unsigned long)val);
        test_fail_count++;
    }
}

static void print_reg(const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[GPIO] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

void app_main(void)
{
    printf("[GPIO] === ESP32-C3 GPIO Register Test ===\n");
    printf("[GPIO] DR_REG_GPIO_BASE = 0x%08lx\n", (unsigned long)DR_REG_GPIO_BASE);

    /* --- Read initial state --- */
    printf("[GPIO] === Initial State ===\n");
    print_reg("GPIO_OUT_REG",    GPIO_OUT_REG);
    print_reg("GPIO_ENABLE_REG", GPIO_ENABLE_REG);
    print_reg("GPIO_IN_REG",     GPIO_IN_REG);
    print_reg("GPIO_STATUS_REG", GPIO_STATUS_REG);
    print_reg("GPIO_STRAP_REG",  GPIO_STRAP_REG);
    print_reg("GPIO_DATE_REG",   GPIO_DATE_REG);

    /* --- Test 1: OUT register write/read --- */
    printf("[GPIO] === Test: OUT register ===\n");
    REG_WRITE_RAW(GPIO_OUT_REG, 0x00000000);
    check("out_clear", GPIO_OUT_REG, 0x00000000, 0x003FFFFF);

    REG_WRITE_RAW(GPIO_OUT_REG, 0x0000000A);
    check("out_write_0A", GPIO_OUT_REG, 0x0000000A, 0x003FFFFF);

    /* --- Test 2: OUT W1TS/W1TC --- */
    printf("[GPIO] === Test: OUT W1TS/W1TC ===\n");
    REG_WRITE_RAW(GPIO_OUT_REG, 0x00000000);
    REG_WRITE_RAW(GPIO_OUT_W1TS_REG, 0x00000005);  /* set bits 0,2 */
    check("out_w1ts_05", GPIO_OUT_REG, 0x00000005, 0x003FFFFF);

    REG_WRITE_RAW(GPIO_OUT_W1TS_REG, 0x0000000A);  /* set bits 1,3 */
    check("out_w1ts_0F", GPIO_OUT_REG, 0x0000000F, 0x003FFFFF);

    REG_WRITE_RAW(GPIO_OUT_W1TC_REG, 0x00000003);  /* clear bits 0,1 */
    check("out_w1tc_0C", GPIO_OUT_REG, 0x0000000C, 0x003FFFFF);

    /* --- Test 3: ENABLE register + W1TS/W1TC --- */
    printf("[GPIO] === Test: ENABLE W1TS/W1TC ===\n");
    REG_WRITE_RAW(GPIO_ENABLE_REG, 0x00000000);
    check("enable_clear", GPIO_ENABLE_REG, 0x00000000, 0x003FFFFF);

    REG_WRITE_RAW(GPIO_ENABLE_W1TS_REG, 0x00000FF0);
    check("enable_w1ts", GPIO_ENABLE_REG, 0x00000FF0, 0x003FFFFF);

    REG_WRITE_RAW(GPIO_ENABLE_W1TC_REG, 0x000000F0);
    check("enable_w1tc", GPIO_ENABLE_REG, 0x00000F00, 0x003FFFFF);

    /* --- Test 4: STATUS W1TS/W1TC --- */
    printf("[GPIO] === Test: STATUS W1TS/W1TC ===\n");
    REG_WRITE_RAW(GPIO_STATUS_REG, 0x00000000);
    REG_WRITE_RAW(GPIO_STATUS_W1TS_REG, 0x00000007);
    check("status_w1ts", GPIO_STATUS_REG, 0x00000007, 0x003FFFFF);

    REG_WRITE_RAW(GPIO_STATUS_W1TC_REG, 0x00000005);
    check("status_w1tc", GPIO_STATUS_REG, 0x00000002, 0x003FFFFF);

    /* --- Test 5: PIN config registers --- */
    printf("[GPIO] === Test: PINn config ===\n");
    print_reg("GPIO_PIN0_REG", GPIO_PIN0_REG);
    print_reg("GPIO_PIN1_REG", GPIO_PIN1_REG);

    /* Write wakeup enable + int type to PIN0 */
    REG_WRITE_RAW(GPIO_PIN0_REG, (1 << 10) | (3 << 7));  /* wakeup=1, int_type=3 */
    check("pin0_wakeup_inttype", GPIO_PIN0_REG, (1 << 10) | (3 << 7), (1 << 10) | (7 << 7));

    /* --- Test 6: FUNC_OUT_SEL_CFG default --- */
    printf("[GPIO] === Test: FUNC_OUT_SEL defaults ===\n");
    check("func0_out_sel_default", GPIO_FUNC0_OUT_SEL_CFG_REG, 0x80, 0xFF);

    /* --- Test 7: DATE register --- */
    printf("[GPIO] === Test: DATE register ===\n");
    check("date_version", GPIO_DATE_REG, 0x2006130, 0x0FFFFFFF);

    /* --- Summary --- */
    printf("[GPIO] === Tests Complete ===\n");
    printf("[GPIO] PASSED=%d FAILED=%d TOTAL=%d\n",
           test_pass_count, test_fail_count, test_pass_count + test_fail_count);

    while (1) {
        vTaskDelay(1000 / portTICK_PERIOD_MS);
    }
}
