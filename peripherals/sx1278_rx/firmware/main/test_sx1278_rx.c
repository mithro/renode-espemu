/**
 * ESP32-C3 SX1278 (SX127x) FSK RF-Receive (air-medium) Test Firmware
 *
 * Proves the wave-3 SX1278 FSK receive path end-to-end in emulation: the
 * firmware puts the Renode SPI.SX1278 model into FSK RX over SPI2, maps DIO0 to
 * PayloadReady, and waits. A test then injects a known frame onto the shared
 * Air433Medium (`air InjectFrame "..."`). The model loads that frame into its
 * RX FIFO (readable via RegFifo 0x00) and asserts DIO0, wired to GPIO4, whose
 * rising-edge interrupt runs the ISR below. The ISR flags the main loop, which
 * reads the payload back over SPI and checks it byte-for-byte plus the interrupt
 * count.
 *
 * Output uses the repo [TAG] convention with TAG = SX1278RX.
 */
#include <stdio.h>
#include <string.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/spi_master.h"
#include "driver/gpio.h"

#define TAG "[SX1278RX]"

#define PIN_MOSI   7
#define PIN_MISO   2
#define PIN_SCLK   6
#define PIN_CS    10
#define PIN_DIO0   4   /* wired to SX1278 DIO0 (numbered output 0) in .repl */

/* SX127x registers */
#define REG_FIFO          0x00
#define REG_OPMODE        0x01
#define REG_DIOMAPPING1   0x40
#define REG_VERSION       0x42

#define WNR_WRITE         0x80   /* address bit7 = 1 -> write */

/* RegOpMode: FSK (LongRangeMode=0), mode field bits[2:0]. */
#define MODE_FSK_RX       0x05   /* FSK/OOK receiver */
#define DIO0_PAYLOAD_RDY  0x00   /* RegDioMapping1[7:6]=00 -> DIO0 = PayloadReady */

/* The frame the test injects on the air medium (must match the Robot suite). */
static const uint8_t EXPECTED[] = { 0xB4, 0xC3, 0xD2, 0xE1, 0xF0 };
#define EXPECTED_LEN (sizeof(EXPECTED))

static int pass_count = 0;
static int fail_count = 0;

static volatile int rx_isr_count = 0;
static volatile int rx_flag = 0;

static spi_device_handle_t s_dev;

static void IRAM_ATTR dio0_isr(void *arg)
{
    (void)arg;
    rx_isr_count++;
    rx_flag = 1;
}

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

/* Burst-read n bytes out of RegFifo (address 0x00 is the FIFO port). */
static void sx_read_fifo(uint8_t *dst, int n)
{
    uint8_t tx[1 + 16] = { REG_FIFO & 0x7F };
    uint8_t rx[1 + 16] = { 0 };
    spi_transaction_t t = { .length = 8 * (n + 1), .tx_buffer = tx, .rx_buffer = rx };
    spi_device_polling_transmit(s_dev, &t);
    memcpy(dst, &rx[1], n);
}

void app_main(void)
{
    printf("%s === ESP32-C3 SX1278 FSK RF-RX Test ===\n", TAG);

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

    /* Sanity: chip id. */
    check("version", sx_read_reg(REG_VERSION), 0x12);

    /* DIO0 -> PayloadReady, then enter FSK RX. */
    sx_write_reg(REG_DIOMAPPING1, DIO0_PAYLOAD_RDY);
    sx_write_reg(REG_OPMODE, MODE_FSK_RX);
    printf("%s opmode=0x%02x\n", TAG, sx_read_reg(REG_OPMODE));

    /* DIO0 -> GPIO4: rising-edge interrupt -> dio0_isr. */
    gpio_config_t io = {
        .pin_bit_mask = 1ULL << PIN_DIO0,
        .mode = GPIO_MODE_INPUT,
        .pull_up_en = GPIO_PULLUP_DISABLE,
        .pull_down_en = GPIO_PULLDOWN_ENABLE,
        .intr_type = GPIO_INTR_POSEDGE,
    };
    gpio_config(&io);
    gpio_install_isr_service(0);
    gpio_isr_handler_add(PIN_DIO0, dio0_isr, NULL);

    printf("%s isr_count before=%d\n", TAG, rx_isr_count);
    printf("%s RX armed\n", TAG);

    for (int i = 0; i < 500 && rx_flag == 0; i++) {
        vTaskDelay(1);
    }

    printf("%s isr_count after=%d\n", TAG, rx_isr_count);
    check("irq_fired", (uint32_t)(rx_isr_count > 0), 1);

    uint8_t buf[16] = { 0 };
    sx_read_fifo(buf, EXPECTED_LEN);
    printf("%s frame=", TAG);
    for (int i = 0; i < (int)EXPECTED_LEN; i++) {
        printf("%02x ", buf[i]);
    }
    printf("\n");

    int match = 1;
    for (int i = 0; i < (int)EXPECTED_LEN; i++) {
        if (buf[i] != EXPECTED[i]) {
            match = 0;
        }
    }
    check("rx_frame", (uint32_t)match, 1);

    printf("%s === Tests Complete ===\n", TAG);
    printf("%s PASSED=%d FAILED=%d TOTAL=%d\n", TAG, pass_count, fail_count,
           pass_count + fail_count);

    while (1) {
        vTaskDelay(1000 / portTICK_PERIOD_MS);
    }
}
