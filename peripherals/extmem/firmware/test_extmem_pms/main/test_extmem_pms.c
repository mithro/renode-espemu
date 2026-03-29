/**
 * test_extmem_pms — ESP32-C3 extmem peripheral test
 *
 * Tests a specific feature of the extmem peripheral.
 * Output format: [EXTMEM] REG_READ addr=0xNNN val=0xNNN register_name
 */
#include <stdio.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "soc/extmem_reg.h"

#define REG_READ_RAW(addr) (*((volatile uint32_t *)(addr)))
#define REG_WRITE_RAW(addr, val) (*((volatile uint32_t *)(addr)) = (val))

static void print_reg(const char *name, uint32_t addr)
{
    uint32_t val = REG_READ_RAW(addr);
    printf("[EXTMEM] REG_READ  addr=0x%08lx val=0x%08lx  %s\n",
           (unsigned long)addr, (unsigned long)val, name);
}

void app_main(void)
{
    printf("[EXTMEM] === test_extmem_pms ===\n");

    /* ExtMem base = 0x600C4000 */
    #define EXTMEM_BASE 0x600C4000

    /* Read IBUS PMS registers using base+offset */
    print_reg("EXTMEM_IBUS_PMS_TBL_LOCK_REG", EXTMEM_IBUS_PMS_TBL_LOCK_REG);
    print_reg("EXTMEM_IBUS_PMS_TBL_BOUNDARY0_REG", EXTMEM_IBUS_PMS_TBL_BOUNDARY0_REG);
    print_reg("EXTMEM_IBUS_PMS_TBL_BOUNDARY1_REG", EXTMEM_IBUS_PMS_TBL_BOUNDARY1_REG);
    print_reg("EXTMEM_IBUS_PMS_TBL_BOUNDARY2_REG", EXTMEM_IBUS_PMS_TBL_BOUNDARY2_REG);
    print_reg("EXTMEM_IBUS_PMS_TBL_ATTR_REG", EXTMEM_IBUS_PMS_TBL_ATTR_REG);

    /* Read DBUS PMS registers */
    print_reg("EXTMEM_DBUS_PMS_TBL_LOCK_REG", EXTMEM_DBUS_PMS_TBL_LOCK_REG);
    print_reg("EXTMEM_DBUS_PMS_TBL_BOUNDARY0_REG", EXTMEM_DBUS_PMS_TBL_BOUNDARY0_REG);
    print_reg("EXTMEM_DBUS_PMS_TBL_BOUNDARY1_REG", EXTMEM_DBUS_PMS_TBL_BOUNDARY1_REG);
    print_reg("EXTMEM_DBUS_PMS_TBL_BOUNDARY2_REG", EXTMEM_DBUS_PMS_TBL_BOUNDARY2_REG);
    print_reg("EXTMEM_DBUS_PMS_TBL_ATTR_REG", EXTMEM_DBUS_PMS_TBL_ATTR_REG);

    printf("[EXTMEM] TEST_PASS\n");

    printf("[EXTMEM] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
