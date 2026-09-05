*** Settings ***
Resource                    ${CURDIR}/../../tests/esp32c3_keywords.robot

*** Variables ***
${RADIO_CS}                ${BASE}/peripherals/sx1278/SX1278.cs
${RADIO_REPL}             ${BASE}/peripherals/sx1278/sx1278_tester.repl

*** Keywords ***
Setup ESP32C3 SX1278 Test
    # Base platform (includes ESP32C3_SPI2.cs, loads firmware), then attach the
    # SX1278 radio model and wire its DIO0 line to GPIO4.
    Setup ESP32C3 Peripheral Test    sx1278
    Execute Command         include @${RADIO_CS}
    Execute Command         machine LoadPlatformDescription @${RADIO_REPL}

*** Test Cases ***
Should Read RegVersion As 0x12
    [Documentation]         The firmware reads RegVersion (0x42) over SPI2 and
    ...                     the SX1278 model returns the silicon id 0x12, so
    ...                     identify() succeeds in emulation.
    [Tags]                  esp32c3  sx1278
    Setup ESP32C3 SX1278 Test
    Boot And Kick
    Wait For Line On Uart   [SX1278] === ESP32-C3 SX1278 Radio Test ===  timeout=20
    Wait For Line On Uart   [SX1278] TEST_PASS version got=0x12  timeout=15

Should Round Trip RegOpMode And Registers
    [Documentation]         RegOpMode mode + LoRa transitions read back per the
    ...                     datasheet (LongRangeMode only writable in SLEEP), and
    ...                     ordinary registers and burst access read back writes.
    [Tags]                  esp32c3  sx1278
    Setup ESP32C3 SX1278 Test
    Boot And Kick
    Wait For Line On Uart   [SX1278] TEST_PASS opmode_default  timeout=20
    Wait For Line On Uart   [SX1278] TEST_PASS opmode_sleep  timeout=10
    Wait For Line On Uart   [SX1278] TEST_PASS opmode_lora  timeout=10
    Wait For Line On Uart   [SX1278] TEST_PASS opmode_lora_locked  timeout=10
    Wait For Line On Uart   [SX1278] TEST_PASS dio_mapping  timeout=10
    Wait For Line On Uart   [SX1278] TEST_PASS burst  timeout=10

Should Report All SX1278 Tests Passed
    [Tags]                  esp32c3  sx1278
    Setup ESP32C3 SX1278 Test
    Boot And Kick
    Wait For Line On Uart   [SX1278] === Tests Complete ===  timeout=20
    Wait For Line On Uart   [SX1278] PASSED=9 FAILED=0 TOTAL=9  timeout=10
