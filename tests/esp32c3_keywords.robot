*** Variables ***
${BASE}                     /home/tim/github/mithro/renode-espemu
${ROM_ELF}                  /home/tim/esp/esp-rom-elfs/esp32c3_rev3_rom.elf
${SETUP}                    ${BASE}/tests/esp32c3_setup.resc
${CLIC_SETUP}               ${BASE}/tests/clic_setup.resc
${UART}                     sysbus.uart0

*** Keywords ***
Setup ESP32C3 Peripheral Test
    [Arguments]             ${peripheral}    ${app_name}=test_${peripheral}
    ${fw_elf}=              Set Variable    ${BASE}/peripherals/${peripheral}/firmware/build/${app_name}.elf
    ${fw_bin}=              Set Variable    ${BASE}/peripherals/${peripheral}/firmware/build/${app_name}.bin
    Execute Command         \$base = @${BASE}
    Execute Command         \$rom_elf = @${ROM_ELF}
    Execute Command         \$firmware_elf="${fw_elf}"
    Execute Command         \$firmware_bin="${fw_bin}"
    Execute Script          ${SETUP}
    Create Terminal Tester  ${UART}

Boot And Kick
    # Phase 1: Boot with CLIC present but unconfigured
    Start Emulation
    Execute Command         sleep 1
    Execute Command         pause
    # Phase 2: Configure CLIC interrupt delivery (from .resc file —
    # multi-line Python hooks don't work via Execute Command)
    Execute Script          ${CLIC_SETUP}
    # Phase 3: CLIC delivers systimer interrupts naturally — no manual kick needed
    Start Emulation
