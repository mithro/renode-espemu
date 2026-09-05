*** Settings ***
Resource                    ${CURDIR}/../../tests/esp32c3_keywords.robot

*** Variables ***
${MEDIUM_CS}              ${BASE}/peripherals/air/Air433Medium.cs
${RADIO_CS}                ${BASE}/peripherals/sx1278/SX1278.cs
${RADIO_REPL}             ${BASE}/peripherals/sx1278_rx/sx1278_rx.repl
${FRAME}                  B4 C3 D2 E1 F0

*** Keywords ***
Setup ESP32C3 SX1278 RX Test
    # Base platform + firmware, then the shared air medium and the SX1278 radio
    # registered on it (DIO0 -> GPIO4).
    Setup ESP32C3 Peripheral Test    sx1278_rx
    Execute Command         include @${MEDIUM_CS}
    Execute Command         include @${RADIO_CS}
    Execute Command         machine LoadPlatformDescription @${RADIO_REPL}

*** Test Cases ***
Should Deliver Injected Frame To SX1278 FIFO And Raise DIO0 IRQ
    [Documentation]         The firmware enters FSK RX with DIO0 = PayloadReady;
    ...                     a frame injected on the shared air medium loads the
    ...                     SX1278 RX FIFO and asserts DIO0 -> GPIO4 IRQ. The
    ...                     firmware reads the payload back over SPI (RegFifo) and
    ...                     verifies it byte-for-byte plus the interrupt count.
    [Tags]                  esp32c3  sx1278  air  rx
    Setup ESP32C3 SX1278 RX Test
    Boot And Kick
    Wait For Line On Uart   [SX1278RX] === ESP32-C3 SX1278 FSK RF-RX Test ===  timeout=20
    Wait For Line On Uart   [SX1278RX] RX armed  timeout=15
    # A synthetic 433 MHz transmitter puts a known frame on the medium.
    Execute Command         air InjectFrame "${FRAME}"
    Wait For Line On Uart   [SX1278RX] TEST_PASS irq_fired  timeout=15
    Wait For Line On Uart   [SX1278RX] TEST_PASS rx_frame  timeout=10

Should Report All SX1278 RX Tests Passed
    [Tags]                  esp32c3  sx1278  air  rx
    Setup ESP32C3 SX1278 RX Test
    Boot And Kick
    Wait For Line On Uart   [SX1278RX] RX armed  timeout=20
    Execute Command         air InjectFrame "${FRAME}"
    Wait For Line On Uart   [SX1278RX] === Tests Complete ===  timeout=15
    Wait For Line On Uart   [SX1278RX] PASSED=3 FAILED=0 TOTAL=3  timeout=10
