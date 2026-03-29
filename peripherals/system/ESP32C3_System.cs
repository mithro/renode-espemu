// ESP32-C3 SYSTEM Peripheral for Renode
//
// Controls clock gating, CPU frequency, reset, and cross-core interrupts.
// Base: DR_REG_SYSTEM_BASE = 0x600C0000, Size: 0x1000
//
// Critical register: CPU_INTR_FROM_CPU_0 (0x28)
//   Writing 1 fires GPIO output 0, which connects to interrupt matrix source 50
//   (ETS_FROM_CPU_INTR0). Used by FreeRTOS vPortYield for cross-core interrupts.
//
// GPIO outputs 0-3 map to interrupt matrix sources 50-53 (FROM_CPU_INTR0-3).

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class ESP32C3_System : BasicDoubleWordPeripheral, IKnownSize, INumberedGPIOOutput
    {
        public ESP32C3_System(Machine machine) : base(machine)
        {
            cpuIntrPending = new bool[NumCpuIntrSources];

            // Create GPIO outputs for FROM_CPU_INTR0-3
            var dict = new Dictionary<int, IGPIO>();
            for (int i = 0; i < NumCpuIntrSources; i++)
            {
                dict[i] = new GPIO();
            }
            Connections = dict;

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            Array.Clear(cpuIntrPending, 0, cpuIntrPending.Length);
            for (int i = 0; i < NumCpuIntrSources; i++)
            {
                Connections[i].Set(false);
            }
        }

        // INumberedGPIOOutput
        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            // 0x00: CPU_PERI_CLK_EN_REG
            // Bits: [7] CLK_EN_DEDICATED_GPIO (default 0), [6] CLK_EN_ASSIST_DEBUG (default 0)
            Registers.CpuPeriClkEn.Define(this, 0x00000000)
                .WithValueField(0, 32, name: "CPU_PERI_CLK_EN");

            // 0x04: CPU_PERI_RST_EN_REG
            // Bits: [7] RST_EN_DEDICATED_GPIO (default 1), [6] RST_EN_ASSIST_DEBUG (default 1)
            Registers.CpuPeriRstEn.Define(this, 0x000000C0)
                .WithValueField(0, 32, name: "CPU_PERI_RST_EN");

            // 0x08: CPU_PER_CONF_REG
            // [7:4] CPU_WAITI_DELAY_NUM (default 0), [3] CPU_WAIT_MODE_FORCE_ON (default 1),
            // [2] PLL_FREQ_SEL (default 1), [1:0] CPUPERIOD_SEL (default 0)
            Registers.CpuPerConf.Define(this, 0x0000000C)
                .WithValueField(0, 32, name: "CPU_PER_CONF");

            // 0x0C: MEM_PD_MASK_REG
            // [0] LSLP_MEM_PD_MASK (default 1)
            Registers.MemPdMask.Define(this, 0x00000001)
                .WithValueField(0, 32, name: "MEM_PD_MASK");

            // 0x10: PERIP_CLK_EN0_REG
            // Default: many peripherals clocked by default. From header bit defaults:
            // bits 31,30,29,28,27,24,23,22,16,15,14,13,6,5,3,2,1,0 = 1
            Registers.PeripClkEn0.Define(this, 0xF9C1E06F)
                .WithValueField(0, 32, name: "PERIP_CLK_EN0");

            // 0x14: PERIP_CLK_EN1_REG
            // Default: all 0 (crypto/DMA not clocked by default)
            Registers.PeripClkEn1.Define(this, 0x00000000)
                .WithValueField(0, 32, name: "PERIP_CLK_EN1");

            // 0x18: PERIP_RST_EN0_REG
            // Default: all 0 (no peripherals in reset)
            Registers.PeripRstEn0.Define(this, 0x00000000)
                .WithValueField(0, 32, name: "PERIP_RST_EN0");

            // 0x1C: PERIP_RST_EN1_REG
            // Default: crypto/DMA in reset (bits 1-8 = 1)
            Registers.PeripRstEn1.Define(this, 0x000001FE)
                .WithValueField(0, 32, name: "PERIP_RST_EN1");

            // 0x20: BT_LPCK_DIV_INT_REG
            // [11:0] BT_LPCK_DIV_NUM (default 255 = 0xFF)
            Registers.BtLpckDivInt.Define(this, 0x000000FF)
                .WithValueField(0, 32, name: "BT_LPCK_DIV_INT");

            // 0x24: BT_LPCK_DIV_FRAC_REG
            // [28] LPCLK_RTC_EN (0), [27] SEL_XTAL32K (0), [26] SEL_XTAL (0),
            // [25] SEL_8M (1), [24] SEL_RTC_SLOW (0),
            // [23:12] DIV_A (1), [11:0] DIV_B (1)
            Registers.BtLpckDivFrac.Define(this, 0x02001001)
                .WithValueField(0, 32, name: "BT_LPCK_DIV_FRAC");

            // 0x28-0x34: CPU_INTR_FROM_CPU_0 through CPU_INTR_FROM_CPU_3
            // Writing bit 0 = 1 asserts the GPIO then auto-clears (pulse).
            // Reading always returns 0 (matches Python stub behavior needed for
            // vPortYield which polls until the register reads 0).
            for (int i = 0; i < NumCpuIntrSources; i++)
            {
                int idx = i;
                ((Registers)(0x28 + i * 4)).Define(this)
                    .WithFlag(0, name: $"CPU_INTR_FROM_CPU_{idx}",
                        writeCallback: (_, value) =>
                        {
                            if (value)
                            {
                                // Assert GPIO to interrupt matrix, then immediately
                                // clear pending so reads return 0 (pulse semantics)
                                Connections[idx].Set(true);
                                cpuIntrPending[idx] = false;
                                this.Log(LogLevel.Debug, "FROM_CPU_INTR{0} pulsed (-> interrupt matrix source {1})",
                                    idx, 50 + idx);
                            }
                            else
                            {
                                Connections[idx].Set(false);
                                cpuIntrPending[idx] = false;
                            }
                        },
                        valueProviderCallback: _ => cpuIntrPending[idx])
                    .WithReservedBits(1, 31);
            }

            // 0x38: RSA_PD_CTRL_REG
            // [2] RSA_MEM_FORCE_PD (0), [1] RSA_MEM_FORCE_PU (0), [0] RSA_MEM_PD (default 1)
            Registers.RsaPdCtrl.Define(this, 0x00000001)
                .WithValueField(0, 32, name: "RSA_PD_CTRL");

            // 0x3C: EDMA_CTRL_REG
            // [1] EDMA_RESET (0), [0] EDMA_CLK_ON (default 1)
            Registers.EdmaCtrl.Define(this, 0x00000001)
                .WithValueField(0, 32, name: "EDMA_CTRL");

            // 0x40: CACHE_CONTROL_REG
            // [3] DCACHE_RESET (0), [2] DCACHE_CLK_ON (1), [1] ICACHE_RESET (0), [0] ICACHE_CLK_ON (1)
            Registers.CacheControl.Define(this, 0x00000005)
                .WithValueField(0, 32, name: "CACHE_CONTROL");

            // 0x44: EXTERNAL_DEVICE_ENCRYPT_DECRYPT_CONTROL_REG
            Registers.ExternalDeviceEncryptDecryptControl.Define(this, 0x00000000)
                .WithValueField(0, 32, name: "EXTERNAL_DEVICE_ENCRYPT_DECRYPT_CONTROL");

            // 0x48: RTC_FASTMEM_CONFIG_REG
            // [31] CRC_FINISH (RO, 0), [30:20] CRC_LEN (default 0x7FF), [19:9] CRC_ADDR (0), [8] CRC_START (0)
            Registers.RtcFastmemConfig.Define(this, 0x7FF00000)
                .WithValueField(0, 32, name: "RTC_FASTMEM_CONFIG");

            // 0x4C: RTC_FASTMEM_CRC_REG (read-only)
            Registers.RtcFastmemCrc.Define(this, 0x00000000)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "RTC_FASTMEM_CRC");

            // 0x50: REDUNDANT_ECO_CTRL_REG
            // [1] ECO_RESULT (RO, 0), [0] ECO_DRIVE (R/W, 0)
            Registers.RedundantEcoCtrl.Define(this, 0x00000000)
                .WithValueField(0, 32, name: "REDUNDANT_ECO_CTRL");

            // 0x54: CLOCK_GATE_REG
            // [0] CLK_EN (default 1)
            Registers.ClockGate.Define(this, 0x00000001)
                .WithValueField(0, 32, name: "CLOCK_GATE");

            // 0x58: SYSCLK_CONF_REG
            // [19] CLK_DIV_EN (RO, 0), [18:12] CLK_XTAL_FREQ (RO, 40 = 0x28),
            // [11:10] SOC_CLK_SEL (R/W, 0 = XTAL), [9:0] PRE_DIV_CNT (R/W, default 1)
            Registers.SysclkConf.Define(this, 0x00028001)
                .WithValueField(0, 10, name: "PRE_DIV_CNT")
                .WithValueField(10, 2, name: "SOC_CLK_SEL")
                .WithValueField(12, 7, mode: FieldMode.Read, name: "CLK_XTAL_FREQ",
                    valueProviderCallback: _ => 40) // 40 MHz, read-only
                .WithFlag(19, mode: FieldMode.Read, name: "CLK_DIV_EN")
                .WithReservedBits(20, 12);

            // 0x5C: MEM_PVT_REG
            // [23:22] MEM_VT_SEL (R/W, 0), [21:6] MEM_TIMING_ERR_CNT (RO, 0),
            // [5] MEM_PVT_MONITOR_EN (R/W, 0), [4] MEM_ERR_CNT_CLR (WO, 0), [3:0] MEM_PATH_LEN (R/W, default 3)
            Registers.MemPvt.Define(this, 0x00000003)
                .WithValueField(0, 32, name: "MEM_PVT");

            // 0x60: COMB_PVT_LVT_CONF_REG
            // [6] COMB_PVT_MONITOR_EN_LVT (R/W, 0), [5] COMB_ERR_CNT_CLR_LVT (WO, 0),
            // [4:0] COMB_PATH_LEN_LVT (R/W, default 3)
            Registers.CombPvtLvtConf.Define(this, 0x00000003)
                .WithValueField(0, 32, name: "COMB_PVT_LVT_CONF");

            // 0x64: COMB_PVT_NVT_CONF_REG
            // [6] COMB_PVT_MONITOR_EN_NVT (R/W, 0), [5] COMB_ERR_CNT_CLR_NVT (WO, 0),
            // [4:0] COMB_PATH_LEN_NVT (R/W, default 3)
            Registers.CombPvtNvtConf.Define(this, 0x00000003)
                .WithValueField(0, 32, name: "COMB_PVT_NVT_CONF");

            // 0x68: COMB_PVT_HVT_CONF_REG
            // [6] COMB_PVT_MONITOR_EN_HVT (R/W, 0), [5] COMB_ERR_CNT_CLR_HVT (WO, 0),
            // [4:0] COMB_PATH_LEN_HVT (R/W, default 3)
            Registers.CombPvtHvtConf.Define(this, 0x00000003)
                .WithValueField(0, 32, name: "COMB_PVT_HVT_CONF");

            // 0x6C: COMB_PVT_ERR_LVT_SITE0_REG (read-only)
            // [15:0] COMB_TIMING_ERR_CNT_LVT_SITE0 (RO, default 0)
            Registers.CombPvtErrLvtSite0.Define(this, 0x00000000)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "COMB_PVT_ERR_LVT_SITE0");

            // 0x70: COMB_PVT_ERR_NVT_SITE0_REG (read-only)
            // [15:0] COMB_TIMING_ERR_CNT_NVT_SITE0 (RO, default 0)
            Registers.CombPvtErrNvtSite0.Define(this, 0x00000000)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "COMB_PVT_ERR_NVT_SITE0");

            // 0x74: COMB_PVT_ERR_HVT_SITE0_REG (read-only)
            // [15:0] COMB_TIMING_ERR_CNT_HVT_SITE0 (RO, default 0)
            Registers.CombPvtErrHvtSite0.Define(this, 0x00000000)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "COMB_PVT_ERR_HVT_SITE0");

            // 0x78: COMB_PVT_ERR_LVT_SITE1_REG (read-only)
            // [15:0] COMB_TIMING_ERR_CNT_LVT_SITE1 (RO, default 0)
            Registers.CombPvtErrLvtSite1.Define(this, 0x00000000)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "COMB_PVT_ERR_LVT_SITE1");

            // 0x7C: COMB_PVT_ERR_NVT_SITE1_REG (read-only)
            // [15:0] COMB_TIMING_ERR_CNT_NVT_SITE1 (RO, default 0)
            Registers.CombPvtErrNvtSite1.Define(this, 0x00000000)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "COMB_PVT_ERR_NVT_SITE1");

            // 0x80: COMB_PVT_ERR_HVT_SITE1_REG (read-only)
            // [15:0] COMB_TIMING_ERR_CNT_HVT_SITE1 (RO, default 0)
            Registers.CombPvtErrHvtSite1.Define(this, 0x00000000)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "COMB_PVT_ERR_HVT_SITE1");

            // 0x84: COMB_PVT_ERR_LVT_SITE2_REG (read-only)
            // [15:0] COMB_TIMING_ERR_CNT_LVT_SITE2 (RO, default 0)
            Registers.CombPvtErrLvtSite2.Define(this, 0x00000000)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "COMB_PVT_ERR_LVT_SITE2");

            // 0x88: COMB_PVT_ERR_NVT_SITE2_REG (read-only)
            // [15:0] COMB_TIMING_ERR_CNT_NVT_SITE2 (RO, default 0)
            Registers.CombPvtErrNvtSite2.Define(this, 0x00000000)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "COMB_PVT_ERR_NVT_SITE2");

            // 0x8C: COMB_PVT_ERR_HVT_SITE2_REG (read-only)
            // [15:0] COMB_TIMING_ERR_CNT_HVT_SITE2 (RO, default 0)
            Registers.CombPvtErrHvtSite2.Define(this, 0x00000000)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "COMB_PVT_ERR_HVT_SITE2");

            // 0x90: COMB_PVT_ERR_LVT_SITE3_REG (read-only)
            // [15:0] COMB_TIMING_ERR_CNT_LVT_SITE3 (RO, default 0)
            Registers.CombPvtErrLvtSite3.Define(this, 0x00000000)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "COMB_PVT_ERR_LVT_SITE3");

            // 0x94: COMB_PVT_ERR_NVT_SITE3_REG (read-only)
            // [15:0] COMB_TIMING_ERR_CNT_NVT_SITE3 (RO, default 0)
            Registers.CombPvtErrNvtSite3.Define(this, 0x00000000)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "COMB_PVT_ERR_NVT_SITE3");

            // 0x98: COMB_PVT_ERR_HVT_SITE3_REG (read-only)
            // [15:0] COMB_TIMING_ERR_CNT_HVT_SITE3 (RO, default 0)
            Registers.CombPvtErrHvtSite3.Define(this, 0x00000000)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "COMB_PVT_ERR_HVT_SITE3");

            // 0xFFC: DATE_REG
            // [27:0] DATE (default 0x2007150)
            Registers.Date.Define(this, 0x02007150)
                .WithValueField(0, 32, name: "DATE");
        }

        private bool[] cpuIntrPending;

        private const int NumCpuIntrSources = 4;

        private enum Registers : long
        {
            CpuPeriClkEn = 0x00,
            CpuPeriRstEn = 0x04,
            CpuPerConf = 0x08,
            MemPdMask = 0x0C,
            PeripClkEn0 = 0x10,
            PeripClkEn1 = 0x14,
            PeripRstEn0 = 0x18,
            PeripRstEn1 = 0x1C,
            BtLpckDivInt = 0x20,
            BtLpckDivFrac = 0x24,
            CpuIntrFromCpu0 = 0x28,
            CpuIntrFromCpu1 = 0x2C,
            CpuIntrFromCpu2 = 0x30,
            CpuIntrFromCpu3 = 0x34,
            RsaPdCtrl = 0x38,
            EdmaCtrl = 0x3C,
            CacheControl = 0x40,
            ExternalDeviceEncryptDecryptControl = 0x44,
            RtcFastmemConfig = 0x48,
            RtcFastmemCrc = 0x4C,
            RedundantEcoCtrl = 0x50,
            ClockGate = 0x54,
            SysclkConf = 0x58,
            MemPvt = 0x5C,
            CombPvtLvtConf = 0x60,
            CombPvtNvtConf = 0x64,
            CombPvtHvtConf = 0x68,
            CombPvtErrLvtSite0 = 0x6C,
            CombPvtErrNvtSite0 = 0x70,
            CombPvtErrHvtSite0 = 0x74,
            CombPvtErrLvtSite1 = 0x78,
            CombPvtErrNvtSite1 = 0x7C,
            CombPvtErrHvtSite1 = 0x80,
            CombPvtErrLvtSite2 = 0x84,
            CombPvtErrNvtSite2 = 0x88,
            CombPvtErrHvtSite2 = 0x8C,
            CombPvtErrLvtSite3 = 0x90,
            CombPvtErrNvtSite3 = 0x94,
            CombPvtErrHvtSite3 = 0x98,
            Date = 0xFFC,
        }
    }
}
