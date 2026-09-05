/**
 * ESP32-C3 CC1101 SPI Model Test Firmware
 *
 * Drives the Renode SPI.CC1101 radio model over the SPI2 (GP-SPI) master the
 * same way the real ESP32-C3 CC1101 driver does, then verifies register-level
 * behaviour. No RF path is exercised (that is a later air-medium wave).
 *
 * Checks:
 *   1. PARTNUM (status 0x30) == 0x00 and VERSION (status 0x31) == 0x14.
 *   2. Config-register write+readback (IOCFG0/PKTLEN) -- the driver always
 *      writes then reads back its configuration.
 *   3. Strobe SRX -> MARCSTATE (status 0x35) == 0x0D (RX).
 *   4. Strobe SIDLE -> MARCSTATE == 0x01 (IDLE).
 *   5. GDO pin-identify: constant-level GDO0/GDO2 via IOCFG (0x2F + INV) drive
 *      GPIO4/GPIO5 (wired to GDO0/GDO2 in the .repl) and read back distinctly.
 *
 * Output uses the repo [TAG] convention with TAG = CC1101.
 */
#include <stdio.h>
#include <string.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/spi_master.h"
#include "driver/gpio.h"

#define TAG "[CC1101]"

#define PIN_MOSI   7
#define PIN_MISO   2
#define PIN_SCLK   6
#define PIN_CS    10
#define PIN_GDO0   4   /* wired to CC1101 GDO0 (numbered output 0) in .repl */
#define PIN_GDO2   5   /* wired to CC1101 GDO2 (numbered output 2) in .repl */

/* CC1101 header bits */
#define CC_WRITE       0x00
#define CC_READ        0x80
#define CC_BURST       0x40

/* Registers / status regs / strobes */
#define REG_IOCFG2     0x00
#define REG_IOCFG0     0x02
#define REG_PKTLEN     0x06
#define SR_PARTNUM     0x30
#define SR_VERSION     0x31
#define SR_MARCSTATE   0x35
#define STROBE_SRES    0x30
#define STROBE_SRX     0x34
#define STROBE_SIDLE   0x36

#define MARC_IDLE      0x01
#define MARC_RX        0x0D

/* IOCFG constant-output settings */
#define GDO_CFG_CONST  0x2F   /* HW to 0 */
#define GDO_INV        0x40   /* GDOx_INV: flips constant to 1 */

static int pass_count = 0;
static int fail_count = 0;

static spi_device_handle_t dev;

static void check(const char *name, uint32_t got, uint32_t expected)
{
    if (got == expected) {
        printf("%s TEST_PASS %s got=0x%02x\n", TAG, name, (unsigned)got);
        pass_count++;
    } else {
        printf("%s TEST_FAIL %s expected=0x%02x got=0x%02x\n", TAG, name,
               (unsigned)expected, (unsigned)got);
        fail_count++;
    }
}

/* Command strobe: single header byte, returns the STATUS byte. */
static uint8_t cc_strobe(uint8_t addr)
{
    uint8_t tx = addr, rx = 0;
    spi_transaction_t t = { .length = 8, .tx_buffer = &tx, .rx_buffer = &rx };
    spi_device_polling_transmit(dev, &t);
    return rx;
}

/* Write one config register. */
static void cc_write_reg(uint8_t addr, uint8_t value)
{
    uint8_t tx[2] = { (uint8_t)(CC_WRITE | addr), value };
    uint8_t rx[2] = { 0 };
    spi_transaction_t t = { .length = 16, .tx_buffer = tx, .rx_buffer = rx };
    spi_device_polling_transmit(dev, &t);
}

/* Read one config register (single). */
static uint8_t cc_read_reg(uint8_t addr)
{
    uint8_t tx[2] = { (uint8_t)(CC_READ | addr), 0x00 };
    uint8_t rx[2] = { 0 };
    spi_transaction_t t = { .length = 16, .tx_buffer = tx, .rx_buffer = rx };
    spi_device_polling_transmit(dev, &t);
    return rx[1];
}

/* Read one status register (requires the burst bit). */
static uint8_t cc_read_status(uint8_t addr)
{
    uint8_t tx[2] = { (uint8_t)(CC_READ | CC_BURST | addr), 0x00 };
    uint8_t rx[2] = { 0 };
    spi_transaction_t t = { .length = 16, .tx_buffer = tx, .rx_buffer = rx };
    spi_device_polling_transmit(dev, &t);
    return rx[1];
}

