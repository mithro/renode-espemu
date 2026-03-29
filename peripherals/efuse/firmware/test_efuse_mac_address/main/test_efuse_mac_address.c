/**
 * test_efuse_mac_address — ESP32-C3 efuse peripheral test
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
    printf("[EFUSE] === test_efuse_mac_address ===\n");

    print_reg("EFUSE_RD_MAC_SPI_SYS_0_REG", EFUSE_RD_MAC_SPI_SYS_0_REG);
    print_reg("EFUSE_RD_MAC_SPI_SYS_1_REG", EFUSE_RD_MAC_SPI_SYS_1_REG);

    uint32_t mac0 = REG_READ_RAW(EFUSE_RD_MAC_SPI_SYS_0_REG);
    uint32_t mac1 = REG_READ_RAW(EFUSE_RD_MAC_SPI_SYS_1_REG);

    /* MAC is stored as: mac0[31:0] = byte5..byte2, mac1[15:0] = byte1..byte0 */
    uint8_t mac[6];
    mac[0] = (mac1 >> 8) & 0xFF;
    mac[1] = (mac1 >> 0) & 0xFF;
    mac[2] = (mac0 >> 24) & 0xFF;
    mac[3] = (mac0 >> 16) & 0xFF;
    mac[4] = (mac0 >> 8) & 0xFF;
    mac[5] = (mac0 >> 0) & 0xFF;

    printf("[EFUSE] MAC address: %02x:%02x:%02x:%02x:%02x:%02x\n",
           mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);

    if (mac0 != 0 || (mac1 & 0xFFFF) != 0) {
        printf("[EFUSE] TEST_PASS\n");
    } else {
        printf("[EFUSE] TEST_FAIL: MAC address is all zeros\n");
    }

    printf("[EFUSE] === Done ===\n");

    while (1) { vTaskDelay(1000 / portTICK_PERIOD_MS); }
}
