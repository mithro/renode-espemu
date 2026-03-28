"""ESP32-C3 SYSTEM peripheral stub.

Key register: offset 0x28 = CPU_INTR_FROM_CPU_0_REG
Writing 1 triggers cross-core interrupt. Reading returns pending state.
We immediately clear it (return 0) so vPortYield stops spinning.
"""

if request.IsInit:
    system_regs = {}

if request.IsRead:
    if request.Offset == 0x28:
        # CPU_INTR_FROM_CPU_0_REG: always return 0 (interrupt already handled)
        request.Value = 0
    else:
        request.Value = system_regs.get(request.Offset, 0)
else:
    system_regs[request.Offset] = request.Value
