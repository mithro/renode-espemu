if request.IsRead:
    if request.Offset == 0x38:
        # RTC_CNTL_RESET_STATE_REG: return POWERON_RESET (1)
        request.Value = 0x1
    else:
        request.Value = 0x0
