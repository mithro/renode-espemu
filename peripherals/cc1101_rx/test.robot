*** Settings ***
Resource                    ${CURDIR}/../../tests/esp32c3_keywords.robot

*** Variables ***
${MEDIUM_CS}              ${BASE}/peripherals/air/Air433Medium.cs
${RADIO_CS}                ${BASE}/peripherals/cc1101/CC1101.cs
${RADIO_REPL}             ${BASE}/peripherals/cc1101_rx/cc1101_rx.repl
${FRAME}                  A7 11 22 33 44 55

*** Keywords ***
Setup ESP32C3 CC1101 RX Test
    # Base platform + firmware, then the shared air medium and the CC1101 radio
    # registered on it (GDO0/GDO2 -> GPIO4/GPIO5).
    Setup ESP32C3 Peripheral Test    cc1101_rx
    Execute Command         include @${MEDIUM_CS}
    Execute Command         include @${RADIO_CS}
    Execute Command         machine LoadPlatformDescription @${RADIO_REPL}

*** Test Cases ***
Should Deliver Injected Frame To CC1101 RX FIFO And Raise GDO0 IRQ
    [Documentation]         The firmware arms packet RX; a frame injected on the
    ...                     shared air medium loads the CC1101 RX FIFO, sets
    ...                     RXBYTES and asserts GDO0 (packet received) -> GPIO4
    ...                     IRQ. The firmware reads the frame back over SPI and
    ...                     verifies it byte-for-byte plus the interrupt count.
    [Tags]                  esp32c3  cc1101  air  rx
    Setup ESP32C3 CC1101 RX Test
    Boot And Kick
    Wait For Line On Uart   [CC1101RX] === ESP32-C3 CC1101 RF-RX Test ===  timeout=20
    Wait For Line On Uart   [CC1101RX] RX armed  timeout=15
    # A synthetic 433 MHz transmitter puts a known frame on the medium.
    Execute Command         air InjectFrame "${FRAME}"
    Wait For Line On Uart   [CC1101RX] TEST_PASS irq_fired  timeout=15
    Wait For Line On Uart   [CC1101RX] TEST_PASS rxbytes  timeout=10
    Wait For Line On Uart   [CC1101RX] TEST_PASS rx_frame  timeout=10

Should Report All CC1101 RX Tests Passed
    [Tags]                  esp32c3  cc1101  air  rx
    Setup ESP32C3 CC1101 RX Test
    Boot And Kick
    Wait For Line On Uart   [CC1101RX] RX armed  timeout=20
    Execute Command         air InjectFrame "${FRAME}"
    Wait For Line On Uart   [CC1101RX] === Tests Complete ===  timeout=15
    Wait For Line On Uart   [CC1101RX] PASSED=3 FAILED=0 TOTAL=3  timeout=10
