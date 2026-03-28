if request.IsRead:
    if request.Offset == 0x68:
        # TIMG_RTCCALICFG_REG: set bit 15 (RTC_CALI_RDY) = calibration done
        request.Value = 0x00008000
    elif request.Offset == 0x6C:
        # TIMG_RTCCALICFG1_REG: calibration value (return a reasonable count)
        request.Value = 0x01000000
    else:
        request.Value = 0x0
