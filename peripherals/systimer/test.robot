*** Settings ***
Resource                    ${CURDIR}/../../tests/esp32c3_keywords.robot

*** Test Cases ***
Should Test SysTimer Registers
    [Tags]                  esp32c3  systimer
    Setup ESP32C3 Peripheral Test    systimer
    Boot And Kick
    Wait For Line On Uart   [SYSTIMER] === ESP32-C3 SYSTIMER Read Test ===  timeout=15
    Wait For Line On Uart   [SYSTIMER] === Phase 1: Reset register dump ===  timeout=5
