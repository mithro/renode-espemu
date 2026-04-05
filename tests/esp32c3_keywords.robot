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
    # Phase 1: Boot with CLIC present but unconfigured
    Start Emulation
    Execute Command         sleep 1
    Execute Command         pause
    # Phase 2: Configure CLIC interrupt delivery
    Execute Command         cpu MTVEC 0x40380003
    # CLIC dispatch hook
    Execute Command         set dispatch_clic\n"""\nfrom Antmicro.Renode.Peripherals.CPU import RegisterValue\nmcause = cpu.MCAUSE.RawValue\nif mcause & 0x80000000:\n    cpu.PC = RegisterValue.Create(0x403801DC, 32)\n"""
    Execute Command         cpu AddHook 0x40380000 $dispatch_clic
    # Enable all 32 CLIC interrupts: level-triggered, SHV=0, max priority
    FOR    ${i}    IN RANGE    32
        ${addr}=    Evaluate    hex(0x20001000 + ${i} * 4 + 1)
        Execute Command    sysbus WriteByte ${addr} 0x01
        ${addr}=    Evaluate    hex(0x20001000 + ${i} * 4 + 2)
        Execute Command    sysbus WriteByte ${addr} 0x00
        ${addr}=    Evaluate    hex(0x20001000 + ${i} * 4 + 3)
        Execute Command    sysbus WriteByte ${addr} 0xFF
    END
    Execute Command         cpu WfiAsNop true
    # CLIC delivers interrupts naturally — no manual kick needed
    Start Emulation
