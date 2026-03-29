*** Settings ***
Resource                    ${CURDIR}/../../tests/esp32c3_keywords.robot

*** Test Cases ***
Should Test GPIO Operations
    [Tags]                  esp32c3  gpio
    Setup ESP32C3 Peripheral Test    gpio
    Boot And Kick
    Wait For Line On Uart   [GPIO] === ESP32-C3 GPIO Register Test ===  timeout=15
    Wait For Line On Uart   [GPIO] === Tests Complete ===  timeout=10
