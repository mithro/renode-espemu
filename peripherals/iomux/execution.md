# IO MUX Controller -- Execution Tracker

## Status
| Phase              | Status      | Date       |
|--------------------|-------------|------------|
| Planning           | complete    | 2026-03-29 |
| Test firmware      | complete    | 2026-03-29 |
| Hardware baseline  | not started |            |
| QEMU baseline      | not started |            |
| Renode impl        | complete    | 2026-03-29 |
| Robot test         | not started |            |
| Code review        | not started |            |
| Merged             | not started |            |

## Verification Levels
| Level | Check                        | Status      | Evidence |
|-------|------------------------------|-------------|----------|
| L1    | C# compiles                  | not started |          |
| L2    | Renode loads without crash    | not started |          |
| L3    | Boot progress >= previous     | not started |          |

## Planning Notes

IO MUX Controller at 0x60009000, size 0x1000. Manages:
- PIN_CTRL at 0x00: clock output selection (CLK_OUT1/2/3), pad power select/delay
- GPIO0-GPIO21 per-pin config at 0x04+n*4 (22 registers, offsets 0x04-0x58)
- DATE version register at 0xFC (default 0x2006050)

Per-pin register fields (16 bits used):
- [0] SLP_OE: output enable in sleep mode
- [1] SLP_SEL: use sleep config overrides
- [2] SLP_PD: pulldown in sleep mode
- [3] SLP_PU: pullup in sleep mode
- [4] SLP_IE: input enable in sleep mode
- [6:5] SLP_DRV: drive strength in sleep mode (0-3)
- [7] FUN_PD: pulldown enable
- [8] FUN_PU: pullup enable
- [9] FUN_IE: input enable
- [11:10] FUN_DRV: drive strength (0-3)
- [14:12] MCU_SEL: function select (0=default, 1=GPIO, 2+=peripheral)
- [15] FILTER_EN: input filter enable

Reset values:
- Most pins: 0x0000
- SPI flash pins (GPIO12-17): 0x0A00 (FUN_DRV=2, FUN_IE=1)
- GPIO8: 0x0100 (FUN_PU=1, boot strapping)
- GPIO20 (U0RXD): 0x0300 (FUN_IE=1, FUN_PU=1, UART RX)

Pin name mapping from io_mux_reg.h:
- GPIO0=XTAL_32K_P, GPIO1=XTAL_32K_N, GPIO2-3=GPIOx
- GPIO4=MTMS, GPIO5=MTDI, GPIO6=MTCK, GPIO7=MTDO (JTAG)
- GPIO8-10=GPIOx, GPIO11=VDD_SPI
- GPIO12=SPIHD, GPIO13=SPIWP, GPIO14=SPICS0, GPIO15=SPICLK, GPIO16=SPID, GPIO17=SPIQ
- GPIO18-19=GPIOx, GPIO20=U0RXD, GPIO21=U0TXD

## Implementation Log

C# peripheral replaces Python stub. Key features:
- PIN_CTRL register with CLK_OUT1/2/3 fields, PAD_POWER_SEL, PAD_POWER_SWITCH_DELAY
- 22 per-pin registers with all documented bit fields (SLP_*, FUN_*, MCU_SEL, FILTER_EN)
- Correct reset values for SPI flash pins, GPIO8, and U0RXD
- DATE register with hardware version 0x2006050
- Backed by uint[] array for pin state, with helper methods for bit/field access
