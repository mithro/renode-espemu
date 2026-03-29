// ESP32-C3 UART peripheral for Renode
//
// Implements the ESP32-C3 UART register interface on top of Renode's UARTBase,
// enabling use with Terminal Tester (Create Terminal Tester / Wait For Line On Uart)
// in Robot Framework tests.
//
// Base: DR_REG_UART_BASE = 0x60000000 (UART0), 0x60010000 (UART1)
// Size: 0x100
//
// Key registers:
//   0x00: UART_FIFO_REG (TX write / RX read)
//   0x04: UART_INT_RAW_REG
//   0x08: UART_INT_ST_REG
//   0x0C: UART_INT_ENA_REG
//   0x10: UART_INT_CLR_REG
//   0x1C: UART_STATUS_REG (TX/RX FIFO counts)
//   0x20: UART_CONF0_REG
//   0x24: UART_CONF1_REG
//   0x50: UART_AT_CMD_PRECNT_REG
//   0x54: UART_AT_CMD_POSTCNT_REG
//   0x58: UART_AT_CMD_GAPTOUT_REG
//   0x5C: UART_AT_CMD_CHAR_REG
//   0x78: UART_CLK_CONF_REG
//   0x7C: UART_DATE_REG

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.UART
{
    public class ESP32C3_UART : UARTBase, IDoubleWordPeripheral, IKnownSize,
        IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public ESP32C3_UART(IMachine machine) : base(machine)
        {
            intEna = 0;
            intRaw = 0;
            RegistersCollection = new DoubleWordRegisterCollection(this, CreateRegisterMap());
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public override void Reset()
        {
            base.Reset();
            intEna = 0;
            intRaw = 0;
            RegistersCollection.Reset();
        }

        public long Size => 0x100;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public override Parity ParityBit => Parity.None;
        public override Bits StopBits => Bits.One;
        public override uint BaudRate => 115200;

        protected override void CharWritten()
        {
            // Called by UARTBase when a character is received from external
            // (e.g., terminal input). Set RX FIFO not-empty status.
            intRaw |= IntRxFifoFull;
        }

        protected override void QueueEmptied()
        {
            // Called when RX queue is emptied
        }

        private Dictionary<long, DoubleWordRegister> CreateRegisterMap()
        {
            return new Dictionary<long, DoubleWordRegister>
            {
                // UART_FIFO_REG: 0x00
                // Write: TX byte → TransmitCharacter
                // Read: RX byte from UARTBase queue
                {(long)Registers.Fifo, new DoubleWordRegister(this)
                    .WithValueField(0, 8,
                        writeCallback: (_, value) =>
                        {
                            TransmitCharacter((byte)value);
                        },
                        valueProviderCallback: _ =>
                        {
                            if (!TryGetCharacter(out var character))
                            {
                                return 0;
                            }
                            return character;
                        })
                    .WithReservedBits(8, 24)
                },

                // UART_INT_RAW_REG: 0x04
                {(long)Registers.IntRaw, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read,
                        valueProviderCallback: _ => intRaw)
                },

                // UART_INT_ST_REG: 0x08 (masked: RAW & ENA)
                {(long)Registers.IntSt, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read,
                        valueProviderCallback: _ => intRaw & intEna)
                },

                // UART_INT_ENA_REG: 0x0C
                {(long)Registers.IntEna, new DoubleWordRegister(this)
                    .WithValueField(0, 32,
                        writeCallback: (_, value) => intEna = (uint)value,
                        valueProviderCallback: _ => intEna)
                },

                // UART_INT_CLR_REG: 0x10 (write-1-to-clear)
                {(long)Registers.IntClr, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write,
                        writeCallback: (_, value) => intRaw &= ~(uint)value)
                },

                // UART_CLKDIV_REG: 0x14
                {(long)Registers.ClkDiv, new DoubleWordRegister(this, 0x2B6)
                    .WithValueField(0, 32, name: "CLKDIV")
                },

                // UART_RX_FILT_REG: 0x18
                {(long)Registers.RxFilt, new DoubleWordRegister(this, 0x00000008)
                    .WithValueField(0, 32, name: "RX_FILT")
                },

                // UART_STATUS_REG: 0x1C
                // Bits [3:0] = RXFIFO_CNT (chars available)
                // Bits [19:16] = TXFIFO_CNT (0 = empty, ready to send)
                // Bit 13 = DSRN, Bit 14 = CTSN, Bit 15 = RXD
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithValueField(0, 10, FieldMode.Read, name: "RXFIFO_CNT",
                        valueProviderCallback: _ => (uint)Count)
                    .WithReservedBits(10, 3)
                    .WithFlag(13, FieldMode.Read, name: "DSRN",
                        valueProviderCallback: _ => false)
                    .WithFlag(14, FieldMode.Read, name: "CTSN",
                        valueProviderCallback: _ => false)
                    .WithFlag(15, FieldMode.Read, name: "RXD",
                        valueProviderCallback: _ => true)
                    .WithValueField(16, 10, FieldMode.Read, name: "TXFIFO_CNT",
                        valueProviderCallback: _ => 0u) // always empty (instant TX)
                    .WithReservedBits(26, 3)
                    .WithFlag(29, FieldMode.Read, name: "DTRN",
                        valueProviderCallback: _ => false)
                    .WithFlag(30, FieldMode.Read, name: "RTSN",
                        valueProviderCallback: _ => false)
                    .WithFlag(31, FieldMode.Read, name: "TXD",
                        valueProviderCallback: _ => true)
                },

                // UART_CONF0_REG: 0x20
                {(long)Registers.Conf0, new DoubleWordRegister(this, 0x1000001C)
                    .WithValueField(0, 32, name: "CONF0")
                },

                // UART_CONF1_REG: 0x24
                {(long)Registers.Conf1, new DoubleWordRegister(this, 0x0000C060)
                    .WithValueField(0, 32, name: "CONF1")
                },

                // UART_LOWPULSE_REG: 0x28 (RO, baud detection)
                {(long)Registers.LowPulse, new DoubleWordRegister(this, 0x00000FFF)
                    .WithValueField(0, 32, FieldMode.Read, name: "LOWPULSE")
                },

                // UART_HIGHPULSE_REG: 0x2C (RO, baud detection)
                {(long)Registers.HighPulse, new DoubleWordRegister(this, 0x00000FFF)
                    .WithValueField(0, 32, FieldMode.Read, name: "HIGHPULSE")
                },

                // UART_RXD_CNT_REG: 0x30 (RO, edge count)
                {(long)Registers.RxdCnt, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, name: "RXD_CNT")
                },

                // UART_FLOW_CONF_REG: 0x34
                {(long)Registers.FlowConf, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "FLOW_CONF")
                },

                // UART_SLEEP_CONF_REG: 0x38
                {(long)Registers.SleepConf, new DoubleWordRegister(this, 0x000000F0)
                    .WithValueField(0, 32, name: "SLEEP_CONF")
                },

                // UART_SWFC_CONF0_REG: 0x3C
                {(long)Registers.SwfcConf0, new DoubleWordRegister(this, 0x000026E0)
                    .WithValueField(0, 32, name: "SWFC_CONF0")
                },

                // UART_SWFC_CONF1_REG: 0x40
                {(long)Registers.SwfcConf1, new DoubleWordRegister(this, 0x00002200)
                    .WithValueField(0, 32, name: "SWFC_CONF1")
                },

                // UART_TXBRK_CONF_REG: 0x44
                {(long)Registers.TxbrkConf, new DoubleWordRegister(this, 0x0000000A)
                    .WithValueField(0, 32, name: "TXBRK_CONF")
                },

                // UART_IDLE_CONF_REG: 0x48
                {(long)Registers.IdleConf, new DoubleWordRegister(this, 0x00040100)
                    .WithValueField(0, 32, name: "IDLE_CONF")
                },

                // UART_RS485_CONF_REG: 0x4C
                {(long)Registers.Rs485Conf, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "RS485_CONF")
                },

                // UART_AT_CMD_PRECNT_REG: 0x50
                {(long)Registers.AtCmdPrecnt, new DoubleWordRegister(this, 0x00000901)
                    .WithValueField(0, 16, name: "PRE_IDLE_NUM")
                    .WithReservedBits(16, 16)
                },

                // UART_AT_CMD_POSTCNT_REG: 0x54
                {(long)Registers.AtCmdPostcnt, new DoubleWordRegister(this, 0x00000901)
                    .WithValueField(0, 16, name: "POST_IDLE_NUM")
                    .WithReservedBits(16, 16)
                },

                // UART_AT_CMD_GAPTOUT_REG: 0x58
                {(long)Registers.AtCmdGaptout, new DoubleWordRegister(this, 0x0000000B)
                    .WithValueField(0, 16, name: "RX_GAP_TOUT")
                    .WithReservedBits(16, 16)
                },

                // UART_AT_CMD_CHAR_REG: 0x5C
                {(long)Registers.AtCmdChar, new DoubleWordRegister(this, 0x0000032B)
                    .WithValueField(0, 8, name: "AT_CMD_CHAR")
                    .WithValueField(8, 8, name: "CHAR_NUM")
                    .WithReservedBits(16, 16)
                },

                // UART_MEM_CONF_REG: 0x60
                {(long)Registers.MemConf, new DoubleWordRegister(this, 0x000A0012)
                    .WithValueField(0, 32, name: "MEM_CONF")
                },

                // UART_MEM_TX_STATUS_REG: 0x64 (RO)
                {(long)Registers.MemTxStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, name: "MEM_TX_STATUS")
                },

                // UART_MEM_RX_STATUS_REG: 0x68 (RO)
                {(long)Registers.MemRxStatus, new DoubleWordRegister(this, 0x00080100)
                    .WithValueField(0, 32, FieldMode.Read, name: "MEM_RX_STATUS")
                },

                // UART_FSM_STATUS_REG: 0x6C (read-only FSM state)
                {(long)Registers.FsmStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, name: "FSM_STATUS",
                        valueProviderCallback: _ => 0u) // idle
                },

                // UART_POSPULSE_REG: 0x70 (RO, baud detection)
                {(long)Registers.PosPulse, new DoubleWordRegister(this, 0x00000FFF)
                    .WithValueField(0, 32, FieldMode.Read, name: "POSPULSE")
                },

                // UART_NEGPULSE_REG: 0x74 (RO, baud detection)
                {(long)Registers.NegPulse, new DoubleWordRegister(this, 0x00000FFF)
                    .WithValueField(0, 32, FieldMode.Read, name: "NEGPULSE")
                },

                // UART_CLK_CONF_REG: 0x78
                {(long)Registers.ClkConf, new DoubleWordRegister(this, 0x03701000)
                    .WithValueField(0, 32, name: "CLK_CONF")
                },

                // UART_DATE_REG: 0x7C
                {(long)Registers.Date, new DoubleWordRegister(this, 0x02008270)
                    .WithValueField(0, 32, name: "DATE")
                },

                // UART_ID_REG: 0x80
                {(long)Registers.Id, new DoubleWordRegister(this, 0x40000500)
                    .WithValueField(0, 32, name: "ID")
                },
            };
        }

        private uint intEna;
        private uint intRaw;

        private const uint IntRxFifoFull = 1u << 0;

        private enum Registers : long
        {
            Fifo = 0x00,
            IntRaw = 0x04,
            IntSt = 0x08,
            IntEna = 0x0C,
            IntClr = 0x10,
            ClkDiv = 0x14,
            RxFilt = 0x18,
            Status = 0x1C,
            Conf0 = 0x20,
            Conf1 = 0x24,
            LowPulse = 0x28,
            HighPulse = 0x2C,
            RxdCnt = 0x30,
            FlowConf = 0x34,
            SleepConf = 0x38,
            SwfcConf0 = 0x3C,
            SwfcConf1 = 0x40,
            TxbrkConf = 0x44,
            IdleConf = 0x48,
            Rs485Conf = 0x4C,
            AtCmdPrecnt = 0x50,
            AtCmdPostcnt = 0x54,
            AtCmdGaptout = 0x58,
            AtCmdChar = 0x5C,
            MemConf = 0x60,
            MemTxStatus = 0x64,
            MemRxStatus = 0x68,
            FsmStatus = 0x6C,
            PosPulse = 0x70,
            NegPulse = 0x74,
            ClkConf = 0x78,
            Date = 0x7C,
            Id = 0x80,
        }
    }
}
