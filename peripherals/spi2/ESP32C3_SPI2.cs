// ESP32-C3 GP-SPI (SPI2 / FSPI) master controller for Renode
//
// Base: DR_REG_SPI2_BASE = 0x60024000, Size: 0x1000
//
// This models the ESP32-C3 general-purpose SPI2 peripheral as an SPI *master*
// that clocks bytes to a connected ISPIPeripheral slave. It implements enough
// of the register set for the ESP-IDF spi_master driver CPU (non-DMA) polling
// path -- spi_bus_initialize(SPI2_HOST) + spi_bus_add_device() +
// spi_device_polling_transmit() -- to perform full-duplex byte exchanges.
//
// A single slave attaches in the .repl as a child of this container, e.g.:
//     spi2: SPI.ESP32C3_SPI2 @ sysbus 0x60024000
//     myslave: SPI.SomeSlave @ spi2
//
// === Modeling basis ===
// There is NO captured ESP32-C3 hardware baseline for SPI2 in this repo.
// Register offsets, bit positions and reset values below are modeled from the
// ESP32-C3 TRM and the ESP-IDF v5.4.1 register/LL headers:
//   components/soc/esp32c3/register/soc/spi_reg.h
//   components/hal/esp32c3/include/hal/spi_ll.h
//
// === Driver interaction (ESP-IDF spi_ll polling path) ===
//   * spi_ll_apply_config(): sets CMD.SPI_UPDATE(bit23), spins until it reads 0.
//   * spi_ll_user_start():   sets CMD.SPI_USR(bit24) to start the transfer.
//   * spi_ll_usr_is_done():  reads DMA_INT_RAW.TRANS_DONE(bit12).
//   * spi_ll_clear_int_stat(): writes DMA_INT_RAW to clear TRANS_DONE.
// We complete each transfer synchronously: on the SPI_USR write we clock all
// bytes to the slave, then set DMA_INT_RAW.TRANS_DONE and self-clear SPI_USR /
// SPI_UPDATE so the driver polling loops fall through immediately.
//
// === What is modeled ===
//   * CMD / USER / USER1 / USER2 / MS_DLEN registers (transfer descriptor).
//   * W0..W15 CPU data buffer (0x98..0xD4), full-duplex read/write.
//   * DMA_INT_RAW/ENA/CLR TRANS_DONE bit for the polling handshake.
//   * Command + address phases (byte-multiple bit lengths) followed by the
//     MOSI/MISO data phase; each byte is passed to slave.Transmit().
//
// === What is NOT modeled (deferred) ===
//   * Interrupt-driven transfers (SPI2_INTR / intmatrix source 19). Only the
//     polling path is wired. The DMA_INT_ENA bit is stored so an IRQ line can
//     be added later without register changes.
//   * GDMA-backed transfers (SPI_DMA_CONF). Use SPI_DMA_DISABLED in firmware.
//   * Clock divider timing, CS setup/hold timing, dual/quad line modes.
//   * Non-byte-multiple command/address/data bit lengths (rounded up to bytes).

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class ESP32C3_SPI2 : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        public ESP32C3_SPI2(IMachine machine) : base(machine)
        {
            dataBuf = new uint[DataBufWords];
        }

        public override void Reset()
        {
            cmd = 0;
            addr = 0;
            ctrl = 0;
            clock = 0;
            user = 0;
            user1 = 0;
            user2 = 0;
            msDlen = 0;
            misc = 0;
            dmaConf = 0;
            dmaIntEna = 0;
            dmaIntRaw = 0;
            slaveReg = 0;
            clkGate = 0;
            Array.Clear(dataBuf, 0, dataBuf.Length);
        }

        public long Size => 0x1000;

        public uint ReadDoubleWord(long offset)
        {
            if(offset >= W0 && offset <= W15)
            {
                return dataBuf[(offset - W0) / 4];
            }
            switch((Registers)offset)
            {
                case Registers.Cmd:       return cmd;
                case Registers.Addr:      return addr;
                case Registers.Ctrl:      return ctrl;
                case Registers.Clock:     return clock;
                case Registers.User:      return user;
                case Registers.User1:     return user1;
                case Registers.User2:     return user2;
                case Registers.MsDlen:    return msDlen;
                case Registers.Misc:      return misc;
                case Registers.DmaConf:   return dmaConf;
                case Registers.DmaIntEna: return dmaIntEna;
                case Registers.DmaIntClr: return 0;
                case Registers.DmaIntRaw: return dmaIntRaw;
                case Registers.DmaIntSt:  return dmaIntRaw & dmaIntEna;
                case Registers.Slave:     return slaveReg;
                case Registers.ClkGate:   return clkGate;
                case Registers.Date:      return DateValue;
                default:
                    this.Log(LogLevel.Debug, "Unhandled read at offset 0x{0:X3}", offset);
                    return 0;
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset >= W0 && offset <= W15)
            {
                dataBuf[(offset - W0) / 4] = value;
                return;
            }
            switch((Registers)offset)
            {
                case Registers.Cmd:
                    // SPI_UPDATE (config latch) and SPI_USR (start) are both
                    // self-clearing; never store them.
                    if((value & SpiUsr) != 0)
                    {
                        PerformTransfer();
                        dmaIntRaw |= TransDone; // signal completion to poller
                    }
                    cmd = value & ~(SpiUsr | SpiUpdate);
                    break;
                case Registers.Addr:      addr = value; break;
                case Registers.Ctrl:      ctrl = value; break;
                case Registers.Clock:     clock = value; break;
                case Registers.User:      user = value; break;
                case Registers.User1:     user1 = value; break;
                case Registers.User2:     user2 = value; break;
                case Registers.MsDlen:    msDlen = value; break;
                case Registers.Misc:      misc = value; break;
                case Registers.DmaConf:   dmaConf = value; break;
                case Registers.DmaIntEna: dmaIntEna = value; break;
                case Registers.DmaIntClr: dmaIntRaw &= ~value; break;      // W1C
                case Registers.DmaIntRaw: dmaIntRaw = value; break;        // driver clears here
                case Registers.Slave:     slaveReg = value; break;
                case Registers.ClkGate:   clkGate = value; break;
                default:
                    this.Log(LogLevel.Debug, "Unhandled write 0x{0:X8} at offset 0x{1:X3}", value, offset);
                    break;
            }
        }

        private void PerformTransfer()
        {
            var slave = RegisteredPeripheral;
            if(slave == null)
            {
                this.Log(LogLevel.Warning, "SPI_USR set but no slave attached; transfer ignored.");
                return;
            }

            // Command phase (USER.USR_COMMAND). Command value in USER2[15:0],
            // bit length-1 in USER2[31:28]. Byte-multiple lengths only.
            if((user & UsrCommand) != 0)
            {
                int cmdBits = (int)((user2 >> 28) & 0xF) + 1;
                int cmdBytes = (cmdBits + 7) / 8;
                uint cmdVal = user2 & 0xFFFF;
                for(int i = 0; i < cmdBytes; i++)
                {
                    slave.Transmit((byte)(cmdVal >> (8 * i)));
                }
            }

            // Address phase (USER.USR_ADDR). Address in ADDR reg, bit length-1
            // in USER1[31:27]. Sent MSB-first for byte-multiple lengths.
            if((user & UsrAddr) != 0)
            {
                int addrBits = (int)((user1 >> 27) & 0x1F) + 1;
                int addrBytes = (addrBits + 7) / 8;
                for(int i = 0; i < addrBytes; i++)
                {
                    slave.Transmit((byte)(addr >> (8 * (addrBytes - 1 - i))));
                }
            }

            // Data phase (USER.USR_MOSI / USER.USR_MISO). Length-1 in MS_DLEN.
            bool mosi = (user & UsrMosi) != 0;
            bool miso = (user & UsrMiso) != 0;
            if(mosi || miso)
            {
                int dataBits = (int)(msDlen & 0x3FFFF) + 1;
                int dataBytes = (dataBits + 7) / 8;
                for(int i = 0; i < dataBytes; i++)
                {
                    byte outByte = mosi ? GetBufByte(i) : (byte)0;
                    byte inByte = slave.Transmit(outByte);
                    if(miso)
                    {
                        SetBufByte(i, inByte);
                    }
                }
            }

            slave.FinishTransmission();
        }

        private byte GetBufByte(int index)
        {
            return (byte)(dataBuf[index / 4] >> (8 * (index % 4)));
        }

        private void SetBufByte(int index, byte value)
        {
            int word = index / 4;
            int shift = 8 * (index % 4);
            dataBuf[word] = (dataBuf[word] & ~(0xFFu << shift)) | ((uint)value << shift);
        }

        private uint cmd, addr, ctrl, clock, user, user1, user2, msDlen, misc;
        private uint dmaConf, dmaIntEna, dmaIntRaw, slaveReg, clkGate;
        private readonly uint[] dataBuf;

        private const int DataBufWords = 16;
        private const long W0 = 0x98;
        private const long W15 = 0xD4;

        private const uint SpiUpdate  = 1u << 23; // CMD
        private const uint SpiUsr     = 1u << 24; // CMD
        private const uint UsrMosi    = 1u << 27; // USER
        private const uint UsrMiso    = 1u << 28; // USER
        private const uint UsrDummy   = 1u << 29; // USER (unused; dummy cycles have no byte effect)
        private const uint UsrAddr    = 1u << 30; // USER
        private const uint UsrCommand = 1u << 31; // USER
        private const uint TransDone  = 1u << 12; // DMA_INT_*

        private const uint DateValue = 0x02007220; // TRM reset default (unverified vs silicon)

        private enum Registers : long
        {
            Cmd       = 0x00,
            Addr      = 0x04,
            Ctrl      = 0x08,
            Clock     = 0x0C,
            User      = 0x10,
            User1     = 0x14,
            User2     = 0x18,
            MsDlen    = 0x1C,
            Misc      = 0x20,
            DmaConf   = 0x30,
            DmaIntEna = 0x34,
            DmaIntClr = 0x38,
            DmaIntRaw = 0x3C,
            DmaIntSt  = 0x40,
            Slave     = 0xE0,
            ClkGate   = 0xE8,
            Date      = 0xF0,
        }
    }
}
