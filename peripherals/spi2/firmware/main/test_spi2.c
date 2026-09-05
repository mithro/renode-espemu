/**
 * ESP32-C3 SPI2 (GP-SPI) Master Test Firmware
 *
 * Exercises the Renode ESP32C3_SPI2 master model against the SPILoopbackTester
 * slave (which returns each byte XOR 0xA5 and raises an IRQ line on byte 0xAA).
 *
 * Part A (SPI round-trip, the wave-1 gate):
 *   spi_bus_initialize(SPI2_HOST) + spi_bus_add_device() +
 *   spi_device_polling_transmit() full-duplex exchanges; verifies each received
 *   byte equals the transmitted byte XOR 0xA5.
 *
 * Part B (slave-driven interrupt):
 *   Configures GPIO4 (wired to the tester's IRQ output in the test .repl) as a
 *   rising-edge interrupt, then sends SPI byte 0xAA so the slave asserts its
 *   IRQ. The GPIO ISR must fire, proving a slave interrupt reaches the CPU.
 *
 * Output uses the repo [TAG] convention with TAG = SPI2.
 */
#include <stdio.h>
#include <string.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/spi_master.h"
#include "driver/gpio.h"

#define TAG "[SPI2]"

#define PIN_MOSI   7
#define PIN_MISO   2
#define PIN_SCLK   6
#define PIN_CS    10
#define PIN_IRQ    4

#define XFORM_KEY 0xA5   /* SPILoopbackTester returns (byte ^ 0xA5) */
#define IRQ_BYTE  0xAA   /* SPILoopbackTester asserts IRQ on this byte */

static int pass_count = 0;
static int fail_count = 0;

static volatile int irq_count = 0;

static void IRAM_ATTR irq_handler(void *arg)
{
    (void)arg;
    irq_count++;
}

static void check_byte(const char *name, uint8_t got, uint8_t expected)
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
    printf("%s === ESP32-C3 SPI2 Master Test ===\n", TAG);

    /* --- Bus + device init (CPU buffer path, no DMA) --- */
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
    spi_device_handle_t dev;
    err = spi_bus_add_device(SPI2_HOST, &devcfg, &dev);
    printf("%s spi_bus_add_device rc=%d\n", TAG, (int)err);

    /* --- Part A: full-duplex loopback round-trip --- */
    printf("%s === Part A: loopback round-trip ===\n", TAG);
    uint8_t tx[4] = { 0x01, 0x02, 0x03, 0x04 };
    uint8_t rx[4] = { 0 };
    spi_transaction_t t = {
        .length = sizeof(tx) * 8,
        .tx_buffer = tx,
        .rx_buffer = rx,
    };
    err = spi_device_polling_transmit(dev, &t);
    printf("%s xfer rc=%d rx=%02x %02x %02x %02x\n", TAG, (int)err, rx[0], rx[1], rx[2], rx[3]);
    check_byte("rt0", rx[0], (uint8_t)(tx[0] ^ XFORM_KEY));
    check_byte("rt1", rx[1], (uint8_t)(tx[1] ^ XFORM_KEY));
    check_byte("rt2", rx[2], (uint8_t)(tx[2] ^ XFORM_KEY));
    check_byte("rt3", rx[3], (uint8_t)(tx[3] ^ XFORM_KEY));

    /* Second exchange with different data to prove it is not a fluke. */
    uint8_t tx2[3] = { 0xDE, 0xAD, 0xBE };
    uint8_t rx2[3] = { 0 };
    spi_transaction_t t2 = { .length = sizeof(tx2) * 8, .tx_buffer = tx2, .rx_buffer = rx2 };
    err = spi_device_polling_transmit(dev, &t2);
    printf("%s xfer2 rc=%d rx=%02x %02x %02x\n", TAG, (int)err, rx2[0], rx2[1], rx2[2]);
    check_byte("rt2_0", rx2[0], (uint8_t)(tx2[0] ^ XFORM_KEY));
    check_byte("rt2_1", rx2[1], (uint8_t)(tx2[1] ^ XFORM_KEY));
    check_byte("rt2_2", rx2[2], (uint8_t)(tx2[2] ^ XFORM_KEY));

    /* --- Part B: slave-driven interrupt via GPIO --- */
    printf("%s === Part B: slave-driven IRQ ===\n", TAG);
    gpio_config_t io = {
        .pin_bit_mask = 1ULL << PIN_IRQ,
        .mode = GPIO_MODE_INPUT,
        .pull_up_en = GPIO_PULLUP_DISABLE,
        .pull_down_en = GPIO_PULLDOWN_ENABLE,
        .intr_type = GPIO_INTR_POSEDGE,
    };
    gpio_config(&io);
    gpio_install_isr_service(0);
    gpio_isr_handler_add(PIN_IRQ, irq_handler, NULL);

    printf("%s irq_count before=%d\n", TAG, irq_count);
    uint8_t trig = IRQ_BYTE, trig_rx = 0;
    spi_transaction_t ti = { .length = 8, .tx_buffer = &trig, .rx_buffer = &trig_rx };
    spi_device_polling_transmit(dev, &ti);

    /* Give the interrupt time to be taken and dispatched. */
    for (int i = 0; i < 10 && irq_count == 0; i++) {
        vTaskDelay(1);
    }
    printf("%s irq_count after=%d\n", TAG, irq_count);
    if (irq_count > 0) {
        printf("%s TEST_PASS irq_received count=%d\n", TAG, irq_count);
        pass_count++;
    } else {
        printf("%s TEST_FAIL irq_received count=0\n", TAG);
        fail_count++;
    }

    /* --- Summary --- */
    printf("%s === Tests Complete ===\n", TAG);
    printf("%s PASSED=%d FAILED=%d TOTAL=%d\n", TAG, pass_count, fail_count, pass_count + fail_count);

    while (1) {
        vTaskDelay(1000 / portTICK_PERIOD_MS);
    }
}