void app_main(void)
{
    printf("%s === ESP32-C3 CC1101 SPI Model Test ===\n", TAG);

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
    err = spi_bus_add_device(SPI2_HOST, &devcfg, &dev);
    printf("%s spi_bus_add_device rc=%d\n", TAG, (int)err);

    /* Bring the chip to a known state. */
    cc_strobe(STROBE_SRES);

    /* --- Check 1: identity registers --- */
    printf("%s === Part A: identity ===\n", TAG);
    uint8_t partnum = cc_read_status(SR_PARTNUM);
    uint8_t version = cc_read_status(SR_VERSION);
    printf("%s partnum=0x%02x version=0x%02x\n", TAG, partnum, version);
    check("partnum", partnum, 0x00);
    check("version", version, 0x14);

    /* --- Check 2: config write + readback --- */
    printf("%s === Part B: config write/readback ===\n", TAG);
    cc_write_reg(REG_PKTLEN, 0x3D);
    uint8_t pktlen = cc_read_reg(REG_PKTLEN);
    printf("%s pktlen readback=0x%02x\n", TAG, pktlen);
    check("pktlen_rb", pktlen, 0x3D);

    cc_write_reg(REG_IOCFG2, 0x06);
    uint8_t iocfg2 = cc_read_reg(REG_IOCFG2);
    printf("%s iocfg2 readback=0x%02x\n", TAG, iocfg2);
    check("iocfg2_rb", iocfg2, 0x06);

    /* --- Check 3: strobe state machine (MARCSTATE) --- */
    printf("%s === Part C: strobe state machine ===\n", TAG);
    cc_strobe(STROBE_SRX);
    uint8_t marc_rx = cc_read_status(SR_MARCSTATE);
    printf("%s marcstate after SRX=0x%02x\n", TAG, marc_rx);
    check("marc_rx", marc_rx, MARC_RX);

    cc_strobe(STROBE_SIDLE);
    uint8_t marc_idle = cc_read_status(SR_MARCSTATE);
    printf("%s marcstate after SIDLE=0x%02x\n", TAG, marc_idle);
    check("marc_idle", marc_idle, MARC_IDLE);

    /* --- Check 4: GDO constant-level pin identify --- */
    printf("%s === Part D: GDO pin identify ===\n", TAG);
    gpio_config_t io = {
        .pin_bit_mask = (1ULL << PIN_GDO0) | (1ULL << PIN_GDO2),
        .mode = GPIO_MODE_INPUT,
        .pull_up_en = GPIO_PULLUP_DISABLE,
        .pull_down_en = GPIO_PULLDOWN_DISABLE,
        .intr_type = GPIO_INTR_DISABLE,
    };
    gpio_config(&io);

    /* Drive GDO0 high (constant + INV), GDO2 low (constant, no INV). */
    cc_write_reg(REG_IOCFG0, GDO_CFG_CONST | GDO_INV);
    cc_write_reg(REG_IOCFG2, GDO_CFG_CONST);
    int g0_hi = gpio_get_level(PIN_GDO0);
    int g2_lo = gpio_get_level(PIN_GDO2);
    printf("%s gdo0=%d gdo2=%d (want 1,0)\n", TAG, g0_hi, g2_lo);
    check("gdo0_high", (uint32_t)g0_hi, 1);
    check("gdo2_low", (uint32_t)g2_lo, 0);

    /* Swap levels to prove the two lines are independent. */
    cc_write_reg(REG_IOCFG0, GDO_CFG_CONST);
    cc_write_reg(REG_IOCFG2, GDO_CFG_CONST | GDO_INV);
    int g0_lo = gpio_get_level(PIN_GDO0);
    int g2_hi = gpio_get_level(PIN_GDO2);
    printf("%s gdo0=%d gdo2=%d (want 0,1)\n", TAG, g0_lo, g2_hi);
    check("gdo0_low", (uint32_t)g0_lo, 0);
    check("gdo2_high", (uint32_t)g2_hi, 1);

    /* --- Summary --- */
    printf("%s === Tests Complete ===\n", TAG);
    printf("%s PASSED=%d FAILED=%d TOTAL=%d\n", TAG, pass_count, fail_count,
           pass_count + fail_count);

    while (1) {
        vTaskDelay(1000 / portTICK_PERIOD_MS);
    }
}
