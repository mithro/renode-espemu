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
            Registers.PeripClkEn0.Define(this, 0xF1C0E06F)
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
            // Writing bit 0 = 1 fires the corresponding GPIO output (interrupt source).
            // Writing bit 0 = 0 clears it. Reading returns pending state.
            for (int i = 0; i < NumCpuIntrSources; i++)
            {
                int idx = i;
                ((Registers)(0x28 + i * 4)).Define(this)
                    .WithFlag(0, name: $"CPU_INTR_FROM_CPU_{idx}",
                        writeCallback: (_, value) =>
                        {
                            cpuIntrPending[idx] = value;
                            Connections[idx].Set(value);
                            if (value)
                            {
                                this.Log(LogLevel.Debug, "FROM_CPU_INTR{0} asserted (-> interrupt matrix source {1})",
                                    idx, 50 + idx);
                            }
                            else
                            {
                                this.Log(LogLevel.Debug, "FROM_CPU_INTR{0} cleared", idx);
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
            // [19] CLK_DIV_EN (RO, 0), [18:12] CLK_XTAL_FREQ (40 = 0x28),
            // [11:10] SOC_CLK_SEL (0 = XTAL), [9:0] PRE_DIV_CNT (default 1)
            // After ROM boot, firmware expects PLL at 160MHz: SOC_CLK_SEL=1 (PLL)
            // Default with XTAL=40MHz: 0x28 << 12 | 0x01 = 0x00028001
            Registers.SysclkConf.Define(this, 0x00028001)
                .WithValueField(0, 32, name: "SYSCLK_CONF");

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
            Date = 0xFFC,
        }
    }
}
