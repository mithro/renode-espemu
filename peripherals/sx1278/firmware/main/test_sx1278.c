/**
 * ESP32-C3 SX1278 (SX127x) Radio Test Firmware
 *
 * Exercises the Renode SPI.SX1278 model over the SPI2 (GP-SPI) master, the same
 * way the real SX1278 driver does its low-level register access: single-register
 * reads/writes and burst access using the SX127x address framing (bit7 = wnr).
 *
 * Coverage (foundation wave):
 *   - identify(): read RegVersion (0x42) == 0x12.
 *   - RegOpMode (0x01): default, SLEEP transition, LoRa-mode enable, and the
 *     datasheet rule that LongRangeMode (bit7) is locked unless in SLEEP.
 *   - A datasheet POR default (RegBitrateMsb 0x02 == 0x1A).
 *   - Ordinary register write/read-back (RegDioMapping1, RegPreambleLsb).
 *   - Burst write + burst read-back (address auto-increment).
 *
 * Output uses the repo [TAG] convention with TAG = SX1278.
 */
#include <stdio.h>
#include <string.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/spi_master.h"

#define TAG "[SX1278]"

#define PIN_MOSI   7
#define PIN_MISO   2
#define PIN_SCLK   6
#define PIN_CS    10

/* SX127x registers */
#define REG_FIFO          0x00
#define REG_OPMODE        0x01
#define REG_BITRATE_MSB   0x02
#define REG_PREAMBLE_LSB  0x26
#define REG_SYNCVALUE1    0x28
#define REG_DIOMAPPING1   0x40
#define REG_VERSION       0x42

#define MODE_SLEEP        0x00
#define MODE_STDBY        0x01
#define LONG_RANGE_MODE   0x80   /* RegOpMode bit7 (LoRa) */

#define WNR_WRITE         0x80   /* address bit7 = 1 -> write */

static int pass_count = 0;
static int fail_count = 0;

static spi_device_handle_t s_dev;

static uint8_t sx_read_reg(uint8_t addr)
{
    uint8_t tx[2] = { (uint8_t)(addr & 0x7F), 0x00 };
    uint8_t rx[2] = { 0, 0 };
    spi_transaction_t t = { .length = 16, .tx_buffer = tx, .rx_buffer = rx };
    spi_device_polling_transmit(s_dev, &t);
    return rx[1];
}

static void sx_write_reg(uint8_t addr, uint8_t val)
{
    uint8_t tx[2] = { (uint8_t)(addr | WNR_WRITE), val };
    spi_transaction_t t = { .length = 16, .tx_buffer = tx };
    spi_device_polling_transmit(s_dev, &t);
}

static void sx_write_burst(uint8_t addr, const uint8_t *data, int n)
{
    uint8_t tx[8];
    tx[0] = (uint8_t)(addr | WNR_WRITE);
    memcpy(&tx[1], data, n);
    spi_transaction_t t = { .length = 8 * (n + 1), .tx_buffer = tx };
    spi_device_polling_transmit(s_dev, &t);
}

static void sx_read_burst(uint8_t addr, uint8_t *data, int n)
{
    uint8_t tx[8] = { 0 };
    uint8_t rx[8] = { 0 };
    tx[0] = (uint8_t)(addr & 0x7F);
    spi_transaction_t t = { .length = 8 * (n + 1), .tx_buffer = tx, .rx_buffer = rx };
    spi_device_polling_transmit(s_dev, &t);
    memcpy(data, &rx[1], n);
}

static void check(const char *name, uint8_t got, uint8_t expected)
{
    if (got == expected) {
        printf("%s TEST_PASS %s got=0x%02x\n", TAG, name, got);
        pass_count++;
    } else {
        printf("%s TEST_FAIL %s expected=0x%02x got=0x%02x\n", TAG, name, expected, got);
        fail_count++;
    }
}

