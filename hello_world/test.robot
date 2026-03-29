*** Variables ***
${SETUP}                    ${CURDIR}/setup.resc
${UART}                     sysbus.uart0

*** Keywords ***
Setup ESP32C3
    Execute Script              ${SETUP}
    Create Terminal Tester      ${UART}

Boot And Kick
    # Boot through init
    Start Emulation
    Execute Command             sleep 1
    Execute Command             pause
    # Enable interrupts and fire single manual kick
    Execute Command             cpu MIE 0x800
    Execute Command             cpu MSTATUS 0x1808
    Execute Command             sysbus WriteDoubleWord 0x60023068 0x1
    Execute Command             sysbus.intmatrix OnGPIO 37 true
    Execute Command             sysbus.intmatrix OnGPIO 50 true
    Start Emulation

*** Test Cases ***
Should Print Hello World
    [Documentation]             Boot ESP32-C3 hello_world firmware and verify UART output
    [Tags]                      esp32c3  hello_world  boot
    Setup ESP32C3
    Boot And Kick
    Wait For Line On Uart       Hello world!  timeout=15

Should Report Correct Chip Revision
    [Documentation]             Verify chip revision matches hardware (v0.4)
    [Tags]                      esp32c3  efuse
    Setup ESP32C3
    Boot And Kick
    Wait For Line On Uart       silicon revision v0.4  timeout=15

Should Start Main Task
    [Documentation]             Verify FreeRTOS main_task starts and calls app_main
    [Tags]                      esp32c3  freertos
    Setup ESP32C3
    Boot And Kick
    Wait For Line On Uart       main_task: Calling app_main()  timeout=15
