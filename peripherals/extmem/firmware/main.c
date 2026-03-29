/*
 * ESP32-C3 ExtMem (Cache/MMU) peripheral test firmware
 *
 * Validates that the Renode ExtMem model returns correct values for
 * boot-critical registers: ICACHE_ENABLE, sync done, preload done,
 * autoload done, cache state idle, and freeze done.
 *
 * Uses UART0 TX (0x60000000) for output, no ESP-IDF dependencies.
 */

#include <stdint.h>

#define UART0_FIFO  (*(volatile uint32_t *)0x60000000)
#define EXTMEM_BASE 0x600C4000

#define REG(off) (*(volatile uint32_t *)(EXTMEM_BASE + (off)))

#define ICACHE_CTRL_REG          REG(0x00)
#define ICACHE_CTRL1_REG         REG(0x04)
#define ICACHE_TAG_POWER_CTRL_REG REG(0x08)
#define ICACHE_SYNC_CTRL_REG     REG(0x28)
#define ICACHE_SYNC_ADDR_REG     REG(0x2C)
#define ICACHE_SYNC_SIZE_REG     REG(0x30)
#define ICACHE_PRELOAD_CTRL_REG  REG(0x34)
#define ICACHE_AUTOLOAD_CTRL_REG REG(0x40)
#define CACHE_STATE_REG          REG(0xB0)
#define CACHE_CONF_MISC_REG      REG(0xC8)
#define ICACHE_FREEZE_REG        REG(0xCC)
#define CLOCK_GATE_REG           REG(0x100)
#define DATE_REG                 REG(0x3FC)

static void uart_putc(char c)
{
    UART0_FIFO = (uint32_t)c;
}

static void uart_puts(const char *s)
{
    while (*s)
        uart_putc(*s++);
}

static void uart_puthex(uint32_t val)
{
    const char hex[] = "0123456789ABCDEF";
    uart_puts("0x");
    for (int i = 28; i >= 0; i -= 4)
        uart_putc(hex[(val >> i) & 0xF]);
}

static int check(const char *name, uint32_t actual, uint32_t expected)
{
    uart_puts(name);
    uart_puts(": ");
    uart_puthex(actual);
    if (actual == expected) {
        uart_puts(" OK\n");
        return 0;
    }
    uart_puts(" FAIL (expected ");
    uart_puthex(expected);
    uart_puts(")\n");
    return 1;
}

void main(void)
{
    int fail = 0;

    uart_puts("\n=== ExtMem Test ===\n");

    /* 1. ICACHE_ENABLE must be set */
    fail += check("ICACHE_CTRL (enable)",
                   ICACHE_CTRL_REG & 0x1, 0x1);

    /* 2. ICACHE_CTRL1 defaults: both buses shut */
    fail += check("ICACHE_CTRL1 (default)",
                   ICACHE_CTRL1_REG & 0x3, 0x3);

    /* 3. TAG_POWER_CTRL: FORCE_ON=1, FORCE_PD=0, FORCE_PU=1 => 0x5 */
    fail += check("TAG_POWER_CTRL",
                   ICACHE_TAG_POWER_CTRL_REG & 0x7, 0x5);

    /* 4. Sync done bit must be set */
    fail += check("SYNC_CTRL (done)",
                   (ICACHE_SYNC_CTRL_REG >> 1) & 0x1, 0x1);

    /* 5. Write sync addr/size, verify readback */
    ICACHE_SYNC_ADDR_REG = 0x42010000;
    fail += check("SYNC_ADDR readback",
                   ICACHE_SYNC_ADDR_REG, 0x42010000);
    ICACHE_SYNC_SIZE_REG = 0x1000;
    fail += check("SYNC_SIZE readback",
                   ICACHE_SYNC_SIZE_REG, 0x1000);

    /* 6. Preload done bit must be set */
    fail += check("PRELOAD_CTRL (done)",
                   (ICACHE_PRELOAD_CTRL_REG >> 1) & 0x1, 0x1);

    /* 7. Autoload done bit must be set */
    fail += check("AUTOLOAD_CTRL (done)",
                   (ICACHE_AUTOLOAD_CTRL_REG >> 3) & 0x1, 0x1);

    /* 8. Cache state = idle (0x001) */
    fail += check("CACHE_STATE (idle)",
                   CACHE_STATE_REG & 0xFFF, 0x001);

    /* 9. CACHE_CONF_MISC defaults: all fault-ignore bits set */
    fail += check("CACHE_CONF_MISC",
                   CACHE_CONF_MISC_REG & 0x7, 0x7);

    /* 10. Freeze done (always reports done) */
    fail += check("ICACHE_FREEZE (done)",
                   (ICACHE_FREEZE_REG >> 2) & 0x1, 0x1);

    /* 11. CLOCK_GATE default: CLK_EN=1 */
    fail += check("CLOCK_GATE",
                   CLOCK_GATE_REG & 0x1, 0x1);

    /* 12. DATE register */
    fail += check("DATE",
                   DATE_REG & 0x0FFFFFFF, 0x02007160);

    /* Summary */
    uart_puts("---\n");
    if (fail == 0)
        uart_puts("PASS: all ExtMem checks passed\n");
    else {
        uart_puts("FAIL: ");
        uart_puthex((uint32_t)fail);
        uart_puts(" check(s) failed\n");
    }

    /* Halt */
    while (1)
        __asm__ volatile("wfi");
}
