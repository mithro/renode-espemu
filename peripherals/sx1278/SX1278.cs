// SX1278.cs -- a Semtech SX1276/77/78 (SX127x) sub-GHz radio model for Renode,
// attached as an ISPIPeripheral slave of the ESP32-C3 SPI2 master (wave-2 radio
// bring-up, building on the wave-1 GP-SPI substrate).
//
// SCOPE (wave 2, "foundation"):
//   * Register-accurate SPI read/write with burst auto-increment.
//   * RegVersion (0x42) = 0x12 so the firmware's identify() passes.
//   * RegOpMode (0x01) mode-bit + LongRangeMode (LoRa) transitions, including
//     the datasheet rule that LongRangeMode is only writable in SLEEP mode.
//   * A datasheet power-on-reset (POR) default register map (FSK/OOK page).
//   * DIO0 exposed as INumberedGPIOOutput (numbered output 0) for later waves.
//
// NOT modeled here (deferred to a later wave with the shared 433 MHz air
// medium): the RF data path, the FSK/OOK packet engine, the FIFO as a real
// queue, RSSI/AFC/PLL behaviour, and any DIO0 interrupt generation. RegFifo
// (0x00) is treated as a plain register byte; DIO0 is exposed but never asserted
// by this model yet.
//
// Attach it in a .repl as a child of the SPI2 master and wire DIO0 to a GPIO
// input pin:
//
//     spi2: SPI.ESP32C3_SPI2 @ sysbus 0x60024000
//     radio: SPI.SX1278 @ spi2
//         0 -> gpio@4          // DIO0 -> GPIO pin 4
//
// SPI framing (SX127x): the first byte of every transaction is the address
// byte -- bit7 is wnr (1 = write, 0 = read), bits[6:0] are the 7-bit register
// address. Subsequent bytes are data; the address auto-increments per byte
// (burst mode). Chip-select deassert (FinishTransmission) ends the transaction.

