// ESP32-C3 Interrupt Matrix for Renode
//
// Routes peripheral interrupt sources (64 inputs) to CPU interrupt lines (32 outputs).
// Based on QEMU's esp32c3_intmatrix.c implementation.
//
// Register layout at DR_REG_INTERRUPT_CORE0_BASE (0x600C2000):
//   0x000-0x0FF: Source mapping registers (one per peripheral, 5 bits = CPU int line)
//   0x104: CPU_INT_ENABLE (bitmask of enabled CPU interrupt lines)
//   0x108: CPU_INT_TYPE (0=level, 1=edge per line)
//   0x10C: CPU_INT_CLEAR (write-1-to-clear edge interrupts)
//   0x110: CPU_INT_EIP_STATUS (pending interrupt status)
//   0x114-0x190: CPU_INT_PRI_0 through CPU_INT_PRI_31 (4-bit priority per line)
//   0x194: CPU_INT_THRESH (priority threshold)

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class ESP32C3_InterruptMatrix : BasicDoubleWordPeripheral, IKnownSize, IGPIOReceiver, INumberedGPIOOutput
    {
        public ESP32C3_InterruptMatrix(Machine machine) : base(machine)
        {
            // Initialize interrupt source mapping (64 sources, each maps to a CPU int line 0-31)
            sourceMapping = new uint[NumSources];

            // Initialize CPU interrupt state
            cpuIntEnable = 0;
            cpuIntType = 0;
            cpuIntPriority = new uint[NumCpuInterrupts];
            cpuIntThreshold = 0;
            irqPending = 0;
            sourceLevel = new bool[NumSources];

            // Create GPIO outputs for CPU interrupt lines
            var dict = new Dictionary<int, IGPIO>();
            for (int i = 0; i < NumCpuInterrupts; i++)
            {
                dict[i] = new GPIO();
            }
            Connections = dict;

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            Array.Clear(sourceMapping, 0, sourceMapping.Length);
            cpuIntEnable = 0;
            cpuIntType = 0;
            Array.Clear(cpuIntPriority, 0, cpuIntPriority.Length);
            cpuIntThreshold = 0;
            irqPending = 0;
            Array.Clear(sourceLevel, 0, sourceLevel.Length);
        }

        // IGPIOReceiver: called when a peripheral asserts/deasserts an interrupt source
        public void OnGPIO(int number, bool value)
        {
            if (number < 0 || number >= NumSources)
            {
                this.Log(LogLevel.Warning, "Invalid interrupt source: {0}", number);
                return;
            }

            sourceLevel[number] = value;
            uint cpuLine = sourceMapping[number] & 0x1F;

            if (cpuLine == 0)
            {
                // CPU line 0 is not a valid interrupt target
                return;
            }

            if (value)
            {
                // Source asserted: try to deliver interrupt
                TryDeliverInterrupt(cpuLine);
            }
            else
            {
                // Source deasserted: clear pending if this was the source
                ClearPendingForLine(cpuLine);
            }
        }

        // INumberedGPIOOutput
        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public long Size => 0x200;

        private void TryDeliverInterrupt(uint line)
        {
            if (line == 0 || line >= NumCpuInterrupts)
                return;

            uint lineBit = 1u << (int)line;

            // Check if this CPU interrupt line is enabled
            if ((cpuIntEnable & lineBit) == 0)
                return;

            // Check priority vs threshold
            uint priority = cpuIntPriority[line];
            if (priority < cpuIntThreshold)
            {
                // Below threshold, mark as pending
                irqPending |= lineBit;
                return;
            }

            // Fire the interrupt
            irqPending |= lineBit;
            Connections[(int)line].Set(true);

            // For edge-type interrupts, auto-deassert
            if ((cpuIntType & lineBit) != 0)
            {
                Connections[(int)line].Set(false);
            }
        }

        private void ClearPendingForLine(uint line)
        {
            if (line < NumCpuInterrupts)
            {
                uint lineBit = 1u << (int)line;
                irqPending &= ~lineBit;
                Connections[(int)line].Set(false);
            }
        }

        private void CheckPendingInterrupts()
        {
            // When threshold changes or interrupts re-enabled, check pending
            for (int line = 1; line < NumCpuInterrupts; line++)
            {
                uint lineBit = 1u << line;
                if ((irqPending & lineBit) != 0 &&
                    (cpuIntEnable & lineBit) != 0 &&
                    cpuIntPriority[line] >= cpuIntThreshold)
                {
                    Connections[line].Set(true);
                    if ((cpuIntType & lineBit) != 0)
                    {
                        Connections[line].Set(false);
                    }
                }
            }
        }

        private void DefineRegisters()
        {
            // Source mapping registers: 0x000-0x0FF (64 sources * 4 bytes)
            for (int src = 0; src < NumSources; src++)
            {
                int srcCapture = src;
                ((Registers)(src * 4)).Define(this)
                    .WithValueField(0, 5, name: $"SOURCE_{srcCapture}_MAP",
                        writeCallback: (_, value) =>
                        {
                            sourceMapping[srcCapture] = (uint)value;
                        },
                        valueProviderCallback: _ => sourceMapping[srcCapture]);
            }

            // CPU_INT_ENABLE: 0x104
            Registers.CpuIntEnable.Define(this)
                .WithValueField(0, 32, name: "CPU_INT_ENABLE",
                    writeCallback: (_, value) =>
                    {
                        cpuIntEnable = (uint)value;
                        CheckPendingInterrupts();
                    },
                    valueProviderCallback: _ => cpuIntEnable);

            // CPU_INT_TYPE: 0x108
            Registers.CpuIntType.Define(this)
                .WithValueField(0, 32, name: "CPU_INT_TYPE",
                    writeCallback: (_, value) => cpuIntType = (uint)value,
                    valueProviderCallback: _ => cpuIntType);

            // CPU_INT_CLEAR: 0x10C (write-1-to-clear)
            Registers.CpuIntClear.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Write, name: "CPU_INT_CLEAR",
                    writeCallback: (_, value) =>
                    {
                        irqPending &= ~(uint)value;
                        // Deassert cleared interrupt lines
                        for (int i = 0; i < NumCpuInterrupts; i++)
                        {
                            if (((uint)value & (1u << i)) != 0)
                            {
                                Connections[i].Set(false);
                            }
                        }
                    });

            // CPU_INT_EIP_STATUS: 0x110 (read-only)
            Registers.CpuIntEipStatus.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "CPU_INT_EIP_STATUS",
                    valueProviderCallback: _ => irqPending & cpuIntEnable);

            // CPU_INT_PRI_0 through CPU_INT_PRI_31: 0x114-0x190
            for (int i = 0; i < NumCpuInterrupts; i++)
            {
                int iCapture = i;
                ((Registers)(0x114 + i * 4)).Define(this)
                    .WithValueField(0, 4, name: $"CPU_INT_PRI_{iCapture}",
                        writeCallback: (_, value) => cpuIntPriority[iCapture] = (uint)value,
                        valueProviderCallback: _ => cpuIntPriority[iCapture]);
            }

            // CPU_INT_THRESH: 0x194
            Registers.CpuIntThresh.Define(this)
                .WithValueField(0, 4, name: "CPU_INT_THRESH",
                    writeCallback: (_, value) =>
                    {
                        if (value != cpuIntThreshold)
                        {
                            cpuIntThreshold = (uint)value;
                            CheckPendingInterrupts();
                        }
                    },
                    valueProviderCallback: _ => cpuIntThreshold);
        }

        private uint[] sourceMapping;
        private uint cpuIntEnable;
        private uint cpuIntType;
        private uint[] cpuIntPriority;
        private uint cpuIntThreshold;
        private uint irqPending;
        private bool[] sourceLevel;

        private const int NumSources = 64;
        private const int NumCpuInterrupts = 32;

        private enum Registers : long
        {
            // Source mappings at 0x000-0x0FC (defined dynamically)
            CpuIntEnable = 0x104,
            CpuIntType = 0x108,
            CpuIntClear = 0x10C,
            CpuIntEipStatus = 0x110,
            // CPU_INT_PRI_0-31 at 0x114-0x190 (defined dynamically)
            CpuIntThresh = 0x194,
        }
    }
}
