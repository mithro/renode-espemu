"""ESP32-C3 Interrupt Matrix stub.

Tracks peripheral-to-CPU interrupt mappings and CPU interrupt enable state.
When the systimer alarm interrupt is mapped and enabled, periodically signals
the CPU by setting the corresponding mip bit.

Register layout (at DR_REG_INTERRUPT_CORE0_BASE = 0x600C2000):
  0x000-0x0FF: Mapping registers (one per peripheral source, 5 bits each)
  0x104: CPU_INT_ENABLE (bitmask of enabled CPU interrupt lines)
  0x108: CPU_INT_TYPE (0=level, 1=edge per line)
  0x10C: CPU_INT_CLEAR (write-1-to-clear edge interrupts)
  0x110: CPU_INT_EIP_STATUS (pending interrupts)
  0x114-0x190: CPU_INT_PRI_0 through CPU_INT_PRI_31 (priority per line)
  0x194: CPU_INT_THRESH (priority threshold)
"""

if request.IsInit:
    intmatrix_regs = {}
    int_enable = 0
    tick_counter = 0

if request.IsRead:
    if request.Offset == 0x104:
        # CPU_INT_ENABLE
        request.Value = int_enable
    elif request.Offset == 0x110:
        # CPU_INT_EIP_STATUS: report pending interrupts
        # For now, periodically signal that the systimer interrupt is pending
        tick_counter += 1
        systimer_cpu_int = intmatrix_regs.get(0x09C, 0) & 0x1F
        if systimer_cpu_int > 0 and (int_enable & (1 << systimer_cpu_int)):
            request.Value = 1 << systimer_cpu_int
        else:
            request.Value = 0
    else:
        request.Value = intmatrix_regs.get(request.Offset, 0)
else:
    # Write
    if request.Offset == 0x104:
        int_enable = request.Value
    elif request.Offset == 0x10C:
        # CPU_INT_CLEAR: clear edge interrupts (write-1-to-clear)
        pass
    else:
        intmatrix_regs[request.Offset] = request.Value
