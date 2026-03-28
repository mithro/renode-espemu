"""ESP32-C3 SYSTIMER stub.

Register map (base 0x60023000):
  0x00: CONF (bit 0 = CLK_EN)
  0x04: UNIT0_OP (bit 30 = latch trigger, bit 29 = VALUE_VALID)
  0x08: UNIT1_OP
  0x0C-0x2C: TARGET value registers
  0x34-0x3C: TARGET_CONF registers
  0x40: UNIT0_VALUE_HI
  0x44: UNIT0_VALUE_LO
  0x48: UNIT1_VALUE_HI
  0x4C: UNIT1_VALUE_LO
  0x50-0x58: COMP_LOAD registers
  0x64: INT_ENA (3 bits: TARGET0/1/2)
  0x68: INT_RAW (3 bits, set on alarm match)
  0x6C: INT_CLR (write-1-to-clear)
  0x70: INT_ST (read-only: RAW & ENA)
"""

if request.IsInit:
    systimer_counter = 0
    latched_value = 0
    int_ena = 0
    int_raw = 0
    systimer_regs = {}

if request.IsRead:
    if request.Offset == 0x04 or request.Offset == 0x08:
        # SYSTIMER_UNIT0_OP / SYSTIMER_UNIT1_OP: VALUE_VALID bit
        request.Value = 0x20000000
    elif request.Offset == 0x44 or request.Offset == 0x4C:
        # VALUE_LO
        request.Value = latched_value & 0xFFFFFFFF
    elif request.Offset == 0x40 or request.Offset == 0x48:
        # VALUE_HI
        request.Value = (latched_value >> 32) & 0xFFFFF
    elif request.Offset == 0x64:
        # INT_ENA
        request.Value = int_ena
    elif request.Offset == 0x68:
        # INT_RAW
        request.Value = int_raw
    elif request.Offset == 0x70:
        # INT_ST = RAW & ENA (masked status)
        request.Value = int_raw & int_ena
    else:
        request.Value = systimer_regs.get(request.Offset, 0x0)
else:
    # Write handlers
    if request.Offset == 0x04 or request.Offset == 0x08:
        # OP register: latch counter
        if request.Value & 0x40000000:
            systimer_counter += 5000
            latched_value = systimer_counter
    elif request.Offset == 0x64:
        # INT_ENA
        int_ena = request.Value & 0x7
    elif request.Offset == 0x68:
        # INT_RAW: external write sets bits (used by .resc to simulate alarm)
        int_raw |= request.Value & 0x7
    elif request.Offset == 0x6C:
        # INT_CLR: write-1-to-clear
        int_raw &= ~(request.Value & 0x7)
    else:
        systimer_regs[request.Offset] = request.Value
