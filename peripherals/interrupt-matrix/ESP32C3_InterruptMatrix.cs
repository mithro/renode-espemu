// ESP32-C3 Interrupt Matrix for Renode
//
// Routes peripheral interrupt sources (62 inputs) to CPU interrupt lines (32 outputs).
// Based on QEMU's esp32c3_intmatrix.c implementation.
//
// Register layout at DR_REG_INTERRUPT_CORE0_BASE (0x600C2000):
//   0x000-0x0F4: Source mapping registers (62 peripherals, 5 bits = CPU int line)
//   0x0F8: INTR_STATUS_0 (read-only, source level status bits 0-31)
//   0x0FC: INTR_STATUS_1 (read-only, source level status bits 32-61)
//   0x100: CLOCK_GATE (bit 0 = CLK_EN, default 1)
//   0x104: CPU_INT_ENABLE (bitmask of enabled CPU interrupt lines)
//   0x108: CPU_INT_TYPE (0=level, 1=edge per line)
//   0x10C: CPU_INT_CLEAR (write-1-to-clear edge interrupts)
//   0x110: CPU_INT_EIP_STATUS (pending interrupt status)
//   0x114-0x190: CPU_INT_PRI_0 through CPU_INT_PRI_31 (4-bit priority per line)
//   0x194: CPU_INT_THRESH (priority threshold)
//   0x7FC: INTERRUPT_DATE (version register, default 0x2007210)

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class ESP32C3_InterruptMatrix : BasicDoubleWordPeripheral, IKnownSize, IGPIOReceiver, INumberedGPIOOutput
    {
        public ESP32C3_InterruptMatrix(Machine machine) : base(machine)
        {
            // Initialize interrupt source mapping (62 sources, each maps to a CPU int line 0-31)
            sourceMapping = new uint[NumSources];

            // Initialize CPU interrupt state
            cpuIntEnable = 0;
            cpuIntType = 0;
            cpuIntPriority = new uint[NumCpuInterrupts];
            cpuIntThreshold = 0;
            irqPending = 0;
            sourceLevel = new bool[NumSources];
            clockGateClkEn = true;
            interruptDate = 0x2007210;

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
            clockGateClkEn = true;
            interruptDate = 0x2007210;
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

        public long Size => 0x800;

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

            // Fire the interrupt via CLIC (per-line GPIO).
            // CLIC sets mcause = 0x80000000 | line automatically.
            irqPending |= lineBit;
            pendingMcause = 0x80000000u | line;

            Connections[(int)line].Set(true);
            this.Log(LogLevel.Info, "Asserted CLIC line {0}", line);
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
            // Source mapping registers: 0x000-0x0F4 (62 sources * 4 bytes)
            for (int src = 0; src < NumSources; src++)
            {
                int srcCapture = src;
                ((Registers)(src * 4)).Define(this)
                    .WithValueField(0, 5, name: $"SOURCE_{srcCapture}_MAP",
                        writeCallback: (_, value) =>
                        {
                            if (value != 0)
                            {
                                this.Log(LogLevel.Info, "Source {0} mapped to CPU int {1}", srcCapture, value & 0x1F);
                            }
                            sourceMapping[srcCapture] = (uint)value;
                        },
                        valueProviderCallback: _ => sourceMapping[srcCapture]);
            }

            // INTR_STATUS_0: 0x0F8 (read-only, interrupt source level status bits 0-31)
            Registers.IntrStatus0.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "INTR_STATUS_0",
                    valueProviderCallback: _ =>
                    {
                        uint status = 0;
                        for (int i = 0; i < 32 && i < NumSources; i++)
                        {
                            if (sourceLevel[i])
                                status |= 1u << i;
                        }
                        return status;
                    });

            // INTR_STATUS_1: 0x0FC (read-only, interrupt source level status bits 32-61)
            Registers.IntrStatus1.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "INTR_STATUS_1",
                    valueProviderCallback: _ =>
                    {
                        uint status = 0;
                        for (int i = 32; i < NumSources; i++)
                        {
                            if (sourceLevel[i])
                                status |= 1u << (i - 32);
                        }
                        return status;
                    });

            // CLOCK_GATE: 0x100 (bit 0 = CLK_EN, default 1)
            Registers.ClockGate.Define(this, 0x1)
                .WithFlag(0, name: "CLK_EN",
                    writeCallback: (_, value) => clockGateClkEn = value,
                    valueProviderCallback: _ => clockGateClkEn)
                .WithReservedBits(1, 31);

            // CPU_INT_ENABLE: 0x104
            Registers.CpuIntEnable.Define(this)
                .WithValueField(0, 32, name: "CPU_INT_ENABLE",
                    writeCallback: (_, value) =>
                    {
                        this.Log(LogLevel.Info, "CPU_INT_ENABLE = 0x{0:X8}", value);
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
                        // Deassert CLIC inputs for cleared lines
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
                        cpuIntThreshold = (uint)value;
                        // Re-evaluate: deassert lines below threshold, assert above
                        for (int i = 1; i < NumCpuInterrupts; i++)
                        {
                            uint bit = 1u << i;
                            bool shouldAssert = (irqPending & cpuIntEnable & bit) != 0
                                && cpuIntPriority[i] >= cpuIntThreshold;
                            Connections[i].Set(shouldAssert);
                        }
                    },
                    valueProviderCallback: _ => cpuIntThreshold);

            // INTERRUPT_DATE: 0x7FC (version/date register, 28 bits, default 0x2007210)
            Registers.InterruptDate.Define(this, 0x2007210)
                .WithValueField(0, 28, name: "INTERRUPT_DATE",
                    writeCallback: (_, value) => interruptDate = (uint)value,
                    valueProviderCallback: _ => interruptDate)
                .WithReservedBits(28, 4);
        }

        private uint[] sourceMapping;
        private uint cpuIntEnable;
        private uint cpuIntType;
        private uint[] cpuIntPriority;
        private uint cpuIntThreshold;
        private uint irqPending;
        private bool[] sourceLevel;

        // meipAsserted removed — CLIC handles per-line delivery
        private bool clockGateClkEn;
        private uint interruptDate;

        /// <summary>
        /// The mcause value that should be set when the CPU takes this interrupt.
        /// Format: 0x80000000 | cpu_line_number
        /// </summary>
        public uint PendingMcause { get { return pendingMcause; } }
        private uint pendingMcause;

        private const int NumSources = 62;
        private const int NumCpuInterrupts = 32;
        private const int MeipLine = 11; // Machine External Interrupt Pending

        private enum Registers : long
        {
            // Source mappings at 0x000-0x0F4 (defined dynamically, 62 sources)
            IntrStatus0 = 0x0F8,
            IntrStatus1 = 0x0FC,
            ClockGate = 0x100,
            CpuIntEnable = 0x104,
            CpuIntType = 0x108,
            CpuIntClear = 0x10C,
            CpuIntEipStatus = 0x110,
            // CPU_INT_PRI_0-31 at 0x114-0x190 (defined dynamically)
            CpuIntThresh = 0x194,
            InterruptDate = 0x7FC,
        }
    }
}
