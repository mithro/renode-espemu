*** Settings ***
Resource                    ${CURDIR}/../../tests/esp32c3_keywords.robot

*** Variables ***
${RADIO_CS}                ${BASE}/peripherals/cc1101/CC1101.cs
${RADIO_REPL}             ${BASE}/peripherals/cc1101/cc1101.repl

*** Keywords ***
Setup ESP32C3 CC1101 Test
    # Base platform (includes ESP32C3_SPI2.cs, loads firmware), then attach the
    # CC1101 radio model and wire its GDO0/GDO2 lines to GPIO4/GPIO5.
    Setup ESP32C3 Peripheral Test    cc1101
    Execute Command         include @${RADIO_CS}
    Execute Command         machine LoadPlatformDescription @${RADIO_REPL}

*** Test Cases ***
Should Read CC1101 Part Number And Version
    [Documentation]         PARTNUM (status 0x30) == 0x00 and VERSION (0x31) ==
    ...                     0x14 read back over SPI2.
    [Tags]                  esp32c3  cc1101
    Setup ESP32C3 CC1101 Test
    Boot And Kick
    Wait For Line On Uart   [CC1101] === ESP32-C3 CC1101 SPI Model Test ===  timeout=20
    Wait For Line On Uart   [CC1101] TEST_PASS partnum  timeout=15
    Wait For Line On Uart   [CC1101] TEST_PASS version  timeout=10

Should Write And Read Back Config Registers
    [Documentation]         Config-register writes (PKTLEN, IOCFG2) read back.
    [Tags]                  esp32c3  cc1101
    Setup ESP32C3 CC1101 Test
    Boot And Kick
    Wait For Line On Uart   [CC1101] TEST_PASS pktlen_rb  timeout=20
    Wait For Line On Uart   [CC1101] TEST_PASS iocfg2_rb  timeout=10

Should Transition MARCSTATE On Strobes
    [Documentation]         SRX -> MARCSTATE 0x0D (RX), SIDLE -> 0x01 (IDLE).
    [Tags]                  esp32c3  cc1101
    Setup ESP32C3 CC1101 Test
    Boot And Kick
    Wait For Line On Uart   [CC1101] TEST_PASS marc_rx  timeout=20
    Wait For Line On Uart   [CC1101] TEST_PASS marc_idle  timeout=10

Should Drive GDO Lines At Constant Levels
    [Documentation]         Constant-level GDO0/GDO2 (IOCFG 0x2F + INV) drive
    ...                     GPIO4/GPIO5 to distinct, independently swappable levels.
    [Tags]                  esp32c3  cc1101  gpio
    Setup ESP32C3 CC1101 Test
    Boot And Kick
    Wait For Line On Uart   [CC1101] TEST_PASS gdo0_high  timeout=20
    Wait For Line On Uart   [CC1101] TEST_PASS gdo2_low  timeout=10
    Wait For Line On Uart   [CC1101] TEST_PASS gdo0_low  timeout=10
    Wait For Line On Uart   [CC1101] TEST_PASS gdo2_high  timeout=10

Should Report All CC1101 Tests Passed
    [Tags]                  esp32c3  cc1101
    Setup ESP32C3 CC1101 Test
    Boot And Kick
    Wait For Line On Uart   [CC1101] === Tests Complete ===  timeout=20
    Wait For Line On Uart   [CC1101] PASSED=10 FAILED=0 TOTAL=10  timeout=10
