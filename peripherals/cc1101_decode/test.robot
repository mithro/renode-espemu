*** Settings ***
Resource                    ${CURDIR}/../../tests/esp32c3_keywords.robot

*** Variables ***
${MEDIUM_CS}              ${BASE}/peripherals/air/Air433Medium.cs
${RADIO_CS}                ${BASE}/peripherals/cc1101/CC1101.cs
${RADIO_REPL}             ${BASE}/peripherals/cc1101_decode/cc1101_decode.repl
# REAL captured frames, padded to the 30-byte infinite-length drain window with
# trailing filler standing in for the demodulated noise that sits behind the
# frame in the RX FIFO (the real product drains a fixed CC_FSK_DRAIN_LEN=30 and
# the decoder ignores the tail). The leading bytes are the exact fixture hex:
#   WS69 id174 : firmware fixtures/ws69_id174_2026-08-20.json packet[0] (25 B)
#   WH51 id0f5c54: firmware fixtures/wh51_id0f5c54_2026-09-05.json packet[0] (14 B)
${WS69_FRAME}            24 AE 5D 82 13 52 05 01 07 24 00 00 00 00 00 5A A1 01 FF FF FF 01 6B 87 33 AA BB CC DD EE
${WH51_FRAME}            51 0F 5C 54 10 7F 28 F8 D0 FF FF FF 4B D7 DE AD BE EF CA FE 11 22 33 44 55 66 77 88 99 A0

*** Keywords ***
Setup ESP32C3 CC1101 Decode Test
    Setup ESP32C3 Peripheral Test    cc1101_decode
    Execute Command         include @${MEDIUM_CS}
    Execute Command         include @${RADIO_CS}
    Execute Command         machine LoadPlatformDescription @${RADIO_REPL}

*** Test Cases ***
Should Decode Real WS69 And WH51 Frames With The Product Decoder
    [Documentation]         The firmware compiles the SHIPPED fineoffset_decode()
    ...                     (decode_fineoffset.c + decode_common.c) and runs it in
    ...                     emulation. Two REAL captured frames injected on the air
    ...                     medium are drained from the CC1101 RX FIFO and decoded;
    ...                     the decoded model/id/fields are asserted. This exercises
    ...                     the real product decoder, not a reimplemented proxy.
    [Tags]                  esp32c3  cc1101  air  decode
    Setup ESP32C3 CC1101 Decode Test
    Boot And Kick
    Wait For Line On Uart   [CC1101DEC] === ESP32-C3 CC1101 Real-Decoder Test ===  timeout=20
    Wait For Line On Uart   [CC1101DEC] RX armed  timeout=15
    # --- WS69 weather frame ---
    Execute Command         air InjectFrame "${WS69_FRAME}"
    Wait For Line On Uart   [CC1101DEC] TEST_PASS ws69_irq  timeout=15
    Wait For Line On Uart   [CC1101DEC] TEST_PASS ws69_decode_ok  timeout=10
    Wait For Line On Uart   [CC1101DEC] TEST_PASS ws69_model  timeout=10
    Wait For Line On Uart   [CC1101DEC] TEST_PASS ws69_id  timeout=10
    Wait For Line On Uart   [CC1101DEC] TEST_PASS ws69_temp  timeout=10
    Wait For Line On Uart   [CC1101DEC] TEST_PASS ws69_hum  timeout=10
    Wait For Line On Uart   [CC1101DEC] WS69 done  timeout=10
    # --- WH51 soil-moisture frame ---
    Execute Command         air InjectFrame "${WH51_FRAME}"
    Wait For Line On Uart   [CC1101DEC] TEST_PASS wh51_irq  timeout=15
    Wait For Line On Uart   [CC1101DEC] TEST_PASS wh51_decode_ok  timeout=10
    Wait For Line On Uart   [CC1101DEC] TEST_PASS wh51_model  timeout=10
    Wait For Line On Uart   [CC1101DEC] TEST_PASS wh51_id  timeout=10
    Wait For Line On Uart   [CC1101DEC] TEST_PASS wh51_moisture  timeout=10

Should Report All CC1101 Decode Tests Passed
    [Tags]                  esp32c3  cc1101  air  decode
    Setup ESP32C3 CC1101 Decode Test
    Boot And Kick
    Wait For Line On Uart   [CC1101DEC] RX armed  timeout=20
    Execute Command         air InjectFrame "${WS69_FRAME}"
    Wait For Line On Uart   [CC1101DEC] WS69 done  timeout=15
    Execute Command         air InjectFrame "${WH51_FRAME}"
    Wait For Line On Uart   [CC1101DEC] === Tests Complete ===  timeout=15
    Wait For Line On Uart   [CC1101DEC] PASSED=11 FAILED=0 TOTAL=11  timeout=10
