*** Settings ***
Resource                    ${CURDIR}/../../tests/esp32c3_keywords.robot

*** Test Cases ***
Should Test Timer Group Registers
    [Tags]                  esp32c3  timer-group
    Setup ESP32C3 Peripheral Test    timer-group    test_timer_group
    Boot And Kick
    Wait For Line On Uart   [TIMER] === ESP32-C3 Timer Group Test ===  timeout=15
    Wait For Line On Uart   [TIMER] TEST_PASS  timeout=10
