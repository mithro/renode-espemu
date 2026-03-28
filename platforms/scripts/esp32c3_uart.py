import sys

if request.IsInit:
    regs = {}
    uart_buf = ""

if request.IsRead:
    if request.Offset == 0x00:
        request.Value = 0x0
    elif request.Offset == 0x1C:
        # UART_STATUS_REG: TXFIFO_CNT=0 (empty, ready to send)
        request.Value = 0x0
    elif request.Offset == 0x04:
        request.Value = regs.get(0x04, 0x0)
    elif request.Offset == 0x08:
        request.Value = 0x0
    elif request.Offset == 0x78:
        request.Value = regs.get(0x78, 0x0)
    elif request.Offset == 0x80:
        request.Value = 0x0500
    else:
        request.Value = regs.get(request.Offset, 0x0)
else:
    if request.Offset == 0x00:
        # UART_FIFO_REG: write to TX FIFO - output the character
        char = request.Value & 0xFF
        if char == 0x0A or char == 0x0D:
            if uart_buf:
                self.Log(LogLevel.Warning, "UART: " + uart_buf)
                uart_buf = ""
        elif char >= 0x20 and char < 0x7F:
            uart_buf += chr(char)
        else:
            uart_buf += "."
    elif request.Offset == 0x10:
        raw = regs.get(0x04, 0x0)
        regs[0x04] = raw & ~request.Value
    else:
        regs[request.Offset] = request.Value
