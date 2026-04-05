*** Variables ***
${SETUP}                    ${CURDIR}/setup.resc
${CLIC_SETUP}               ${CURDIR}/../tests/clic_setup.resc
${UART}                     sysbus.uart0

*** Keywords ***
Setup ESP32C3
    Execute Script              ${SETUP}
    Create Terminal Tester      ${UART}

Boot And Kick
    # Phase 1: Boot with CLIC present but unconfigured
    Start Emulation
    Execute Command             sleep 1
    Execute Command             pause
    # Phase 2: Configure CLIC interrupt delivery (from .resc file —
    # multi-line Python hooks don't work via Execute Command)
    Execute Script              ${CLIC_SETUP}
    # Phase 3: CLIC delivers systimer interrupts naturally — no manual kick needed
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
