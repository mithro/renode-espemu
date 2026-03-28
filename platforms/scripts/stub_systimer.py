if request.IsInit:
    systimer_counter = 0
    latched_value = 0

if request.IsRead:
    if request.Offset == 0x04 or request.Offset == 0x08:
        # SYSTIMER_UNIT0_OP_REG / SYSTIMER_UNIT1_OP_REG
        # Bit 29 = TIMER_UNITn_VALUE_VALID (latch done)
        request.Value = 0x20000000
    elif request.Offset == 0x44 or request.Offset == 0x4C:
        # SYSTIMER_UNIT0/1_VALUE_LO (low 32 bits of latched counter)
        request.Value = latched_value & 0xFFFFFFFF
    elif request.Offset == 0x40 or request.Offset == 0x48:
        # SYSTIMER_UNIT0/1_VALUE_HI (high 20 bits of latched counter)
        request.Value = (latched_value >> 32) & 0xFFFFF
    else:
        request.Value = 0x0
else:
    # Write: check for latch trigger
    if request.Offset == 0x04 or request.Offset == 0x08:
        # Write to OP register with bit 30 set = trigger latch update
        if request.Value & 0x40000000:
            systimer_counter += 5000
            latched_value = systimer_counter
