// CC1101 -- a register-accurate SPI model of the TI CC1101 sub-1GHz radio for
// the ESP32-C3 emulation (Renode). It implements the SPI framing, register map,
// command-strobe state machine and GDO GPIO output lines that the ESP32-C3
// firmware's CC1101 driver exercises. It does NOT model the RF data path or
// timing -- that is a later "433 MHz air medium" wave.
//
// Attach it in a .repl as the single child of the SPI2 master and wire its GDO
// lines to GPIO input pins:
//
//     radio: SPI.CC1101 @ spi2
//         0 -> gpio@4          // GDO0 -> GPIO pin 4
//         2 -> gpio@5          // GDO2 -> GPIO pin 5
//
// The class lives in namespace Antmicro.Renode.Peripherals.SPI so the .repl
// resolver (which only searches Antmicro.Renode.Peripherals.*) finds it as
// SPI.CC1101. It mirrors the interface of peripherals/spi2/SPILoopbackTester.cs
// (ISPIPeripheral + INumberedGPIOOutput), which in turn follows Renode's own
// Wireless.CC2520.
//
// SPI framing (CC1101 datasheet SWRS061, section 10):
//   * The first byte of every transaction is a header:
//       bit7  = R/W   (1 = read, 0 = write)
//       bit6  = BURST (auto-increment / FIFO burst)
//       bit5:0 = address
//   * The header byte always returns the chip STATUS byte on MISO:
//       bit7   = CHIP_RDYn (0 = ready)   -- always 0 here
//       bit6:4 = STATE (IDLE/RX/TX/...)
//       bit3:0 = FIFO_BYTES_AVAILABLE (RX bytes on read, TX free on write)
//   * Config registers 0x00-0x2E: single or burst (auto-increment) R/W.
//   * Addresses 0x30-0x3D: BURST bit disambiguates --
//       BURST = 0 -> command strobe (single header byte)
//       BURST = 1 -> status register read (read-only)
//   * PATABLE 0x3E: 8-byte R/W, burst auto-increments (wraps at 8).
//   * FIFO 0x3F: write = TX FIFO, read = RX FIFO (burst = multi-byte).