void app_main(void)
{
    printf("%s === ESP32-C3 SX1278 Radio Test ===\n", TAG);

    spi_bus_config_t buscfg = {
        .mosi_io_num = PIN_MOSI,
        .miso_io_num = PIN_MISO,
        .sclk_io_num = PIN_SCLK,
        .quadwp_io_num = -1,
        .quadhd_io_num = -1,
        .max_transfer_sz = 64,
    };
    esp_err_t err = spi_bus_initialize(SPI2_HOST, &buscfg, SPI_DMA_DISABLED);
    printf("%s spi_bus_initialize rc=%d\n", TAG, (int)err);

    spi_device_interface_config_t devcfg = {
        .clock_speed_hz = 1 * 1000 * 1000,
        .mode = 0,
        .spics_io_num = PIN_CS,
        .queue_size = 1,
    };
    err = spi_bus_add_device(SPI2_HOST, &devcfg, &s_dev);
    printf("%s spi_bus_add_device rc=%d\n", TAG, (int)err);

    /* --- identify(): RegVersion --- */
    uint8_t version = sx_read_reg(REG_VERSION);
    printf("%s RegVersion=0x%02x\n", TAG, version);
    check("version", version, 0x12);

    /* --- RegOpMode default --- */
    check("opmode_default", sx_read_reg(REG_OPMODE), 0x01);

    /* --- Enter SLEEP --- */
    sx_write_reg(REG_OPMODE, MODE_SLEEP);
    check("opmode_sleep", sx_read_reg(REG_OPMODE), MODE_SLEEP);

    /* --- Enable LoRa (LongRangeMode) while in SLEEP: allowed --- */
    sx_write_reg(REG_OPMODE, LONG_RANGE_MODE | MODE_SLEEP);
    check("opmode_lora", sx_read_reg(REG_OPMODE), (uint8_t)(LONG_RANGE_MODE | MODE_SLEEP));

    /* --- Move to STDBY (still LoRa), then try to clear LoRa while NOT in
     *     SLEEP: the datasheet locks LongRangeMode, so bit7 stays set. --- */
    sx_write_reg(REG_OPMODE, LONG_RANGE_MODE | MODE_STDBY);
    sx_write_reg(REG_OPMODE, MODE_STDBY);  /* attempt to clear bit7 in STDBY */
    check("opmode_lora_locked", sx_read_reg(REG_OPMODE), (uint8_t)(LONG_RANGE_MODE | MODE_STDBY));

    /* --- Ordinary register write/read-back --- */
    sx_write_reg(REG_DIOMAPPING1, 0x40);
    check("dio_mapping", sx_read_reg(REG_DIOMAPPING1), 0x40);

    /* --- Burst write + burst read-back (address auto-increment) --- */
    uint8_t wb[3] = { 0x11, 0x22, 0x33 };
    uint8_t rb[3] = { 0 };
    sx_write_burst(REG_SYNCVALUE1, wb, 3);
    sx_read_burst(REG_SYNCVALUE1, rb, 3);
    printf("%s burst rb=%02x %02x %02x\n", TAG, rb[0], rb[1], rb[2]);
    check("burst", (uint8_t)(rb[0] == 0x11 && rb[1] == 0x22 && rb[2] == 0x33), 1);

    /* --- POR default of an ordinary register --- */
    check("bitrate_default", sx_read_reg(REG_BITRATE_MSB), 0x1A);

    /* --- Generic register round-trip --- */
    sx_write_reg(REG_PREAMBLE_LSB, 0x07);
    check("reg_roundtrip", sx_read_reg(REG_PREAMBLE_LSB), 0x07);

    printf("%s === Tests Complete ===\n", TAG);
    printf("%s PASSED=%d FAILED=%d TOTAL=%d\n", TAG, pass_count, fail_count, pass_count + fail_count);

    while (1) {
        vTaskDelay(1000 / portTICK_PERIOD_MS);
    }
}
