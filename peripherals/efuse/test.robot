*** Settings ***
Resource                    ${CURDIR}/../../tests/esp32c3_keywords.robot

*** Test Cases ***
Should Read eFuse Registers
    [Documentation]         Boot eFuse test firmware and verify register reads
    [Tags]                  esp32c3  efuse
    Setup ESP32C3 Peripheral Test    efuse
    Boot And Kick
    Wait For Line On Uart   [EFUSE] === BLK0 Read Data ===  timeout=15
    Wait For Line On Uart   [EFUSE] === Decoded Fields ===  timeout=5

Should Report Correct MAC
    [Tags]                  esp32c3  efuse
    Setup ESP32C3 Peripheral Test    efuse
    Boot And Kick
    Wait For Line On Uart   [EFUSE] MAC_ADDR=  timeout=15
    Wait For Line On Uart   [EFUSE] TEST_PASS mac_nonzero  timeout=5

Should Report Correct Chip Revision
    [Tags]                  esp32c3  efuse
    Setup ESP32C3 Peripheral Test    efuse
    Boot And Kick
    Wait For Line On Uart   [EFUSE] CHIP_REVISION=v0.4  timeout=15
