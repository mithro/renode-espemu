import sys

if request.IsInit:
    regs = {}

if request.IsRead:
    if request.Offset == 0x00:
        # UART_FIFO_REG: read from RX FIFO (empty)
        request.Value = 0x0
    elif request.Offset == 0x1C:
        # UART_STATUS_REG: TXFIFO_CNT=0 (TX empty, ready to send), RXFIFO_CNT=0
        request.Value = 0x0
    elif request.Offset == 0x04:
        # UART_INT_RAW_REG
        request.Value = regs.get(0x04, 0x0)
    elif request.Offset == 0x08:
        # UART_INT_ST_REG (masked interrupts)
        request.Value = 0x0
    elif request.Offset == 0x78:
        # UART_CLK_CONF_REG: return default value
        request.Value = regs.get(0x78, 0x0)
    elif request.Offset == 0x80:
        # UART_ID_REG
        request.Value = 0x0500
    else:
        request.Value = regs.get(request.Offset, 0x0)
else:
    # Write
    if request.Offset == 0x00:
        # UART_FIFO_REG: write to TX FIFO - output the character
        char = request.Value & 0xFF
        if char != 0:
            self.NoisyLog("UART TX: 0x{0:02X} '{1}'".format(char, chr(char) if 32 <= char < 127 else '?'))
    elif request.Offset == 0x10:
        # UART_INT_CLR_REG: clear interrupts (write-1-to-clear)
        raw = regs.get(0x04, 0x0)
        regs[0x04] = raw & ~request.Value
    else:
        regs[request.Offset] = request.Value
