/**
 * ESP32-C3 CC1101 real-decoder-in-emulation test firmware.
 *
 * This closes the CI gap that two adversarial reviews flagged: the earlier
 * air-medium RX suite (peripherals/cc1101_rx) proves TRANSPORT only (inject a
 * frame -> RX FIFO -> GDO0 IRQ -> SPI read == bytes) and runs NO decoder. This
 * suite runs the ACTUAL shipped product FSK decoder (decode_fineoffset.c +
 * decode_common.c, vendored under main/decoders/ from the esp32-to-433mhz
 * firmware repo and COMPILED into this firmware) against REAL captured weather
 * (WS69) and soil-moisture (WH51) frames injected on the emulated 433 MHz air
 * medium.
 *
 * Flow, mirroring the product driver (cc1101_weather.cpp cc_weather_drain):
 *   1. Configure the CC1101 for infinite-length FSK RX exactly as the product
 *      preset does: PKTCTRL0 = 0x02 (infinite packet length), GDO0 = packet
 *      received (IOCFG0 = 0x07).
 *   2. Arm RX (SFRX + SRX) and announce readiness.
 *   3. A test injects a REAL frame on the air medium; the CC1101 model loads it
 *      into the RX FIFO and asserts GDO0 -> GPIO4 IRQ.
 *   4. cc_drain_decode(): read RXBYTES, drain CC_FSK_DRAIN_LEN (30) bytes from
 *      the FIFO over SPI (the real frame sits at the FIFO head with demodulated
 *      noise behind it, as infinite-length RX presents it), SFRX + re-enter RX,
 *      then run the PRODUCT fineoffset_decode() on the drained bytes.
 *   5. ASSERT the decoded JSON carries the expected model/id/fields.
 *
 * Two real frames are decoded in sequence: WS69 (weather) then WH51 (moisture).
 *
 * Output uses the repo [TAG] convention with TAG = CC1101DEC.
 */
#include <stdio.h>
#include <string.h>
#include <stdint.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/spi_master.h"
#include "driver/gpio.h"

#include "decode_fineoffset.h"   /* product decoder (vendored, compiled in) */
#include "decode_common.h"       /* RF_DECODE_OK etc. */

#define TAG "[CC1101DEC]"

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
/* PKTCTRL0 = 0x02: infinite packet length (product Fine Offset FSK preset). */
#define PKTCTRL0_INFINITE   0x02

/* Product drain constants (mirror firmware cc1101_weather.h). */
#define CC_FSK_DRAIN_LEN 30   /* >= WS85 28 B; also < 64-byte RX FIFO */
#define CC_FSK_MIN_FRAME 14   /* shortest frame we decode (WH51) */

static int pass_count = 0;
static int fail_count = 0;

static volatile int rx_isr_count = 0;
static spi_device_handle_t dev;

static void IRAM_ATTR gdo0_isr(void *arg)
{
    (void)arg;
    rx_isr_count++;
}

static void pass(const char *name)
{
    printf("%s TEST_PASS %s\n", TAG, name);
    pass_count++;
}

static void fail(const char *name, const char *detail)
{
    printf("%s TEST_FAIL %s (%s)\n", TAG, name, detail);
    fail_count++;
}

