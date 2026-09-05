// SPILoopbackTester -- a trivial ISPIPeripheral test slave for the ESP32-C3
// SPI2 master model (wave-1 GP-SPI bring-up). NOT a production peripheral.
//
// Attach it in a test .repl as a child of the SPI2 master, and optionally wire
// its IRQ output (numbered GPIO output 0) to a GPIO input pin:
//
//     spi2: SPI.ESP32C3_SPI2 @ sysbus 0x60024000
//     tester: SPI.SPILoopbackTester @ spi2
//         0 -> gpio@4          // IRQ line into GPIO pin 4
//
// Behaviour:
//   * Transmit(b) returns (b XOR 0xA5). This deterministic transform proves the
//     byte actually travelled through the slave (a plain wire loopback would
//     return b unchanged). The next SPI radio models return real register data
//     here instead.
//   * Receiving the byte 0xAA asserts the IRQ output (0 -> 1, a rising edge);
//     receiving 0x55 deasserts it. This demonstrates a *slave-driven* interrupt
//     reaching the CPU through GPIO edge detection + the interrupt matrix.
//
// This is the exact interface (ISPIPeripheral + INumberedGPIOOutput) that the
// CC1101 and SX1278 radio models will implement.

using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.SPI;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class SPILoopbackTester : ISPIPeripheral, INumberedGPIOOutput
    {
        public SPILoopbackTester()
        {
            IRQ = new GPIO();
            Connections = new Dictionary<int, IGPIO> { { 0, IRQ } };
        }

        public byte Transmit(byte data)
        {
            switch(data)
            {
                case IrqAssertByte:
                    this.Log(LogLevel.Debug, "IRQ asserted by SPI byte 0x{0:X2}", data);
                    IRQ.Set(true);
                    break;
                case IrqDeassertByte:
                    this.Log(LogLevel.Debug, "IRQ deasserted by SPI byte 0x{0:X2}", data);
                    IRQ.Set(false);
                    break;
            }
            return (byte)(data ^ TransformKey);
        }

        public void FinishTransmission()
        {
        }

        public void Reset()
        {
            IRQ.Set(false);
        }

        public GPIO IRQ { get; }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        private const byte TransformKey = 0xA5;
        private const byte IrqAssertByte = 0xAA;
        private const byte IrqDeassertByte = 0x55;
    }
}