using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class CC1101 : ISPIPeripheral, INumberedGPIOOutput
    {
        public CC1101()
        {
            GDO0 = new GPIO();
            GDO1 = new GPIO();
            GDO2 = new GPIO();
            Connections = new Dictionary<int, IGPIO>
            {
                { 0, GDO0 },
                { 1, GDO1 },
                { 2, GDO2 },
            };
            configRegs = new byte[ConfigRegCount];
            patable = new byte[PatableSize];
            txFifo = new Queue<byte>();
            rxFifo = new Queue<byte>();
            Reset();
        }

        public void Reset()
        {
            // Power-on / SRES register defaults (datasheet section 29).
            System.Array.Copy(DefaultConfig, configRegs, ConfigRegCount);
            System.Array.Clear(patable, 0, PatableSize);
            patable[0] = 0xC6; // typical PATABLE[0] reset value
            txFifo.Clear();
            rxFifo.Clear();
            byteIndex = 0;
            marcState = MarcStateIdle;
            chipState = StateIdle;
            UpdateGdoLines();
        }

        // Called once per SPI byte, in transfer order. Byte 0 is the header;
        // subsequent bytes are data. Return value is the MISO byte the ESP reads.
        public byte Transmit(byte data)
        {
            byte miso;
            if(byteIndex == 0)
            {
                miso = HandleHeader(data);
            }
            else
            {
                miso = HandleData(data);
            }
            byteIndex++;
            return miso;
        }

        // Chip-select deassert: reset per-transaction byte position.
        public void FinishTransmission()
        {
            byteIndex = 0;
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public GPIO GDO0 { get; }
        public GPIO GDO1 { get; }
        public GPIO GDO2 { get; }

        private byte HandleHeader(byte header)
        {
            headerRead = (header & 0x80) != 0;
            headerBurst = (header & 0x40) != 0;
            headerAddr = (byte)(header & 0x3F);

            // Addresses 0x30-0x3D without the burst bit are command strobes.
            if(headerAddr >= 0x30 && headerAddr <= 0x3D && !headerBurst)
            {
                ExecuteStrobe(headerAddr);
                curAccess = Access.Strobe;
            }
            else if(headerAddr >= 0x30 && headerAddr <= 0x3D && headerBurst)
            {
                // Status register read (requires burst bit set + read bit).
                curAccess = Access.StatusRead;
                curAddr = headerAddr;
            }
            else if(headerAddr == AddrPatable)
            {
                curAccess = headerRead ? Access.PatableRead : Access.PatableWrite;
                curAddr = 0;
            }
            else if(headerAddr == AddrFifo)
            {
                curAccess = headerRead ? Access.FifoRead : Access.FifoWrite;
                curAddr = 0;
            }
            else // 0x00-0x2E config registers
            {
                curAccess = headerRead ? Access.ConfigRead : Access.ConfigWrite;
                curAddr = headerAddr;
            }

            return BuildStatusByte();
        }

        private byte HandleData(byte data)
        {
            switch(curAccess)
            {
                case Access.ConfigRead:
                {
                    byte val = ReadConfig(curAddr);
                    if(headerBurst && curAddr < ConfigRegCount - 1)
                    {
                        curAddr++;
                    }
                    return val;
                }
                case Access.ConfigWrite:
                {
                    WriteConfig(curAddr, data);
                    if(headerBurst && curAddr < ConfigRegCount - 1)
                    {
                        curAddr++;
                    }
                    return BuildStatusByte();
                }
                case Access.StatusRead:
                {
                    byte val = ReadStatus(curAddr);
                    if(headerBurst && curAddr < 0x3D)
                    {
                        curAddr++;
                    }
                    return val;
                }
                case Access.PatableRead:
                {
                    byte val = patable[curAddr % PatableSize];
                    curAddr++;
                    return val;
                }
                case Access.PatableWrite:
                {
                    patable[curAddr % PatableSize] = data;
                    curAddr++;
                    return BuildStatusByte();
                }
                case Access.FifoRead:
                {
                    byte val = rxFifo.Count > 0 ? rxFifo.Dequeue() : (byte)0x00;
                    return val;
                }
                case Access.FifoWrite:
                {
                    if(txFifo.Count < FifoSize)
                    {
                        txFifo.Enqueue(data);
                    }
                    return BuildStatusByte();
                }
                case Access.Strobe:
                default:
                    // Extra clocked bytes after a strobe: return status.
                    return BuildStatusByte();
            }
        }

        private byte ReadConfig(byte addr)
        {
            if(addr < ConfigRegCount)
            {
                return configRegs[addr];
            }
            return 0x00;
        }

        private void WriteConfig(byte addr, byte value)
        {
            if(addr >= ConfigRegCount)
            {
                return;
            }
            configRegs[addr] = value;
            if(addr == RegIOCFG0 || addr == RegIOCFG2 || addr == RegIOCFG1)
            {
                UpdateGdoLines();
            }
        }

        private byte ReadStatus(byte addr)
        {
            switch(addr)
            {
                case 0x30: return PartNum;      // PARTNUM
                case 0x31: return Version;      // VERSION
                case 0x32: return 0x00;         // FREQEST
                case 0x33: return 0x00;         // LQI (CRC_OK=0, worst LQI)
                case 0x34: return RssiValue;    // RSSI (2's complement, plausible)
                case 0x35: return marcState;    // MARCSTATE
                case 0x36: return 0x00;         // WORTIME1
                case 0x37: return 0x00;         // WORTIME0
                case 0x38: return 0x00;         // PKTSTATUS
                case 0x39: return 0x00;         // VCO_VC_DAC
                case 0x3A: return TxBytes;      // TXBYTES
                case 0x3B: return RxBytes;      // RXBYTES
                case 0x3C: return 0x00;         // RCCTRL1_STATUS
                case 0x3D: return 0x00;         // RCCTRL0_STATUS
                default:   return 0x00;
            }
        }

        private byte TxBytes
        {
            get
            {
                // bit7 = TXFIFO_UNDERFLOW (0 here), bits6:0 = byte count.
                return (byte)(txFifo.Count & 0x7F);
            }
        }

        private byte RxBytes
        {
            get
            {
                // bit7 = RXFIFO_OVERFLOW (0 here), bits6:0 = byte count.
                return (byte)(rxFifo.Count & 0x7F);
            }
        }

        private void ExecuteStrobe(byte addr)
        {
            switch(addr)
            {
                case StrobeSRES:
                    Reset();
                    break;
                case StrobeSFSTXON:
                    chipState = StateFstxon;
                    marcState = MarcStateFstxon;
                    break;
                case StrobeSXOFF:
                    chipState = StateIdle;
                    marcState = MarcStateIdle;
                    break;
                case StrobeSCAL:
                    // Calibrate then return to IDLE (no timing modeled).
                    chipState = StateIdle;
                    marcState = MarcStateIdle;
                    break;
                case StrobeSRX:
                    chipState = StateRx;
                    marcState = MarcStateRx;
                    break;
                case StrobeSTX:
                    chipState = StateTx;
                    marcState = MarcStateTx;
                    break;
                case StrobeSIDLE:
                    chipState = StateIdle;
                    marcState = MarcStateIdle;
                    break;
                case StrobeSPWD:
                    chipState = StateIdle;
                    marcState = MarcStateSleep;
                    break;
                case StrobeSFRX:
                    rxFifo.Clear();
                    break;
                case StrobeSFTX:
                    txFifo.Clear();
                    break;
                case StrobeSWORRST:
                case StrobeSWOR:
                case StrobeSNOP:
                    break;
                default:
                    this.Log(LogLevel.Warning, "Unknown command strobe 0x{0:X2}", addr);
                    break;
            }
            UpdateGdoLines();
        }

        // STATUS byte returned on the header (and on write data bytes).
        private byte BuildStatusByte()
        {
            int fifoField;
            if(headerRead)
            {
                fifoField = rxFifo.Count;
            }
            else
            {
                fifoField = FifoSize - txFifo.Count;
            }
            if(fifoField > 0x0F)
            {
                fifoField = 0x0F;
            }
            // CHIP_RDYn (bit7) = 0, STATE (bits6:4), FIFO_BYTES (bits3:0).
            return (byte)(((chipState & 0x7) << 4) | (fifoField & 0x0F));
        }

        // Drive the GDO0/GDO2 constant-level output settings. Only the constant
        // settings (GDOx_CFG = 0x2E high-Z, 0x2F constant, with GDOx_INV) are
        // modeled -- enough for the firmware's GDO pin-identify logic. A real
        // OOK / serial-data GDO waveform is a later air-medium wave.
        private void UpdateGdoLines()
        {
            SetGdoFromIocfg(configRegs[RegIOCFG0], GDO0);
            SetGdoFromIocfg(configRegs[RegIOCFG1], GDO1);
            SetGdoFromIocfg(configRegs[RegIOCFG2], GDO2);
        }

        private void SetGdoFromIocfg(byte iocfg, GPIO line)
        {
            int cfg = iocfg & 0x3F;
            bool inv = (iocfg & 0x40) != 0;
            if(cfg == GdoCfgConstant) // 0x2F: HW to 0, INV flips to 1
            {
                line.Set(inv);
            }
            else // 0x2E high-Z or an unmodeled data mode -> park low (INV flips)
            {
                line.Set(inv);
            }
        }

        private enum Access
        {
            ConfigRead,
            ConfigWrite,
            StatusRead,
            PatableRead,
            PatableWrite,
            FifoRead,
            FifoWrite,
            Strobe,
        }

        private readonly byte[] configRegs;
        private readonly byte[] patable;
        private readonly Queue<byte> txFifo;
        private readonly Queue<byte> rxFifo;

        private int byteIndex;
        private bool headerRead;
        private bool headerBurst;
        private byte headerAddr;
        private byte curAddr;
        private Access curAccess;

        private byte marcState;
        private int chipState;

        // --- constants ---
        private const int ConfigRegCount = 0x2F;   // 0x00-0x2E
        private const int PatableSize = 8;
        private const int FifoSize = 64;

        private const byte AddrPatable = 0x3E;
        private const byte AddrFifo = 0x3F;

        private const byte RegIOCFG2 = 0x00;
        private const byte RegIOCFG1 = 0x01;
        private const byte RegIOCFG0 = 0x02;

        private const byte PartNum = 0x00;         // status 0x30
        private const byte Version = 0x14;         // status 0x31
        private const byte RssiValue = 0x80;       // status 0x34 (raw, plausible)

        private const byte GdoCfgConstant = 0x2F;

        // Chip STATE field (status byte bits6:4).
        private const int StateIdle = 0x0;
        private const int StateRx = 0x1;
        private const int StateTx = 0x2;
        private const int StateFstxon = 0x3;

        // MARCSTATE (status register 0x35) values.
        private const byte MarcStateSleep = 0x00;
        private const byte MarcStateIdle = 0x01;
        private const byte MarcStateFstxon = 0x12;
        private const byte MarcStateRx = 0x0D;
        private const byte MarcStateTx = 0x13;

        // Command strobes (addresses 0x30-0x3D, burst bit = 0).
        private const byte StrobeSRES = 0x30;
        private const byte StrobeSFSTXON = 0x31;
        private const byte StrobeSXOFF = 0x32;
        private const byte StrobeSCAL = 0x33;
        private const byte StrobeSRX = 0x34;
        private const byte StrobeSTX = 0x35;
        private const byte StrobeSIDLE = 0x36;
        private const byte StrobeSWOR = 0x38;
        private const byte StrobeSPWD = 0x39;
        private const byte StrobeSFRX = 0x3A;
        private const byte StrobeSFTX = 0x3B;
        private const byte StrobeSWORRST = 0x3C;
        private const byte StrobeSNOP = 0x3D;

        // CC1101 power-on register defaults (datasheet SWRS061 section 29).
        private static readonly byte[] DefaultConfig = new byte[ConfigRegCount]
        {
            0x29, // 0x00 IOCFG2
            0x2E, // 0x01 IOCFG1
            0x3F, // 0x02 IOCFG0
            0x07, // 0x03 FIFOTHR
            0xD3, // 0x04 SYNC1
            0x91, // 0x05 SYNC0
            0xFF, // 0x06 PKTLEN
            0x04, // 0x07 PKTCTRL1
            0x45, // 0x08 PKTCTRL0
            0x00, // 0x09 ADDR
            0x00, // 0x0A CHANNR
            0x0F, // 0x0B FSCTRL1
            0x00, // 0x0C FSCTRL0
            0x1E, // 0x0D FREQ2
            0xC4, // 0x0E FREQ1
            0xEC, // 0x0F FREQ0
            0x8C, // 0x10 MDMCFG4
            0x22, // 0x11 MDMCFG3
            0x02, // 0x12 MDMCFG2
            0x22, // 0x13 MDMCFG1
            0xF8, // 0x14 MDMCFG0
            0x47, // 0x15 DEVIATN
            0x07, // 0x16 MCSM2
            0x30, // 0x17 MCSM1
            0x04, // 0x18 MCSM0
            0x36, // 0x19 FOCCFG
            0x6C, // 0x1A BSCFG
            0x03, // 0x1B AGCCTRL2
            0x40, // 0x1C AGCCTRL1
            0x91, // 0x1D AGCCTRL0
            0x87, // 0x1E WOREVT1
            0x6B, // 0x1F WOREVT0
            0xF8, // 0x20 WORCTRL
            0x56, // 0x21 FREND1
            0x10, // 0x22 FREND0
            0xA9, // 0x23 FSCAL3
            0x0A, // 0x24 FSCAL2
            0x20, // 0x25 FSCAL1
            0x0D, // 0x26 FSCAL0
            0x41, // 0x27 RCCTRL1
            0x00, // 0x28 RCCTRL0
            0x59, // 0x29 FSTEST
            0x7F, // 0x2A PTEST
            0x3F, // 0x2B AGCTEST
            0x88, // 0x2C TEST2
            0x31, // 0x2D TEST1
            0x0B, // 0x2E TEST0
        };
    }
}