/* Assert that the decoded JSON contains a given substring (model/id/field). */
static void check_contains(const char *name, const char *json, const char *needle)
{
    if(strstr(json, needle) != NULL) {
        pass(name);
    } else {
        fail(name, needle);
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
    uint8_t tx[1 + 32] = { (uint8_t)(CC_READ | CC_BURST | ADDR_FIFO) };
    uint8_t rx[1 + 32] = { 0 };
    if(n > 32) n = 32;
    spi_transaction_t t = { .length = 8 * (n + 1), .tx_buffer = tx, .rx_buffer = rx };
    spi_device_polling_transmit(dev, &t);
    memcpy(dst, &rx[1], n);
}

/* One weather-RX poll in the style of the product cc_weather_drain(): read
 * RXBYTES, drain CC_FSK_DRAIN_LEN bytes, re-arm RX, then run the real decoder.
 * Returns the fineoffset_decode() code, or -100 if the FIFO has not yet filled
 * to a full drain (nothing consumed). */
static int cc_drain_decode(char *json, size_t json_len, uint8_t *raw,
                           size_t raw_cap, size_t *out_n)
{
    uint8_t n = cc_read_status(SR_RXBYTES) & 0x7F;
    printf("%s rxbytes=%d\n", TAG, n);
    if(n < CC_FSK_DRAIN_LEN) {
        return -100;   /* frame not fully in the FIFO yet */
    }
    size_t take = CC_FSK_DRAIN_LEN;
    if(take > raw_cap) take = raw_cap;
    cc_read_fifo(raw, (int)take);
    cc_strobe(STROBE_SFRX);   /* flush + re-enter RX: re-arm for the next frame */
    cc_strobe(STROBE_SRX);
    *out_n = take;

    printf("%s drained=", TAG);
    for(size_t i = 0; i < take; i++) printf("%02x ", raw[i]);
    printf("\n");

    return fineoffset_decode(raw, take, json, json_len);   /* PRODUCT decoder */
}

/* Wait until the GDO0 ISR fires again (rx_isr_count advances past prev). */
static int wait_irq(int prev)
{
    for(int i = 0; i < 500 && rx_isr_count <= prev; i++) {
        vTaskDelay(1);
    }
    return rx_isr_count > prev;
}

void app_main(void)
{
    printf("%s === ESP32-C3 CC1101 Real-Decoder Test ===\n", TAG);

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

    /* Configure infinite-length FSK RX exactly as the product preset does:
     * PKTCTRL0 = 0x02 (infinite length) + GDO0 = packet-received IRQ. */
    cc_strobe(STROBE_SRES);
    cc_write_reg(REG_IOCFG0, GDO_CFG_PKT_CRC_OK);
    cc_write_reg(REG_PKTCTRL0, PKTCTRL0_INFINITE);

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

    cc_strobe(STROBE_SFRX);
    cc_strobe(STROBE_SRX);
    printf("%s RX armed\n", TAG);

    char json[RF_JSON_MAX];
    uint8_t raw[CC_FSK_DRAIN_LEN];
    size_t got = 0;

    /* ---- Frame 1: real WS69 weather frame (id 174) ---- */
    int prev = rx_isr_count;
    if(!wait_irq(prev)) {
        fail("ws69_irq", "no GDO0 IRQ");
    } else {
        pass("ws69_irq");
        int rc = cc_drain_decode(json, sizeof json, raw, sizeof raw, &got);
        printf("%s ws69_rc=%d json=%s\n", TAG, rc, json);
        if(rc == RF_DECODE_OK) {
            pass("ws69_decode_ok");
            check_contains("ws69_model", json, "\"model\":\"Fineoffset-WS69\"");
            check_contains("ws69_id",    json, "\"id\":174");
            check_contains("ws69_temp",  json, "\"temperature_C\":13.1");
            check_contains("ws69_hum",   json, "\"humidity\":82");
        } else {
            fail("ws69_decode_ok", "rc != RF_DECODE_OK");
        }
    }
    printf("%s WS69 done\n", TAG);

    /* ---- Frame 2: real WH51 soil-moisture frame (id 0f5c54) ---- */
    prev = rx_isr_count;
    if(!wait_irq(prev)) {
        fail("wh51_irq", "no GDO0 IRQ");
    } else {
        pass("wh51_irq");
        int rc = cc_drain_decode(json, sizeof json, raw, sizeof raw, &got);
        printf("%s wh51_rc=%d json=%s\n", TAG, rc, json);
        if(rc == RF_DECODE_OK) {
            pass("wh51_decode_ok");
            check_contains("wh51_model",    json, "\"model\":\"Fineoffset-WH51\"");
            check_contains("wh51_id",       json, "\"id\":\"0f5c54\"");
            check_contains("wh51_moisture", json, "\"moisture\":40");
        } else {
            fail("wh51_decode_ok", "rc != RF_DECODE_OK");
        }
    }
    printf("%s WH51 done\n", TAG);

    printf("%s === Tests Complete ===\n", TAG);
    printf("%s PASSED=%d FAILED=%d TOTAL=%d\n", TAG, pass_count, fail_count,
           pass_count + fail_count);

    while(1) {
        vTaskDelay(1000 / portTICK_PERIOD_MS);
    }
}
