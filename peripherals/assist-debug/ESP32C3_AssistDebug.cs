// ESP32-C3 Assist Debug peripheral for Renode
//
// Hardware assist for RISC-V CPU debugging: area monitors, SP overflow
// detection, PC recording, and exception logging.
//
// Base: 0x600CE000, Size: 0x1000
//
// During boot, firmware configures SP monitoring boundaries and enables
// PC recording. All registers are simple storage — no active monitoring
// behavior is implemented (hardware monitors bus transactions, which
// this peripheral does not do).

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class ESP32C3_AssistDebug : BasicDoubleWordPeripheral, IKnownSize
    {
        public ESP32C3_AssistDebug(Machine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            // Interrupt registers
            Registers.IntrEna.Define(this)
                .WithValueField(0, 12, name: "CORE_0_INTR_ENA");
            Registers.IntrRaw.Define(this)
                .WithValueField(0, 12, mode: FieldMode.Read, name: "CORE_0_INTR_RAW");
            Registers.IntrRls.Define(this)
                .WithValueField(0, 12, name: "CORE_0_INTR_RLS");
            Registers.IntrClr.Define(this)
                .WithValueField(0, 12, mode: FieldMode.Write, name: "CORE_0_INTR_CLR");

            // Area monitor registers
            Registers.AreaDram0_0Min.Define(this)
                .WithValueField(0, 32, name: "CORE_0_AREA_DRAM0_0_MIN");
            Registers.AreaDram0_0Max.Define(this)
                .WithValueField(0, 32, name: "CORE_0_AREA_DRAM0_0_MAX");
            Registers.AreaDram0_1Min.Define(this)
                .WithValueField(0, 32, name: "CORE_0_AREA_DRAM0_1_MIN");
            Registers.AreaDram0_1Max.Define(this)
                .WithValueField(0, 32, name: "CORE_0_AREA_DRAM0_1_MAX");
            Registers.AreaPif0Min.Define(this)
                .WithValueField(0, 32, name: "CORE_0_AREA_PIF_0_MIN");
            Registers.AreaPif0Max.Define(this)
                .WithValueField(0, 32, name: "CORE_0_AREA_PIF_0_MAX");
            Registers.AreaPif1Min.Define(this)
                .WithValueField(0, 32, name: "CORE_0_AREA_PIF_1_MIN");
            Registers.AreaPif1Max.Define(this)
                .WithValueField(0, 32, name: "CORE_0_AREA_PIF_1_MAX");

            // Area monitor capture (read-only)
            Registers.AreaPc.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "CORE_0_AREA_PC");
            Registers.AreaSp.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "CORE_0_AREA_SP");

            // SP monitoring
            Registers.SpMin.Define(this)
                .WithValueField(0, 32, name: "CORE_0_SP_MIN");
            Registers.SpMax.Define(this)
                .WithValueField(0, 32, name: "CORE_0_SP_MAX");
            Registers.SpPc.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "CORE_0_SP_PC");

            // Recording control
            Registers.RcdEn.Define(this)
                .WithFlag(0, name: "PDEBUG_ENABLE")
                .WithFlag(1, name: "RECORD_DISABLE_DRAM0_REGION");
            Registers.RcdPc.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "CORE_0_RCD_PDEBUGPC");
            Registers.RcdSp.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "CORE_0_RCD_PDEBUGSP");

            // Exception monitors (read-only)
            Registers.Iram0ExMon0.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "CORE_0_IRAM0_EXCEPTION_MONITOR_0");
            Registers.Iram0ExMon1.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "CORE_0_IRAM0_EXCEPTION_MONITOR_1");
            Registers.Dram0ExMon0.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "CORE_0_DRAM0_EXCEPTION_MONITOR_0");
            Registers.Dram0ExMon1.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "CORE_0_DRAM0_EXCEPTION_MONITOR_1");
            Registers.Dram0ExMon2.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "CORE_0_DRAM0_EXCEPTION_MONITOR_2");
            Registers.Dram0ExMon3.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "CORE_0_DRAM0_EXCEPTION_MONITOR_3");
            Registers.CoreXExMon0.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "CORE_X_IRAM0_DRAM0_EXCEPTION_MONITOR_0");
            Registers.CoreXExMon1.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "CORE_X_IRAM0_DRAM0_EXCEPTION_MONITOR_1");

            // Logging registers
            Registers.LogSetting.Define(this)
                .WithValueField(0, 32, name: "LOG_SETTING");
            Registers.LogData0.Define(this)
                .WithValueField(0, 32, name: "LOG_DATA_0");
            Registers.LogDataMask.Define(this)
                .WithValueField(0, 32, name: "LOG_DATA_MASK");
            Registers.LogMin.Define(this)
                .WithValueField(0, 32, name: "LOG_MIN");
            Registers.LogMax.Define(this)
                .WithValueField(0, 32, name: "LOG_MAX");
            Registers.LogMemStart.Define(this)
                .WithValueField(0, 32, name: "LOG_MEM_START");
            Registers.LogMemEnd.Define(this)
                .WithValueField(0, 32, name: "LOG_MEM_END");
            Registers.LogMemWritingAddr.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "LOG_MEM_WRITING_ADDR");
            Registers.LogMemFullFlag.Define(this)
                .WithFlag(0, mode: FieldMode.Read, name: "LOG_MEM_FULL_FLAG");

            // Debug state (read-only)
            Registers.LastPcBeforeException.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "CORE_0_LASTPC_BEFORE_EXCEPTION");
            Registers.DebugMode.Define(this)
                .WithFlag(0, mode: FieldMode.Read, name: "DEBUG_MODE")
                .WithFlag(1, mode: FieldMode.Read, name: "DEBUG_MODULE_ACTIVE");

            // Version register
            Registers.Date.Define(this, 0x02008010)
                .WithValueField(0, 28, name: "DATE");
        }

        private enum Registers : long
        {
            IntrEna         = 0x000,
            IntrRaw         = 0x004,
            IntrRls         = 0x008,
            IntrClr         = 0x00C,
            AreaDram0_0Min  = 0x010,
            AreaDram0_0Max  = 0x014,
            AreaDram0_1Min  = 0x018,
            AreaDram0_1Max  = 0x01C,
            AreaPif0Min     = 0x020,
            AreaPif0Max     = 0x024,
            AreaPif1Min     = 0x028,
            AreaPif1Max     = 0x02C,
            AreaPc          = 0x030,
            AreaSp          = 0x034,
            SpMin           = 0x038,
            SpMax           = 0x03C,
            SpPc            = 0x040,
            RcdEn           = 0x044,
            RcdPc           = 0x048,
            RcdSp           = 0x04C,
            Iram0ExMon0     = 0x050,
            Iram0ExMon1     = 0x054,
            Dram0ExMon0     = 0x058,
            Dram0ExMon1     = 0x05C,
            Dram0ExMon2     = 0x060,
            Dram0ExMon3     = 0x064,
            CoreXExMon0     = 0x068,
            CoreXExMon1     = 0x06C,
            LogSetting      = 0x070,
            LogData0        = 0x074,
            LogDataMask     = 0x078,
            LogMin          = 0x07C,
            LogMax          = 0x080,
            LogMemStart     = 0x084,
            LogMemEnd       = 0x088,
            LogMemWritingAddr = 0x08C,
            LogMemFullFlag  = 0x090,
            LastPcBeforeException = 0x094,
            DebugMode       = 0x098,
            Date            = 0x1FC,
        }
    }
}
