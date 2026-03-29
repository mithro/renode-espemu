*** Settings ***
Resource                    ${CURDIR}/../../tests/esp32c3_keywords.robot

*** Test Cases ***
Should Test Interrupt Matrix
    [Tags]                  esp32c3  interrupt-matrix
    Setup ESP32C3 Peripheral Test    interrupt-matrix    test_interrupt_matrix
    Boot And Kick
    Wait For Line On Uart   [INTMATRIX] === ESP32-C3 Interrupt Matrix Test ===  timeout=15
    Wait For Line On Uart   [INTMATRIX] TEST_PASS sources_mapped  timeout=10
    Wait For Line On Uart   [INTMATRIX] TEST_PASS interrupts_enabled  timeout=5
