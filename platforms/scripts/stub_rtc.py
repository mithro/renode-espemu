if request.IsInit:
    rtc_time_counter = 0

if request.IsRead:
    if request.Offset == 0x38:
        # RTC_CNTL_RESET_STATE_REG: return POWERON_RESET (1)
        request.Value = 0x1
    elif request.Offset == 0x10:
        # RTC_CNTL_TIME_LOW0_REG: low 32 bits of RTC time (incrementing)
        rtc_time_counter += 1000
        request.Value = rtc_time_counter & 0xFFFFFFFF
    elif request.Offset == 0x14:
        # RTC_CNTL_TIME_HIGH0_REG: high 32 bits of RTC time
        request.Value = (rtc_time_counter >> 32) & 0xFFFF
    elif request.Offset == 0x4C:
        # RTC_CNTL_BROWN_OUT_REG: bit 31=brownout_det, bit 30=brownout_ena
        # Set bit 30 (enable) but NOT bit 31 (no brownout detected)
        # Also set bits for threshold etc to reasonable defaults
        request.Value = 0x40000000
    elif request.Offset == 0x7C:
        # RTC_CNTL_INT_ST_REG: no interrupts pending
        request.Value = 0x0
    elif request.Offset == 0x74:
        # RTC_CNTL_INT_RAW_REG: no raw interrupts
        request.Value = 0x0
    else:
        request.Value = 0x0
