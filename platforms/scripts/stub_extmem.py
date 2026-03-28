if request.IsInit:
    extmem_regs = {}

if request.IsRead:
    if request.Offset == 0x00:
        # EXTMEM_ICACHE_CTRL_REG: bit 0 = ICACHE_ENABLE
        request.Value = extmem_regs.get(0x00, 0x1)
    elif request.Offset == 0x28:
        # EXTMEM_ICACHE_SYNC_CTRL_REG: bit 1 = sync done
        request.Value = 0x2
    elif request.Offset == 0x34:
        # EXTMEM_ICACHE_PRELOAD_CTRL_REG: bit 1 = preload done
        request.Value = 0x2
    else:
        request.Value = extmem_regs.get(request.Offset, 0x0)
else:
    extmem_regs[request.Offset] = request.Value
