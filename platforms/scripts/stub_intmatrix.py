"""ESP32-C3 Interrupt Matrix stub with logging."""

if request.IsInit:
    intmatrix_regs = {}
    int_enable = 0

if request.IsRead:
    if request.Offset == 0x104:
        request.Value = int_enable
    elif request.Offset == 0x110:
        # CPU_INT_EIP_STATUS: pending interrupts
        request.Value = intmatrix_regs.get(0x110, 0)
    else:
        request.Value = intmatrix_regs.get(request.Offset, 0)
else:
    if request.Offset == 0x104:
        int_enable = request.Value
        self.Log(LogLevel.Warning, "INTMATRIX: CPU_INT_ENABLE = 0x{0:08X}".format(request.Value))
    elif request.Offset == 0x09C:
        # SYSTIMER_TARGET2_INT_MAP - this is the FreeRTOS tick interrupt
        intmatrix_regs[request.Offset] = request.Value
        self.Log(LogLevel.Warning, "INTMATRIX: SYSTIMER_TARGET2 mapped to CPU int {0}".format(request.Value & 0x1F))
    elif request.Offset >= 0x114 and request.Offset <= 0x190:
        # CPU_INT_PRI_N registers
        intmatrix_regs[request.Offset] = request.Value
        pri_num = (request.Offset - 0x114) // 4
        self.Log(LogLevel.Warning, "INTMATRIX: CPU_INT_PRI_{0} = {1}".format(pri_num, request.Value))
    elif request.Offset == 0x194:
        intmatrix_regs[request.Offset] = request.Value
        self.Log(LogLevel.Warning, "INTMATRIX: CPU_INT_THRESH = {0}".format(request.Value))
    elif request.Offset >= 0x000 and request.Offset < 0x104:
        # Other mapping registers
        intmatrix_regs[request.Offset] = request.Value
        src = request.Offset // 4
        cpu_int = request.Value & 0x1F
        if cpu_int > 0:
            self.Log(LogLevel.Warning, "INTMATRIX: Source {0} -> CPU int {1}".format(src, cpu_int))
    else:
        intmatrix_regs[request.Offset] = request.Value
