// ESP32-C3 SYSCON peripheral for Renode
//
// The SYSCON block at 0x60026000 contains system configuration registers
// including WiFi/BT clock control, memory power, and the RNG data register.
//
// Register map from ESP-IDF syscon_reg.h:
//   0x000: SYSCON_WIFI_BB_CFG_REG
//   0x004: SYSCON_WIFI_BB_CFG_2_REG
//   0x008: SYSCON_HOST_INF_SEL_REG
//   0x014: SYSCON_WIFI_CLK_EN_REG (critical: reset default 0xFFFFFFFF)
//   0x018: SYSCON_WIFI_RST_EN_REG
//   0x028-0x054: Various config registers
//   0x088-0x0AC: Memory power and clock gate registers
//   0x0B0: SYSCON_RND_DATA_REG (hardware RNG, read-only)
//   0x3FC: SYSCON_DATE_REG

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class ESP32C3_RNG : BasicDoubleWordPeripheral, IKnownSize
    {
        public ESP32C3_RNG(Machine machine) : base(machine)
        {
            random = new PseudoRandom();
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            random.Reset();
        }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            // 0x000: SYSCON_WIFI_BB_CFG_REG
            Registers.WifiBbCfg.Define(this)
                .WithValueField(0, 32, name: "WIFI_BB_CFG");

            // 0x004: SYSCON_WIFI_BB_CFG_2_REG
            Registers.WifiBbCfg2.Define(this)
                .WithValueField(0, 32, name: "WIFI_BB_CFG_2");

            // 0x008: SYSCON_HOST_INF_SEL_REG
            Registers.HostInfSel.Define(this)
                .WithValueField(0, 32, name: "HOST_INF_SEL");

            // 0x014: SYSCON_WIFI_CLK_EN_REG (default: all clocks enabled)
            Registers.WifiClkEn.Define(this, 0xFFFFFFFF)
                .WithValueField(0, 32, name: "WIFI_CLK_EN");

            // 0x018: SYSCON_WIFI_RST_EN_REG
            Registers.WifiRstEn.Define(this)
                .WithValueField(0, 32, name: "WIFI_RST_EN");

            // 0x028-0x054: Tick/calibration config registers
            Registers.BtLpckDivInt.Define(this).WithValueField(0, 32, name: "BT_LPCK_DIV_INT");
            Registers.BtLpckDivFrac.Define(this).WithValueField(0, 32, name: "BT_LPCK_DIV_FRAC");
            Registers.CpuIntrFromCpu0.Define(this).WithValueField(0, 32, name: "CPU_INTR_FROM_CPU_0");
            Registers.CpuIntrFromCpu1.Define(this).WithValueField(0, 32, name: "CPU_INTR_FROM_CPU_1");
            Registers.EdgecaseBoostUpdate.Define(this).WithValueField(0, 32, name: "EDGECASE_BOOST_UPDATE");
            Registers.ReservedReg38.Define(this).WithValueField(0, 32, name: "RESERVED_38");
            Registers.ReservedReg3C.Define(this).WithValueField(0, 32, name: "RESERVED_3C");
            Registers.ReservedReg40.Define(this).WithValueField(0, 32, name: "RESERVED_40");
            Registers.ReservedReg44.Define(this).WithValueField(0, 32, name: "RESERVED_44");
            Registers.ReservedReg48.Define(this).WithValueField(0, 32, name: "RESERVED_48");
            Registers.ReservedReg4C.Define(this).WithValueField(0, 32, name: "RESERVED_4C");
            Registers.ReservedReg50.Define(this).WithValueField(0, 32, name: "RESERVED_50");
            Registers.ReservedReg54.Define(this).WithValueField(0, 32, name: "RESERVED_54");

            // 0x088-0x0AC: Memory and clock gate registers
            Registers.FrontEndMemPd.Define(this).WithValueField(0, 32, name: "FRONT_END_MEM_PD");
            Registers.ReservedReg8C.Define(this).WithValueField(0, 32, name: "RESERVED_8C");
            Registers.ReservedReg90.Define(this).WithValueField(0, 32, name: "RESERVED_90");
            Registers.ReservedReg94.Define(this).WithValueField(0, 32, name: "RESERVED_94");
            Registers.ReservedReg98.Define(this).WithValueField(0, 32, name: "RESERVED_98");
            Registers.FrontEndMemPd2.Define(this).WithValueField(0, 32, name: "FRONT_END_MEM_PD_2");
            Registers.Retention.Define(this).WithValueField(0, 32, name: "RETENTION_CTRL");
            Registers.ClkgateForceOn.Define(this, 0x3F).WithValueField(0, 32, name: "CLKGATE_FORCE_ON");
            Registers.MemPowerDown.Define(this).WithValueField(0, 32, name: "MEM_POWER_DOWN");
            Registers.MemPowerUp.Define(this, 0x3F).WithValueField(0, 32, name: "MEM_POWER_UP");

            // 0x0B0: SYSCON_RND_DATA_REG (hardware random number generator)
            Registers.RndData.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "RND_DATA",
                    valueProviderCallback: _ => random.Next());

            // 0x3FC: SYSCON_DATE_REG
            Registers.Date.Define(this, 0x02007150)
                .WithValueField(0, 32, name: "DATE");
        }

        private readonly PseudoRandom random;

        private class PseudoRandom
        {
            private uint state = InitialState;

            public uint Next()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return state;
            }

            public void Reset()
            {
                state = InitialState;
            }

            private const uint InitialState = 0xDEADBEEF;
        }

        private enum Registers : long
        {
            WifiBbCfg = 0x000,
            WifiBbCfg2 = 0x004,
            HostInfSel = 0x008,
            WifiClkEn = 0x014,
            WifiRstEn = 0x018,
            BtLpckDivInt = 0x028,
            BtLpckDivFrac = 0x02C,
            CpuIntrFromCpu0 = 0x030,
            CpuIntrFromCpu1 = 0x034,
            EdgecaseBoostUpdate = 0x038,
            ReservedReg38 = 0x03C,
            ReservedReg3C = 0x040,
            ReservedReg40 = 0x044,
            ReservedReg44 = 0x048,
            ReservedReg48 = 0x04C,
            ReservedReg4C = 0x050,
            ReservedReg50 = 0x054,
            ReservedReg54 = 0x058,
            FrontEndMemPd = 0x088,
            ReservedReg8C = 0x08C,
            ReservedReg90 = 0x090,
            ReservedReg94 = 0x094,
            ReservedReg98 = 0x098,
            FrontEndMemPd2 = 0x09C,
            Retention = 0x0A0,
            ClkgateForceOn = 0x0A4,
            MemPowerDown = 0x0A8,
            MemPowerUp = 0x0AC,
            RndData = 0x0B0,
            Date = 0x3FC,
        }
    }
}
