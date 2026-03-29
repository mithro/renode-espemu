*** Settings ***
Resource                    ${CURDIR}/../../tests/esp32c3_keywords.robot

*** Test Cases ***
Should Read Random Values
    [Documentation]         Boot RNG test firmware and verify random reads
    [Tags]                  esp32c3  rng
    Setup ESP32C3 Peripheral Test    rng
    Boot And Kick
    Wait For Line On Uart   [RNG] === Reading 10 values ===  timeout=15
    Wait For Line On Uart   [RNG] TEST_PASS values_nonzero  timeout=10
    Wait For Line On Uart   [RNG] TEST_PASS values_distinct  timeout=5
