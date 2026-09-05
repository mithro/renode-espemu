*** Settings ***
Resource                    ${CURDIR}/../../tests/esp32c3_keywords.robot

*** Variables ***
${TESTER_CS}               ${BASE}/peripherals/spi2/SPILoopbackTester.cs
${TESTER_REPL}             ${BASE}/peripherals/spi2/spi2_tester.repl

*** Keywords ***
Setup ESP32C3 SPI2 Test
    # Base platform (includes ESP32C3_SPI2.cs, loads firmware), then attach the
    # loopback tester slave and wire its IRQ line to GPIO4.
    Setup ESP32C3 Peripheral Test    spi2
    Execute Command         include @${TESTER_CS}
    Execute Command         machine LoadPlatformDescription @${TESTER_REPL}

*** Test Cases ***
Should Round Trip Bytes Through SPI2 Slave
    [Documentation]         Full-duplex spi_device_polling_transmit round-trips
    ...                     bytes through the ISPIPeripheral slave (byte ^ 0xA5).
    [Tags]                  esp32c3  spi2
    Setup ESP32C3 SPI2 Test
    Boot And Kick
    Wait For Line On Uart   [SPI2] === ESP32-C3 SPI2 Master Test ===  timeout=20
    Wait For Line On Uart   [SPI2] TEST_PASS rt0  timeout=15
    Wait For Line On Uart   [SPI2] TEST_PASS rt3  timeout=10
    Wait For Line On Uart   [SPI2] TEST_PASS rt2_2  timeout=10

Should Deliver Slave Driven Interrupt To CPU
    [Documentation]         The slave asserts its IRQ line on an SPI byte; the
    ...                     GPIO edge reaches the CPU through interrupt-matrix
    ...                     source 16 and runs the ESP-IDF GPIO ISR.
    [Tags]                  esp32c3  spi2  interrupt
    Setup ESP32C3 SPI2 Test
    Boot And Kick
    Wait For Line On Uart   [SPI2] TEST_PASS irq_received  timeout=20

Should Report All SPI2 Tests Passed
    [Tags]                  esp32c3  spi2
    Setup ESP32C3 SPI2 Test
    Boot And Kick
    Wait For Line On Uart   [SPI2] === Tests Complete ===  timeout=20
    Wait For Line On Uart   [SPI2] PASSED=8 FAILED=0 TOTAL=8  timeout=10
