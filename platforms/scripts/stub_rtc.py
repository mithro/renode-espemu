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
    else:
        request.Value = 0x0
