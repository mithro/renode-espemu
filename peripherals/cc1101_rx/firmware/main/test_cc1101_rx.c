/**
 * ESP32-C3 CC1101 RF-Receive (air-medium) Test Firmware
 *
 * Proves the wave-3 RF receive path end-to-end in emulation: the firmware
 * configures the Renode SPI.CC1101 model for packet RX over SPI2, arms RX, and
 * waits. A test then injects a known frame onto the shared Air433Medium
 * (`air InjectFrame "..."`). The model loads that frame into the RX FIFO, sets
 * RXBYTES, and asserts the packet-received event on GDO0 (IOCFG0 = 0x07). GDO0
 * is wired to GPIO4, whose rising-edge interrupt runs the ISR below. The ISR
 * flags the main loop, which reads the frame back over SPI and checks it byte
 * for byte and confirms the interrupt fired.
 *
 * Output uses the repo [TAG] convention with TAG = CC1101RX.
 */
#include <stdio.h>
#include <string.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/spi_master.h"
#include "driver/gpio.h"

#define TAG "[CC1101RX]"

#define PIN_MOSI   7
#define PIN_MISO   2
#define PIN_SCLK   6
#define PIN_CS    10
#define PIN_GDO0   4   /* wired to CC1101 GDO0 (numbered output 0) in .repl */

/* CC1101 header bits */
#define CC_WRITE       0x00
#define CC_READ        0x80
#define CC_BURST       0x40

/* Registers / status regs / strobes */
#define REG_IOCFG0     0x02
#define REG_PKTLEN     0x06
#define REG_PKTCTRL0   0x08
#define SR_RXBYTES     0x3B
#define ADDR_FIFO      0x3F
#define STROBE_SRES    0x30
#define STROBE_SRX     0x34
#define STROBE_SFRX    0x3A

/* IOCFG0 = 0x07: GDO0 asserts when a packet is received with good CRC. */
#define GDO_CFG_PKT_CRC_OK  0x07

/* The frame the test injects on the air medium (must match the Robot suite). */
static const uint8_t EXPECTED[] = { 0xA7, 0x11, 0x22, 0x33, 0x44, 0x55 };
#define EXPECTED_LEN (sizeof(EXPECTED))

static int pass_count = 0;
static int fail_count = 0;

static volatile int rx_isr_count = 0;
static volatile int rx_flag = 0;

static spi_device_handle_t dev;

static void IRAM_ATTR gdo0_isr(void *arg)
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

static uint8_t cc_strobe(uint8_t addr)
{
    uint8_t tx = addr, rx = 0;
    spi_transaction_t t = { .length = 8, .tx_buffer = &tx, .rx_buffer = &rx };
    spi_device_polling_transmit(dev, &t);
    return rx;
}

static void cc_write_reg(uint8_t addr, uint8_t value)
{
    uint8_t tx[2] = { (uint8_t)(CC_WRITE | addr), value };
    uint8_t rx[2] = { 0 };
    spi_transaction_t t = { .length = 16, .tx_buffer = tx, .rx_buffer = rx };
    spi_device_polling_transmit(dev, &t);
}

static uint8_t cc_read_status(uint8_t addr)
{
    uint8_t tx[2] = { (uint8_t)(CC_READ | CC_BURST | addr), 0x00 };
    uint8_t rx[2] = { 0 };
    spi_transaction_t t = { .length = 16, .tx_buffer = tx, .rx_buffer = rx };
    spi_device_polling_transmit(dev, &t);
    return rx[1];
}

/* Burst-read n bytes out of the RX FIFO (header 0xFF = READ|BURST|0x3F). */
static void cc_read_fifo(uint8_t *dst, int n)
{
    uint8_t tx[1 + 16] = { (uint8_t)(CC_READ | CC_BURST | ADDR_FIFO) };
    uint8_t rx[1 + 16] = { 0 };
    spi_transaction_t t = { .length = 8 * (n + 1), .tx_buffer = tx, .rx_buffer = rx };
    spi_device_polling_transmit(dev, &t);
    memcpy(dst, &rx[1], n);
}

void app_main(void)
{
    printf("%s === ESP32-C3 CC1101 RF-RX Test ===\n", TAG);

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

    /* Reset + configure packet RX with GDO0 as the packet-received IRQ. */
    cc_strobe(STROBE_SRES);
    cc_write_reg(REG_IOCFG0, GDO_CFG_PKT_CRC_OK);
    cc_write_reg(REG_PKTLEN, (uint8_t)EXPECTED_LEN);
    cc_write_reg(REG_PKTCTRL0, 0x00);   /* fixed packet length */

    /* GDO0 -> GPIO4: rising-edge interrupt -> gdo0_isr. */
    gpio_config_t io = {
        .pin_bit_mask = 1ULL << PIN_GDO0,
        .mode = GPIO_MODE_INPUT,
        .pull_up_en = GPIO_PULLUP_DISABLE,
        .pull_down_en = GPIO_PULLDOWN_ENABLE,
        .intr_type = GPIO_INTR_POSEDGE,
    };
    gpio_config(&io);
    gpio_install_isr_service(0);
    gpio_isr_handler_add(PIN_GDO0, gdo0_isr, NULL);

    /* Arm RX and announce readiness so the test can inject the frame. */
    cc_strobe(STROBE_SFRX);
    cc_strobe(STROBE_SRX);
    printf("%s isr_count before=%d\n", TAG, rx_isr_count);
    printf("%s RX armed\n", TAG);

    /* Wait for the packet-received interrupt (bounded). */
    for (int i = 0; i < 500 && rx_flag == 0; i++) {
        vTaskDelay(1);
    }

    printf("%s isr_count after=%d\n", TAG, rx_isr_count);
    check("irq_fired", (uint32_t)(rx_isr_count > 0), 1);

    /* Read RXBYTES, then drain the RX FIFO and compare to the injected frame. */
    uint8_t rxbytes = cc_read_status(SR_RXBYTES);
    printf("%s rxbytes=%d\n", TAG, rxbytes);
    check("rxbytes", rxbytes, EXPECTED_LEN);

    uint8_t buf[16] = { 0 };
    int n = rxbytes;
    if (n > (int)sizeof(buf)) {
        n = sizeof(buf);
    }
    cc_read_fifo(buf, n);
    printf("%s frame=", TAG);
    for (int i = 0; i < n; i++) {
        printf("%02x ", buf[i]);
    }
    printf("\n");

    int match = (n == (int)EXPECTED_LEN);
    for (int i = 0; i < (int)EXPECTED_LEN && i < n; i++) {
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
