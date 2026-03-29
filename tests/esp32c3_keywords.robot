*** Variables ***
${BASE}                     /home/tim/github/mithro/renode-espemu
${SETUP}                    ${BASE}/tests/esp32c3_setup.resc
${UART}                     sysbus.uart0

*** Keywords ***
Setup ESP32C3 Peripheral Test
    [Arguments]             ${peripheral}    ${app_name}=test_${peripheral}
    ${fw_elf}=              Set Variable    ${BASE}/peripherals/${peripheral}/firmware/build/${app_name}.elf
    ${fw_bin}=              Set Variable    ${BASE}/peripherals/${peripheral}/firmware/build/${app_name}.bin
    Execute Command         \$firmware_elf="${fw_elf}"
    Execute Command         \$firmware_bin="${fw_bin}"
    Execute Script          ${SETUP}
    Create Terminal Tester  ${UART}

Boot And Kick
    Start Emulation
    Execute Command         sleep 1
    Execute Command         pause
    Execute Command         cpu MIE 0x800
    Execute Command         cpu MSTATUS 0x1808
    Execute Command         sysbus WriteDoubleWord 0x60023068 0x1
    Execute Command         sysbus.intmatrix OnGPIO 37 true
    Execute Command         sysbus.intmatrix OnGPIO 50 true
    Start Emulation