using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Wireless;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class SX1278 : ISPIPeripheral, INumberedGPIOOutput, IAir433Node
    {
        // `medium` is an optional shared 433 MHz air medium the radio joins so an
        // injected/transmitted frame can reach its RX FIFO. Set from the .repl as
        // `medium: air`; when omitted the model behaves as the register-only
        // wave-2 model (no RF path).
        public SX1278(Air433Medium medium = null)
        {
            this.medium = medium;
            DIO0 = new GPIO();
            Connections = new Dictionary<int, IGPIO> { { 0, DIO0 } };
            rxFifo = new Queue<byte>();
            medium?.RegisterNode(this);
            Reset();
        }

        public byte Transmit(byte data)
        {
            if(!addressLatched)
            {
                // First byte of the transaction: the address byte.
                addressLatched = true;
                writeMode = (data & WriteBit) != 0;
                address = (byte)(data & AddressMask);
                // MISO during the address byte is undefined on the SX127x; the
                // firmware ignores this byte. Return 0.
                return 0x00;
            }

            byte result;
            if(writeMode)
            {
                WriteRegister(address, data);
                result = 0x00;
            }
            else if(address == RegFifo && rxFifo.Count > 0)
            {
                // Reading RegFifo pops the RX FIFO (a received FSK payload).
                result = rxFifo.Dequeue();
                if(rxFifo.Count == 0 && payloadReady)
                {
                    // PayloadReady de-asserts once the FIFO has been emptied.
                    payloadReady = false;
                    DIO0.Set(false);
                }
            }
            else
            {
                result = registers[address];
            }
            this.Log(LogLevel.Noisy, "SX1278 {0} reg 0x{1:X2} {2}=0x{3:X2}",
                writeMode ? "write" : "read", address, writeMode ? "<-" : "->",
                writeMode ? data : result);
            // Burst mode auto-increments the address, except RegFifo (0x00) which
            // is a FIFO port: consecutive accesses stay on it (SX127x behaviour).
            if(address != RegFifo)
            {
                address = (byte)((address + 1) & AddressMask);
            }
            return result;
        }

        public void FinishTransmission()
        {
            // Chip-select deasserted: the next byte starts a new transaction.
            addressLatched = false;
        }

        public void Reset()
        {
            addressLatched = false;
            writeMode = false;
            address = 0;
            rxFifo.Clear();
            payloadReady = false;
            LoadResetDefaults();
            DIO0.Set(false);
        }

        public GPIO DIO0 { get; }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        // --- Shared 433 MHz air medium (FSK receive path) ------------------------

        // Called by the air medium when a frame arrives. Models an FSK packet-mode
        // reception: the radio must be in FSK RX (RegOpMode LongRangeMode=0, mode
        // = RX 0x05). The payload is pushed into the RX FIFO (readable via RegFifo
        // 0x00) and DIO0 (PayloadReady in FSK RX) is asserted -> DIO0 IRQ.
        public void ReceiveAirFrame(byte[] data)
        {
            if(data == null || data.Length == 0)
            {
                return;
            }
            var opmode = registers[RegOpMode];
            var lora = (opmode & 0x80) != 0;
            var mode = opmode & ModeMask;
            if(lora || (mode != ModeFskRx && mode != ModeFskRxSingle))
            {
                this.Log(LogLevel.Warning,
                    "SX1278: FSK frame of {0} byte(s) dropped -- not in FSK RX (RegOpMode 0x{1:X2})",
                    data.Length, opmode);
                return;
            }
            rxFifo.Clear();
            foreach(var b in data)
            {
                rxFifo.Enqueue(b);
            }
            payloadReady = true;
            DIO0.Set(true);
            this.Log(LogLevel.Info, "SX1278: received FSK frame of {0} byte(s)", data.Length);
        }

        private void WriteRegister(byte addr, byte value)
        {
            switch(addr)
            {
                case RegVersion:
                    // Read-only chip-version register: ignore writes.
                    break;
                case RegOpMode:
                {
                    var current = registers[RegOpMode];
                    // LongRangeMode (bit7) may only change while in SLEEP mode
                    // (SX1276/78 datasheet 6.2, RegOpMode). Otherwise keep the
                    // old bit7; the mode field and the rest always update.
                    if((current & ModeMask) != ModeSleep)
                    {
                        value = (byte)((value & 0x7F) | (current & 0x80));
                    }
                    registers[RegOpMode] = value;
                    break;
                }
                default:
                    registers[addr] = value;
                    break;
            }
        }

        private void LoadResetDefaults()
        {
            for(var i = 0; i < registers.Length; i++)
            {
                registers[i] = 0x00;
            }
            // Power-on-reset defaults from the SX1276/77/78/79 datasheet
            // (Rev. 7) register table, FSK/OOK page. Reserved registers not
            // listed here reset to 0x00.
            foreach(var kv in ResetMap)
            {
                registers[kv.Key] = kv.Value;
            }
        }

        private bool addressLatched;
        private bool writeMode;
        private byte address;
        private bool payloadReady;
        private readonly Air433Medium medium;

        private readonly byte[] registers = new byte[RegisterCount];
        private readonly Queue<byte> rxFifo;

        private const int RegisterCount = 0x80;      // 7-bit address space
        private const byte WriteBit = 0x80;
        private const byte AddressMask = 0x7F;

        // RegFifo (0x00): the FIFO data port.
        private const byte RegFifo = 0x00;

        // RegOpMode (0x01)
        private const byte RegOpMode = 0x01;
        private const byte ModeMask = 0x07;
        private const byte ModeSleep = 0x00;
        private const byte ModeFskRx = 0x05;        // FSK/OOK receiver
        private const byte ModeFskRxSingle = 0x06;  // accepted leniently

        // RegVersion (0x42), silicon value 0x12 for SX1276/77/78.
        private const byte RegVersion = 0x42;

        private static readonly Dictionary<byte, byte> ResetMap = new Dictionary<byte, byte>
        {
            { 0x00, 0x00 }, // RegFifo
            { 0x01, 0x01 }, // RegOpMode (FSK, STDBY)
            { 0x02, 0x1A }, // RegBitrateMsb
            { 0x03, 0x0B }, // RegBitrateLsb
            { 0x04, 0x00 }, // RegFdevMsb
            { 0x05, 0x52 }, // RegFdevLsb
            { 0x06, 0x6C }, // RegFrfMsb
            { 0x07, 0x80 }, // RegFrfMid
            { 0x08, 0x00 }, // RegFrfLsb
            { 0x09, 0x4F }, // RegPaConfig
            { 0x0A, 0x09 }, // RegPaRamp
            { 0x0B, 0x2B }, // RegOcp
            { 0x0C, 0x20 }, // RegLna
            { 0x0D, 0x08 }, // RegRxConfig
            { 0x0E, 0x02 }, // RegRssiConfig
            { 0x0F, 0x0A }, // RegRssiCollision
            { 0x10, 0xFF }, // RegRssiThresh
            { 0x12, 0x15 }, // RegRxBw
            { 0x13, 0x0B }, // RegAfcBw
            { 0x14, 0x28 }, // RegOokPeak
            { 0x15, 0x0C }, // RegOokFix
            { 0x16, 0x12 }, // RegOokAvg
            { 0x1F, 0x40 }, // RegPreambleDetect
            { 0x24, 0x05 }, // RegOsc
            { 0x26, 0x03 }, // RegPreambleLsb
            { 0x27, 0x93 }, // RegSyncConfig
            { 0x28, 0x55 }, // RegSyncValue1
            { 0x29, 0x55 }, // RegSyncValue2
            { 0x2A, 0x55 }, // RegSyncValue3
            { 0x2B, 0x55 }, // RegSyncValue4
            { 0x2C, 0x55 }, // RegSyncValue5
            { 0x2D, 0x55 }, // RegSyncValue6
            { 0x2E, 0x55 }, // RegSyncValue7
            { 0x2F, 0x55 }, // RegSyncValue8
            { 0x30, 0x90 }, // RegPacketConfig1
            { 0x31, 0x40 }, // RegPacketConfig2
            { 0x32, 0x40 }, // RegPayloadLength
            { 0x35, 0x0F }, // RegFifoThresh
            { 0x39, 0xF5 }, // RegTimer1Coef (SyncWord in some maps)
            { 0x3A, 0x20 }, // RegTimer2Coef
            { 0x3B, 0x82 }, // RegImageCal
            { 0x3D, 0x02 }, // RegLowBat
            { 0x40, 0x00 }, // RegDioMapping1
            { 0x41, 0x00 }, // RegDioMapping2
            { 0x42, 0x12 }, // RegVersion (chip id)
            { 0x44, 0x2D }, // RegPllHop
            { 0x4B, 0x09 }, // RegTcxo
            { 0x4D, 0x84 }, // RegPaDac
            { 0x5B, 0x00 }, // RegFormerTemp
            { 0x61, 0x13 }, // RegAgcRef
            { 0x62, 0x0E }, // RegAgcThresh1
            { 0x63, 0x5B }, // RegAgcThresh2
            { 0x64, 0xDB }, // RegAgcThresh3
            { 0x70, 0xD0 }, // RegPll
        };
    }
}
