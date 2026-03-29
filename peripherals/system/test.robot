*** Settings ***
Resource                    ${CURDIR}/../../tests/esp32c3_keywords.robot

*** Test Cases ***
Should Test System Registers
    [Tags]                  esp32c3  system
    Setup ESP32C3 Peripheral Test    system
    Boot And Kick
    Wait For Line On Uart   [SYSTEM] === Test Results ===  timeout=15
    Wait For Line On Uart   [SYSTEM] TEST_PASS  timeout=5
